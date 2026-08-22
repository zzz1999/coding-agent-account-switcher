using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using CodingAgentAccountSwitcher.Core;

namespace CodingAgentAccountSwitcher.App;

public partial class MainWindow : Window
{
    private const string ProfilesHiddenNoticeKey = "Status.ProfilesHidden";
    private const int DwmWindowCornerPreferenceAttribute = 33;
    private const int DwmWindowBorderColorAttribute = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;

    private readonly LocalizationService _localization;
    private readonly ApplicationSettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistration;
    private readonly IUpdateCheckService _updateCheckService = new GitHubUpdateCheckService();
    private readonly IReadOnlyDictionary<AgentProvider, ProviderContext> _contexts;
    private readonly Dictionary<AgentProvider, ProviderNotice> _providerNotices = [];
    private readonly Version _applicationVersion;

    private IReadOnlyList<AccountCardViewModel> _visibleAccounts = [];
    private AgentProvider _selectedProvider = AgentProvider.Codex;
    private PendingAccountOperation? _pendingOperation;
    private AccountCardViewModel? _profileBeingRenamed;
    private AccountCardViewModel? _profilePendingDeletion;
    private Guid? _confirmedReplacementProfileId;
    private IInputElement? _dialogReturnFocus;
    private CancellationTokenSource? _updateCheckCancellationSource;
    private bool _isBusy;
    private bool _isCheckingForUpdates;
    private bool _activeDeletionConfirmed;
    private bool _isDarkTheme;
    private bool _suppressSettingsEvents;
    private bool _startupStateKnown = true;
    private ApplicationSettings _settings;
    private UpdateCheckViewState _updateCheckState;
    private Version? _latestVersion;
    private Uri? _latestReleasePage;
    private string? _updateFailureDetail;

    public MainWindow(
        LocalizationService localization,
        ApplicationSettingsService settingsService,
        StartupRegistrationService startupRegistration,
        ApplicationSettings settings)
    {
        _localization = localization;
        _settingsService = settingsService;
        _startupRegistration = startupRegistration;
        _settings = settings;
        _applicationVersion = GitHubUpdateCheckService.NormalizeVersion(
            typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0));

        InitializeComponent();
        ConfigureNativeWindowCorners();
        ApplyTheme(_settings.UseDarkTheme);

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory could not be resolved.");
        }

        var storageRoot = System.IO.Path.Combine(localApplicationData, "CodingAgentAccountSwitcher");
        IAuthenticationAdapter[] adapters =
        {
            new CodexAuthenticationAdapter(),
            new ClaudeCodeAuthenticationAdapter(),
            new OpenCodeAuthenticationAdapter(),
        };
        _contexts = adapters.ToDictionary(
            adapter => adapter.Provider,
            adapter => CreateProviderContext(storageRoot, adapter));

        _suppressSettingsEvents = true;
        LanguageComboBox.ItemsSource = _localization.SupportedLanguages;
        LanguageComboBox.SelectedItem = _localization.SupportedLanguages.First(option =>
            string.Equals(option.Code, _settings.Language, StringComparison.OrdinalIgnoreCase));
        try
        {
            var startupEnabled = _startupRegistration.IsEnabled();
            _settings = _settings with { StartWithWindows = startupEnabled };
            StartupToggle.IsChecked = startupEnabled;
        }
        catch (Exception exception)
        {
            _startupStateKnown = false;
            StartupToggle.IsChecked = null;
            StartupToggle.IsEnabled = false;
            SettingsErrorText.Text = _localization.Get("Settings.StartupFailure", exception.Message);
        }
        finally
        {
            _suppressSettingsEvents = false;
        }

        ApplyLanguageLayout();
        ApplyUpdateStatusText();
        ShowProvider(AgentProvider.Codex);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyNativeWindowAppearance();
    }

    private void ConfigureNativeWindowCorners()
    {
        var chrome = WindowChrome.GetWindowChrome(this);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // WindowChrome's custom region has visibly aliased edges. On
            // Windows 11, let DWM perform the final GPU-composited clipping.
            if (chrome is not null)
            {
                chrome.CornerRadius = default;
            }

            return;
        }

        // Windows 10 has no native DWM corner preference. A layered WPF window
        // gives only the four outer corners per-pixel alpha, while the opaque
        // MainShell keeps the rest of the interface rendered at native DPI.
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        if (chrome is not null)
        {
            chrome.CornerRadius = default;
        }
    }

    private void ApplyNativeWindowAppearance()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var cornerPreference = DwmWindowCornerPreference.Round;
        _ = DwmSetWindowAttribute(
            handle,
            DwmWindowCornerPreferenceAttribute,
            ref cornerPreference,
            Marshal.SizeOf<DwmWindowCornerPreference>());

        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            handle,
            DwmWindowBorderColorAttribute,
            ref borderColor,
            sizeof(uint));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            foreach (var context in _contexts.Values)
            {
                var adapter = context.Adapter;
                SwitchTransactionJournal? journal;
                try
                {
                    journal = context.Vault.GetPendingJournal(adapter.Provider);
                }
                catch (Exception exception)
                {
                    _providerNotices[adapter.Provider] = new ProviderNotice(
                        "Recovery.MetadataInvalid",
                        [ProviderDisplayName(adapter.Provider), exception.Message],
                        StatusTone.Error);
                    continue;
                }

                if (journal is null)
                {
                    continue;
                }

                var result = await context.SwitchService.RecoverAsync(adapter);
                switch (result.Status)
                {
                    case RecoveryStatus.RecoveredToSource:
                    case RecoveryStatus.CompletedTargetActivation:
                        _providerNotices[adapter.Provider] = new ProviderNotice(
                            "Recovery.Recovered",
                            [ProviderDisplayName(adapter.Provider)],
                            StatusTone.Success);
                        break;
                    case RecoveryStatus.BlockedByRunningProcesses:
                        _providerNotices[adapter.Provider] = new ProviderNotice(
                            "Recovery.CloseThenRetry",
                            [ProviderDisplayName(adapter.Provider)],
                            StatusTone.Warning);
                        break;
                    case RecoveryStatus.ProcessInspectionUnknown:
                        _providerNotices[adapter.Provider] = new ProviderNotice(
                            "Recovery.ProcessUnknown",
                            [ProviderDisplayName(adapter.Provider)],
                            StatusTone.Warning);
                        break;
                    case RecoveryStatus.LockUnavailable:
                        _providerNotices[adapter.Provider] = new ProviderNotice(
                            "Recovery.InProgress",
                            [ProviderDisplayName(adapter.Provider)],
                            StatusTone.Warning);
                        break;
                    case RecoveryStatus.ManualInterventionRequired:
                    case RecoveryStatus.Failed:
                        _providerNotices[adapter.Provider] = new ProviderNotice(
                            "Recovery.Attention",
                            [ProviderDisplayName(adapter.Provider)],
                            StatusTone.Error,
                            result.ErrorMessage);
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            _providerNotices[_selectedProvider] = new ProviderNotice(
                "Recovery.StartupFailed",
                [exception.Message],
                StatusTone.Error);
        }
        finally
        {
            SetBusy(false);
            if (ReloadProfiles(_selectedProvider))
            {
                ApplyProviderStatus(_selectedProvider);
            }
        }
    }

    private void ProviderSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || _isBusy)
        {
            return;
        }

        var provider = ReferenceEquals(sender, ClaudeSegment)
            ? AgentProvider.ClaudeCode
            : ReferenceEquals(sender, OpenCodeSegment)
                ? AgentProvider.OpenCode
                : AgentProvider.Codex;
        ShowProvider(provider);
    }

    private bool ShowProvider(AgentProvider provider)
    {
        _selectedProvider = provider;
        var adapter = _contexts[provider].Adapter;
        ProviderTitle.Text = T("Provider.Accounts", ProviderDisplayName(provider));
        ProviderDescription.Text = T(
            "Provider.ManagedFiles",
            string.Join("  ·  ", adapter.ManagedFilePaths.Select(FormatAuthenticationPath)));

        if (ReloadProfiles(provider))
        {
            ApplyProviderStatus(provider);
            return true;
        }

        return false;
    }

    private void ApplyProviderStatus(AgentProvider provider)
    {
        if (_providerNotices.TryGetValue(provider, out var notice))
        {
            var message = T(notice.ResourceKey, notice.Arguments);
            SetStatus(
                notice.ErrorDetail is null ? message : BuildFailureMessage(message, notice.ErrorDetail),
                notice.Tone);
            return;
        }

        SetStatus(T("Status.ReadyProvider", ProviderDisplayName(provider)), StatusTone.Ready);
    }

    private bool ReloadProfiles(AgentProvider provider)
    {
        try
        {
            var vault = _contexts[provider].Vault;
            var activeProfile = vault.GetActiveProfile(provider);
            var profileList = vault.ListProfilesWithIssues(provider);
            UpdateProfileLoadNotice(provider, profileList.Issues.Count);
            _visibleAccounts = profileList.Profiles
                .Select(profile => new AccountCardViewModel(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.Provider,
                    profile.CapturedAtUtc,
                    activeProfile?.ProfileId == profile.ProfileId,
                    _localization))
                .ToArray();

            AccountItems.ItemsSource = _visibleAccounts;
            EmptyStatePanel.Visibility = _visibleAccounts.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (_visibleAccounts.Count == 0)
            {
                if (profileList.Issues.Count > 0)
                {
                    EmptyStateTitle.Text = T("Empty.Unavailable.Title");
                    EmptyStateMessage.Text = T("Empty.Unavailable.Message");
                }
                else
                {
                    var currentSnapshotExists = _contexts[provider].Adapter.HasCurrentSnapshot;
                    EmptyStateTitle.Text = currentSnapshotExists
                        ? T("Empty.LoginDetected.Title")
                        : T("Empty.NoLogin.Title");
                    EmptyStateMessage.Text = currentSnapshotExists
                        ? T("Empty.LoginDetected.Message")
                        : T("Empty.NoLogin.Message");
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            _visibleAccounts = [];
            AccountItems.ItemsSource = _visibleAccounts;
            EmptyStatePanel.Visibility = Visibility.Visible;
            EmptyStateTitle.Text = T("Empty.Unavailable.Title");
            EmptyStateMessage.Text = T("Empty.Unavailable.Message");
            SetStatus(
                T("Status.ProfilesLoadFailed", LocalizeOperationError(exception.Message)),
                StatusTone.Error);
            return false;
        }
    }

    private void UpdateProfileLoadNotice(AgentProvider provider, int hiddenProfileCount)
    {
        if (hiddenProfileCount > 0)
        {
            if (!_providerNotices.TryGetValue(provider, out var existingNotice) ||
                existingNotice.ResourceKey == ProfilesHiddenNoticeKey)
            {
                _providerNotices[provider] = new ProviderNotice(
                    ProfilesHiddenNoticeKey,
                    [hiddenProfileCount],
                    StatusTone.Warning);
            }

            return;
        }

        if (_providerNotices.TryGetValue(provider, out var notice) &&
            notice.ResourceKey == ProfilesHiddenNoticeKey)
        {
            _providerNotices.Remove(provider);
        }
    }

    private async void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || sender is not Button { DataContext: AccountCardViewModel account })
        {
            return;
        }

        await AttemptSwitchAsync(account.Provider, account.ProfileId, account.Name);
    }

    private void ProfileName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 ||
            _isBusy ||
            sender is not TextBlock { DataContext: AccountCardViewModel account })
        {
            return;
        }

        e.Handled = true;
        ShowRenameProfileDialog(account);
    }

    private void ProfileName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.F2) ||
            _isBusy ||
            sender is not TextBlock { DataContext: AccountCardViewModel account })
        {
            return;
        }

        e.Handled = true;
        ShowRenameProfileDialog(account);
    }

    private void ShowRenameProfileDialog(AccountCardViewModel account)
    {
        _profileBeingRenamed = account;
        RenameProfileDescription.Text = T("Rename.Description", account.Name);
        RenameProfileTextBox.Text = account.Name;
        RenameProfileValidationText.Visibility = Visibility.Collapsed;
        RememberDialogFocusIfOpening(RenameProfileDialogOverlay);
        RenameProfileDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() =>
        {
            RenameProfileTextBox.Focus();
            RenameProfileTextBox.SelectAll();
        });
    }

    private async void ConfirmRenameProfile_Click(object sender, RoutedEventArgs e)
    {
        var account = _profileBeingRenamed;
        if (_isBusy || account is null)
        {
            return;
        }

        var displayName = RenameProfileTextBox.Text.Trim();
        if (!ValidateProfileName(displayName, RenameProfileTextBox, RenameProfileValidationText))
        {
            return;
        }

        SetBusy(true);
        ProfileManagementResult result;
        try
        {
            result = await _contexts[account.Provider].SwitchService.RenameProfileAsync(
                account.Provider,
                account.ProfileId,
                displayName);
        }
        catch (Exception exception)
        {
            result = new ProfileManagementResult
            {
                Status = ProfileManagementStatus.Failed,
                ErrorMessage = exception.Message,
            };
        }
        finally
        {
            SetBusy(false);
        }

        switch (result.Status)
        {
            case ProfileManagementStatus.Success:
                CloseRenameProfileDialog();
                if (_selectedProvider != account.Provider || ReloadProfiles(account.Provider))
                {
                    SetStatus(T("Status.ProfileRenamed", account.Name, displayName), StatusTone.Success);
                }
                break;
            case ProfileManagementStatus.DisplayNameConflict:
                RenameProfileValidationText.Text = T("Rename.NameConflict", displayName);
                RenameProfileValidationText.Visibility = Visibility.Visible;
                RenameProfileTextBox.Focus();
                RenameProfileTextBox.SelectAll();
                break;
            case ProfileManagementStatus.ProfileNotFound:
                CloseRenameProfileDialog();
                ReloadProfiles(account.Provider);
                SetStatus(T("Status.ProfileManagementMissing"), StatusTone.Error);
                break;
            case ProfileManagementStatus.LockUnavailable:
                SetStatus(T("Status.OperationInProgress"), StatusTone.Warning);
                break;
            case ProfileManagementStatus.RecoveryRequired:
                SetStatus(T("Status.ProfileManagementRecoveryRequired"), StatusTone.Error);
                break;
            default:
                SetStatus(
                    BuildFailureMessage(T("Rename.Failure"), result.ErrorMessage),
                    StatusTone.Error);
                break;
        }
    }

    private void RenameProfileTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        ConfirmRenameProfile_Click(sender, new RoutedEventArgs());
    }

    private void RenameProfileTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (RenameProfileValidationText is not null)
        {
            RenameProfileValidationText.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelRenameProfileDialog_Click(object sender, RoutedEventArgs e) =>
        CloseRenameProfileDialog();

    private void CloseRenameProfileDialog()
    {
        var wasVisible = RenameProfileDialogOverlay.Visibility == Visibility.Visible;
        RenameProfileDialogOverlay.Visibility = Visibility.Collapsed;
        RenameProfileValidationText.Visibility = Visibility.Collapsed;
        RenameProfileTextBox.Clear();
        _profileBeingRenamed = null;
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || sender is not Button { DataContext: AccountCardViewModel account })
        {
            return;
        }

        _profilePendingDeletion = account;
        _activeDeletionConfirmed = account.IsActive;
        DeleteProfileMessage.Text = T(
            account.IsActive ? "Delete.ActiveMessage" : "Delete.Message",
            account.Name);
        DeleteProfileErrorText.Visibility = Visibility.Collapsed;
        RememberDialogFocusIfOpening(DeleteProfileDialogOverlay);
        DeleteProfileDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => Keyboard.Focus(CancelDeleteProfileButton));
    }

    private async void ConfirmDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var account = _profilePendingDeletion;
        if (_isBusy || account is null)
        {
            return;
        }

        SetBusy(true);
        ProfileManagementResult result;
        try
        {
            result = await _contexts[account.Provider].SwitchService.DeleteProfileAsync(
                account.Provider,
                account.ProfileId,
                _activeDeletionConfirmed);
        }
        catch (Exception exception)
        {
            result = new ProfileManagementResult
            {
                Status = ProfileManagementStatus.Failed,
                ErrorMessage = exception.Message,
            };
        }
        finally
        {
            SetBusy(false);
        }

        switch (result.Status)
        {
            case ProfileManagementStatus.Success:
                CloseDeleteProfileDialog();
                if (_selectedProvider != account.Provider || ReloadProfiles(account.Provider))
                {
                    SetStatus(
                        T(
                            result.RemovedActiveSelection
                                ? "Status.ActiveProfileDeleted"
                                : "Status.ProfileDeleted",
                            account.Name),
                        result.RemovedActiveSelection ? StatusTone.Warning : StatusTone.Success);
                }
                break;
            case ProfileManagementStatus.ActiveProfileConfirmationRequired:
                // Another app instance can activate this profile after the dialog opens.
                // Update the warning and require one more explicit click before deletion.
                _activeDeletionConfirmed = true;
                DeleteProfileMessage.Text = T("Delete.ActiveMessage", account.Name);
                DeleteProfileErrorText.Text = T("Delete.ActiveConfirmation");
                DeleteProfileErrorText.Visibility = Visibility.Visible;
                Keyboard.Focus(CancelDeleteProfileButton);
                break;
            case ProfileManagementStatus.ProfileNotFound:
                CloseDeleteProfileDialog();
                ReloadProfiles(account.Provider);
                SetStatus(T("Status.ProfileManagementMissing"), StatusTone.Error);
                break;
            case ProfileManagementStatus.LockUnavailable:
                DeleteProfileErrorText.Text = T("Status.OperationInProgress");
                DeleteProfileErrorText.Visibility = Visibility.Visible;
                break;
            case ProfileManagementStatus.RecoveryRequired:
                DeleteProfileErrorText.Text = T("Status.ProfileManagementRecoveryRequired");
                DeleteProfileErrorText.Visibility = Visibility.Visible;
                break;
            default:
                DeleteProfileErrorText.Text = BuildFailureMessage(
                    T("Delete.Failure"),
                    result.ErrorMessage);
                DeleteProfileErrorText.Visibility = Visibility.Visible;
                break;
        }
    }

    private void CancelDeleteProfileDialog_Click(object sender, RoutedEventArgs e) =>
        CloseDeleteProfileDialog();

    private void CloseDeleteProfileDialog()
    {
        var wasVisible = DeleteProfileDialogOverlay.Visibility == Visibility.Visible;
        DeleteProfileDialogOverlay.Visibility = Visibility.Collapsed;
        DeleteProfileErrorText.Visibility = Visibility.Collapsed;
        _profilePendingDeletion = null;
        _activeDeletionConfirmed = false;
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private void SaveCurrentLogin_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var personalLabel = T("Save.SuggestedPersonal");
        var workLabel = T("Save.SuggestedWork");
        var suggestedName = _visibleAccounts.All(account =>
            !string.Equals(account.Name, personalLabel, StringComparison.OrdinalIgnoreCase))
            ? personalLabel
            : _visibleAccounts.All(account =>
                !string.Equals(account.Name, workLabel, StringComparison.OrdinalIgnoreCase))
                ? workLabel
                : string.Empty;

        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = T("Save.Snapshot");
        ProfileNameTextBox.Text = suggestedName;
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
        RememberDialogFocusIfOpening(SaveProfileDialogOverlay);
        SaveProfileDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() =>
        {
            ProfileNameTextBox.Focus();
            ProfileNameTextBox.SelectAll();
        });
    }

    private async void ConfirmSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var displayName = ProfileNameTextBox.Text.Trim();
        if (!ValidateProfileName(displayName, ProfileNameTextBox, ProfileNameValidationText))
        {
            return;
        }

        var existingProfile = _visibleAccounts.FirstOrDefault(account =>
            string.Equals(account.Name, displayName, StringComparison.OrdinalIgnoreCase));
        if (existingProfile is not null && _confirmedReplacementProfileId != existingProfile.ProfileId)
        {
            _confirmedReplacementProfileId = existingProfile.ProfileId;
            ProfileNameValidationText.Text = T("Save.ReplaceConfirm", existingProfile.Name);
            ProfileNameValidationText.Visibility = Visibility.Visible;
            ConfirmSaveProfileButton.Content = T("Save.Replace");
            return;
        }

        CloseSaveProfileDialog();
        await AttemptCaptureAsync(
            _selectedProvider,
            existingProfile?.Name ?? displayName,
            existingProfile?.ProfileId);
    }

    private bool ValidateProfileName(
        string displayName,
        TextBox input,
        TextBlock validationText)
    {
        string? validationMessage = null;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            validationMessage = T("Save.EnterLabel");
        }
        else if (displayName.Length > 80 || displayName.Any(char.IsControl))
        {
            validationMessage = T("Save.InvalidLabel");
        }
        if (validationMessage is null)
        {
            validationText.Visibility = Visibility.Collapsed;
            return true;
        }

        validationText.Text = validationMessage;
        validationText.Visibility = Visibility.Visible;
        input.Focus();
        return false;
    }

    private void CancelSaveProfileDialog_Click(object sender, RoutedEventArgs e) =>
        CloseSaveProfileDialog();

    private void ProfileNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        ConfirmSaveProfile_Click(sender, new RoutedEventArgs());
    }

    private void ProfileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_confirmedReplacementProfileId is null)
        {
            return;
        }

        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = T("Save.Snapshot");
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
    }

    private void CloseSaveProfileDialog()
    {
        var wasVisible = SaveProfileDialogOverlay.Visibility == Visibility.Visible;
        SaveProfileDialogOverlay.Visibility = Visibility.Collapsed;
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
        ProfileNameTextBox.Clear();
        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = T("Save.Snapshot");
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private async Task AttemptCaptureAsync(
        AgentProvider provider,
        string displayName,
        Guid? profileToReplace = null)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        SetStatus(
            T("Status.CheckingSave"),
            StatusTone.Ready);

        CaptureProfileResult result;
        try
        {
            var context = _contexts[provider];
            result = await context.SwitchService.CaptureCurrentLoginAsync(
                context.Adapter,
                displayName,
                profileToReplace);
        }
        catch (Exception exception)
        {
            SetBusy(false);
            CloseProcessDialog();
            SetStatus(
                T("Status.SaveException", LocalizeOperationError(exception.Message)),
                StatusTone.Error);
            return;
        }

        SetBusy(false);
        if (result.Status is not AccountOperationStatus.LockUnavailable and
            not AccountOperationStatus.RecoveryRequired)
        {
            _providerNotices.Remove(provider);
        }

        switch (result.Status)
        {
            case AccountOperationStatus.Success:
                CloseProcessDialog();
                _providerNotices.Remove(provider);
                var captureProfilesLoaded = _selectedProvider != provider || ReloadProfiles(provider);
                if (captureProfilesLoaded)
                {
                    var captureVerb = T(profileToReplace.HasValue
                        ? "Status.UpdatedVerb"
                        : "Status.SavedVerb");
                    SetStatus(T(
                            "Status.ProfileCaptured",
                            captureVerb,
                            displayName,
                            ProviderDisplayName(provider)),
                        StatusTone.Success);
                }
                break;
            case AccountOperationStatus.AuthenticationFileMissing:
                CloseProcessDialog();
                SetStatus(
                    T("Status.LoginMissing", ProviderDisplayName(provider)),
                    StatusTone.Error);
                break;
            case AccountOperationStatus.AuthenticationFileEmpty:
                CloseProcessDialog();
                SetStatus(T("Status.AuthenticationEmpty"), StatusTone.Error);
                break;
            case AccountOperationStatus.ProfileNotFound:
                CloseProcessDialog();
                ReloadProfiles(provider);
                SetStatus(T("Status.ProfileReplaceMissing"), StatusTone.Error);
                break;
            case AccountOperationStatus.DisplayNameConflict:
                CloseProcessDialog();
                ReloadProfiles(provider);
                SetStatus(T("Rename.NameConflict", displayName), StatusTone.Warning);
                break;
            case AccountOperationStatus.LockUnavailable:
                CloseProcessDialog();
                SetStatus(T("Status.OperationInProgress"), StatusTone.Warning);
                break;
            case AccountOperationStatus.RecoveryRequired:
                CloseProcessDialog();
                var captureRecoveryMessage = BuildFailureMessage(
                    T("Status.CaptureRecovery"),
                    result.ErrorMessage);
                _providerNotices[provider] = new ProviderNotice(
                    "Status.CaptureRecovery",
                    [],
                    StatusTone.Error,
                    result.ErrorMessage);
                SetStatus(captureRecoveryMessage, StatusTone.Error);
                break;
            default:
                CloseProcessDialog();
                SetStatus(BuildFailureMessage(T("Status.SaveFailed"), result.ErrorMessage), StatusTone.Error);
                break;
        }
    }

    private async Task AttemptSwitchAsync(
        AgentProvider provider,
        Guid profileId,
        string displayName,
        string? confirmationFingerprint = null)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        SetStatus(
            T("Status.CheckingSwitch", ProviderDisplayName(provider)),
            StatusTone.Ready);

        SwitchProfileResult result;
        try
        {
            var context = _contexts[provider];
            result = await context.SwitchService.SwitchAsync(
                context.Adapter,
                profileId,
                confirmationFingerprint);
        }
        catch (Exception exception)
        {
            SetBusy(false);
            CloseProcessDialog();
            SetStatus(
                T("Status.SwitchException", LocalizeOperationError(exception.Message)),
                StatusTone.Error);
            return;
        }

        SetBusy(false);
        if (result.Status is not AccountOperationStatus.BlockedByRunningProcesses and
            not AccountOperationStatus.ProcessInspectionUnknown and
            not AccountOperationStatus.LockUnavailable and
            not AccountOperationStatus.RecoveryRequired)
        {
            _providerNotices.Remove(provider);
        }

        switch (result.Status)
        {
            case AccountOperationStatus.Success:
                CloseProcessDialog();
                CloseChangedLoginDialog();
                _providerNotices.Remove(provider);
                var switchProfilesLoaded = _selectedProvider != provider || ReloadProfiles(provider);
                var activeDisplayName = _visibleAccounts
                    .FirstOrDefault(account => account.ProfileId == profileId)
                    ?.Name ?? displayName;
                if (switchProfilesLoaded)
                {
                    SetStatus(T("Status.NowActive", activeDisplayName, ProviderDisplayName(provider)),
                        StatusTone.Success);
                }
                ShowSwitchSuccessDialog(provider, activeDisplayName);
                break;
            case AccountOperationStatus.AlreadyActive:
                CloseProcessDialog();
                SetStatus(T("Status.AlreadyActive", displayName), StatusTone.Ready);
                break;
            case AccountOperationStatus.BlockedByRunningProcesses:
            case AccountOperationStatus.ProcessInspectionUnknown:
                CloseChangedLoginDialog(clearPendingOperation: false);
                _pendingOperation = PendingAccountOperation.Switch(
                    provider,
                    profileId,
                    displayName);
                ShowProcessDialog(result.ProcessInspection);
                break;
            case AccountOperationStatus.ActiveProfileUpdateConfirmationRequired:
                CloseProcessDialog();
                _pendingOperation = PendingAccountOperation.Switch(
                    provider,
                    profileId,
                    displayName,
                    result.ConfirmationFingerprint);
                ShowChangedLoginDialog(result.SourceProfileId, isSavedSnapshotRestore: false);
                break;
            case AccountOperationStatus.SavedSnapshotRestoreConfirmationRequired:
                CloseProcessDialog();
                _pendingOperation = PendingAccountOperation.Switch(
                    provider,
                    profileId,
                    displayName,
                    result.ConfirmationFingerprint);
                ShowChangedLoginDialog(result.SourceProfileId, isSavedSnapshotRestore: true);
                break;
            case AccountOperationStatus.AuthenticationFileMissing:
                CloseProcessDialog();
                SetStatus(T("Status.CurrentAuthMissing"),
                    StatusTone.Error);
                break;
            case AccountOperationStatus.ProfileNotFound:
                CloseProcessDialog();
                ReloadProfiles(provider);
                SetStatus(T("Status.ProfileMissing"), StatusTone.Error);
                break;
            case AccountOperationStatus.LockUnavailable:
                CloseProcessDialog();
                SetStatus(T("Status.OperationInProgress"), StatusTone.Warning);
                break;
            case AccountOperationStatus.RecoveryRequired:
                CloseProcessDialog();
                var switchRecoveryMessage = BuildFailureMessage(
                    T("Status.SwitchRecovery"),
                    result.ErrorMessage);
                _providerNotices[provider] = new ProviderNotice(
                    "Status.SwitchRecovery",
                    [],
                    StatusTone.Error,
                    result.ErrorMessage);
                SetStatus(switchRecoveryMessage, StatusTone.Error);
                break;
            default:
                CloseProcessDialog();
                var prefix = result.RolledBack
                    ? T("Status.SwitchFailedRestored")
                    : T("Status.SwitchFailed");
                SetStatus(BuildFailureMessage(prefix, result.ErrorMessage), StatusTone.Error);
                break;
        }
    }

    private async void RecheckProcesses_Click(object sender, RoutedEventArgs e)
    {
        var pendingOperation = _pendingOperation;
        if (_isBusy || pendingOperation is null)
        {
            return;
        }

        await AttemptSwitchAsync(
            pendingOperation.Provider,
            pendingOperation.ProfileId,
            pendingOperation.DisplayName,
            pendingOperation.ConfirmationFingerprint);
    }

    private void ShowChangedLoginDialog(Guid? sourceProfileId, bool isSavedSnapshotRestore)
    {
        var sourceName = _visibleAccounts
            .FirstOrDefault(account => account.ProfileId == sourceProfileId)
            ?.Name ?? T("Changed.LastSelected");

        if (isSavedSnapshotRestore)
        {
            ChangedLoginDialogTitle.Text = T("Changed.Restore.Title");
            ChangedLoginDialogMessage.Text = T("Changed.Restore.Message", sourceName);
            ChangedLoginSafetyText.Text = T("Changed.Restore.Safety");
            ConfirmChangedLoginButton.Content = T("Changed.Restore.Action");
            AutomationProperties.SetName(ConfirmChangedLoginButton, T("Changed.Restore.Automation"));
            SetStatus(T("Status.ConfirmRestore"),
                StatusTone.Warning);
        }
        else
        {
            ChangedLoginDialogTitle.Text = T("Changed.Confirm.Title");
            ChangedLoginDialogMessage.Text = T("Changed.Confirm.Message", sourceName);
            ChangedLoginSafetyText.Text = T("Changed.Confirm.Safety");
            ConfirmChangedLoginButton.Content = T("Changed.Confirm.Action");
            AutomationProperties.SetName(ConfirmChangedLoginButton, T("Changed.Confirm.Automation"));
            SetStatus(T("Status.ConfirmCurrent"),
                StatusTone.Warning);
        }

        RememberDialogFocusIfOpening(ChangedLoginDialogOverlay);
        ChangedLoginDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => Keyboard.Focus(ConfirmChangedLoginButton));
    }

    private async void ConfirmChangedLoginSwitch_Click(object sender, RoutedEventArgs e)
    {
        var pendingOperation = _pendingOperation;
        if (_isBusy || pendingOperation is null)
        {
            return;
        }

        CloseChangedLoginDialog(clearPendingOperation: false);
        await AttemptSwitchAsync(
            pendingOperation.Provider,
            pendingOperation.ProfileId,
            pendingOperation.DisplayName,
            pendingOperation.ConfirmationFingerprint);
    }

    private void SaveChangedLoginAsNew_Click(object sender, RoutedEventArgs e)
    {
        CloseChangedLoginDialog();
        SaveCurrentLogin_Click(sender, e);
    }

    private void CancelChangedLoginDialog_Click(object sender, RoutedEventArgs e)
    {
        CloseChangedLoginDialog();
        SetStatus(T("Status.SwitchCancelled"), StatusTone.Ready);
    }

    private void CloseChangedLoginDialog(bool clearPendingOperation = true)
    {
        var wasVisible = ChangedLoginDialogOverlay.Visibility == Visibility.Visible;
        ChangedLoginDialogOverlay.Visibility = Visibility.Collapsed;
        ChangedLoginDialogMessage.Text = string.Empty;
        ChangedLoginSafetyText.Text = string.Empty;
        if (clearPendingOperation)
        {
            _pendingOperation = null;
        }
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private void CancelProcessDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        CloseProcessDialog();
        SetStatus(T("Status.OperationCancelled"), StatusTone.Ready);
    }

    private void ShowProcessDialog(ProcessInspectionResult inspection)
    {
        var rows = inspection.Processes
            .Select(process => new RunningProcessViewModel(
                process.ProcessName,
                T("Process.Detail", process.ProcessName, process.ProcessId),
                T("Process.Running")))
            .Concat(inspection.Issues.Select(issue => new RunningProcessViewModel(
                issue.ProcessName == "*" ? T("Process.Inspection") : issue.ProcessName,
                T("Process.Unverified"),
                T("Process.CheckFailed"))))
            .ToArray();

        if (inspection.Status == ProcessInspectionStatus.Unknown)
        {
            ProcessDialogTitle.Text = T("Process.Unknown.Title");
            ProcessDialogMessage.Text = T("Process.Unknown.Message");
        }
        else
        {
            ProcessDialogTitle.Text = T("Process.Close.Title");
            ProcessDialogMessage.Text = T("Process.Close.Message");
        }

        BlockingProcessItems.ItemsSource = rows;
        RememberDialogFocusIfOpening(ProcessDialogOverlay);
        ProcessDialogOverlay.Visibility = Visibility.Visible;
        SetStatus(T("Status.ProcessBlocked"), StatusTone.Warning);
        Dispatcher.BeginInvoke(() => Keyboard.Focus(RecheckButton));
    }

    private void CloseProcessDialog()
    {
        var wasVisible = ProcessDialogOverlay.Visibility == Visibility.Visible;
        ProcessDialogOverlay.Visibility = Visibility.Collapsed;
        BlockingProcessItems.ItemsSource = null;
        _pendingOperation = null;
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        ProviderSelector.IsEnabled = !isBusy;
        SaveCurrentLoginButton.IsEnabled = !isBusy;
        SettingsButton.IsEnabled = !isBusy;
        AccountItems.IsEnabled = !isBusy;
        RecheckButton.IsEnabled = !isBusy;
        CancelProcessDialogButton.IsEnabled = !isBusy;
        ConfirmSaveProfileButton.IsEnabled = !isBusy;
        ConfirmChangedLoginButton.IsEnabled = !isBusy;
        ConfirmRenameProfileButton.IsEnabled = !isBusy;
        CancelRenameProfileButton.IsEnabled = !isBusy;
        ConfirmDeleteProfileButton.IsEnabled = !isBusy;
        CancelDeleteProfileButton.IsEnabled = !isBusy;
        LanguageComboBox.IsEnabled = !isBusy;
        StartupToggle.IsEnabled = !isBusy && _startupStateKnown;
        CheckForUpdatesButton.IsEnabled = !isBusy && !_isCheckingForUpdates;
        OpenUpdatePageButton.IsEnabled = !isBusy && !_isCheckingForUpdates;
        CloseSwitchSuccessButton.IsEnabled = !isBusy;
    }

    private void ShowSwitchSuccessDialog(AgentProvider provider, string displayName)
    {
        var message = T("SwitchSuccess.Message", displayName);
        if (provider == AgentProvider.Codex)
        {
            message = $"{message}{Environment.NewLine}{Environment.NewLine}{T("SwitchSuccess.CodexProviderNotice")}";
        }

        SwitchSuccessMessage.Text = message;
        RememberDialogFocusIfOpening(SwitchSuccessDialogOverlay);
        SwitchSuccessDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => Keyboard.Focus(CloseSwitchSuccessButton));
    }

    private void CloseSwitchSuccessDialog_Click(object sender, RoutedEventArgs e) =>
        CloseSwitchSuccessDialog();

    private void CloseSwitchSuccessDialog()
    {
        var wasVisible = SwitchSuccessDialogOverlay.Visibility == Visibility.Visible;
        SwitchSuccessDialogOverlay.Visibility = Visibility.Collapsed;
        SwitchSuccessMessage.Text = string.Empty;
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusText.Text = message;
        var resourceKey = tone switch
        {
            StatusTone.Warning => "Brush.State.Warning",
            StatusTone.Error => "Brush.State.Danger",
            _ => "Brush.State.Success",
        };
        StatusDot.Fill = (Brush)FindResource(resourceKey);
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var previousSettings = _settings;
        var proposedSettings = previousSettings with { UseDarkTheme = !previousSettings.UseDarkTheme };
        try
        {
            ApplyTheme(proposedSettings.UseDarkTheme);
            _settingsService.Save(proposedSettings);
        }
        catch (Exception exception)
        {
            ApplyTheme(previousSettings.UseDarkTheme);
            ShowSettingsError(T("Settings.ThemeFailure", exception.Message));
            return;
        }

        _settings = proposedSettings;
        SettingsErrorText.Visibility = Visibility.Collapsed;
        SetStatus(T(_isDarkTheme ? "Status.DarkTheme" : "Status.LightTheme"), StatusTone.Ready);
    }

    private void ApplyTheme(bool useDarkTheme)
    {
        _isDarkTheme = useDarkTheme;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeIndex = dictionaries
            .Select((dictionary, index) => (dictionary, index))
            .First(item => item.dictionary.Source?.OriginalString.Contains(
                "Colors.", StringComparison.OrdinalIgnoreCase) == true)
            .index;

        dictionaries[themeIndex] = new ResourceDictionary
        {
            Source = new Uri(
                _isDarkTheme ? "Themes/Colors.Dark.xaml" : "Themes/Colors.Light.xaml",
                UriKind.Relative),
        };

        ThemeMoonIcon.Visibility = _isDarkTheme ? Visibility.Collapsed : Visibility.Visible;
        ThemeSunIcon.Visibility = _isDarkTheme ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        _suppressSettingsEvents = true;
        LanguageComboBox.SelectedItem = _localization.SupportedLanguages.First(option =>
            string.Equals(option.Code, _settings.Language, StringComparison.OrdinalIgnoreCase));
        try
        {
            var enabled = _startupRegistration.IsEnabled();
            _startupStateKnown = true;
            _settings = _settings with { StartWithWindows = enabled };
            StartupToggle.IsChecked = enabled;
            StartupToggle.IsEnabled = true;
            SettingsErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            _startupStateKnown = false;
            StartupToggle.IsChecked = null;
            StartupToggle.IsEnabled = false;
            ShowSettingsError(T("Settings.StartupFailure", exception.Message));
        }
        finally
        {
            _suppressSettingsEvents = false;
        }

        RememberDialogFocusIfOpening(SettingsDialogOverlay);
        ApplyUpdateStatusText();
        SettingsDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => Keyboard.Focus(LanguageComboBox));
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSettingsEvents ||
            LanguageComboBox.SelectedItem is not LanguageOption selectedLanguage ||
            string.Equals(selectedLanguage.Code, _settings.Language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousSettings = _settings;
        var proposedSettings = previousSettings with { Language = selectedLanguage.Code };
        try
        {
            _localization.Apply(selectedLanguage.Code);
            ApplyLanguageLayout();
            ApplyUpdateStatusText();
            _settingsService.Save(proposedSettings);
            _settings = proposedSettings;
            SettingsErrorText.Visibility = Visibility.Collapsed;
            var providerLoaded = ShowProvider(_selectedProvider);
            if (providerLoaded && !_providerNotices.ContainsKey(_selectedProvider))
            {
                SetStatus(T("Settings.Saved"), StatusTone.Success);
            }
        }
        catch (Exception exception)
        {
            _localization.Apply(previousSettings.Language);
            ApplyLanguageLayout();
            ApplyUpdateStatusText();
            _suppressSettingsEvents = true;
            LanguageComboBox.SelectedItem = _localization.SupportedLanguages.First(option =>
                string.Equals(option.Code, previousSettings.Language, StringComparison.OrdinalIgnoreCase));
            _suppressSettingsEvents = false;
            ShowSettingsError(T("Settings.LanguageFailure", exception.Message));
        }
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingsEvents)
        {
            return;
        }

        var previousSettings = _settings;
        var requestedValue = StartupToggle.IsChecked == true;
        if (requestedValue == previousSettings.StartWithWindows)
        {
            return;
        }

        try
        {
            _startupRegistration.SetEnabled(requestedValue);
            var proposedSettings = previousSettings with { StartWithWindows = requestedValue };
            _settingsService.Save(proposedSettings);

            _settings = proposedSettings;
            _startupStateKnown = true;
            SettingsErrorText.Visibility = Visibility.Collapsed;
            var providerLoaded = ShowProvider(_selectedProvider);
            if (providerLoaded && !_providerNotices.ContainsKey(_selectedProvider))
            {
                SetStatus(T("Settings.Saved"), StatusTone.Success);
            }
        }
        catch (Exception exception)
        {
            Exception displayedException = exception;
            bool? actualState = null;
            try
            {
                _startupRegistration.SetEnabled(previousSettings.StartWithWindows);
                actualState = previousSettings.StartWithWindows;
            }
            catch (Exception rollbackException)
            {
                displayedException = new AggregateException(exception, rollbackException);
                try
                {
                    actualState = _startupRegistration.IsEnabled();
                }
                catch (Exception inspectionException)
                {
                    displayedException = new AggregateException(
                        exception,
                        rollbackException,
                        inspectionException);
                }
            }

            _suppressSettingsEvents = true;
            if (actualState.HasValue)
            {
                _startupStateKnown = true;
                _settings = previousSettings with { StartWithWindows = actualState.Value };
                StartupToggle.IsChecked = actualState.Value;
                StartupToggle.IsEnabled = true;
            }
            else
            {
                _startupStateKnown = false;
                StartupToggle.IsChecked = null;
                StartupToggle.IsEnabled = false;
            }

            _suppressSettingsEvents = false;
            ShowSettingsError(T("Settings.StartupFailure", displayedException.Message));
        }
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _isCheckingForUpdates)
        {
            return;
        }

        _updateCheckCancellationSource?.Dispose();
        _updateCheckCancellationSource = new CancellationTokenSource();
        var cancellationToken = _updateCheckCancellationSource.Token;
        _isCheckingForUpdates = true;
        _updateCheckState = UpdateCheckViewState.Checking;
        _latestVersion = null;
        _latestReleasePage = null;
        _updateFailureDetail = null;
        CheckForUpdatesButton.IsEnabled = false;
        OpenUpdatePageButton.IsEnabled = false;
        OpenUpdatePageButton.Visibility = Visibility.Collapsed;
        ApplyUpdateStatusText();

        try
        {
            var result = await _updateCheckService.CheckAsync(_applicationVersion, cancellationToken);
            _latestVersion = result.LatestVersion;
            _latestReleasePage = result.ReleasePage;
            _updateCheckState = result.Availability == UpdateAvailability.UpdateAvailable
                ? UpdateCheckViewState.UpdateAvailable
                : UpdateCheckViewState.UpToDate;
            OpenUpdatePageButton.Visibility = result.Availability == UpdateAvailability.UpdateAvailable
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _updateCheckState = UpdateCheckViewState.Idle;
        }
        catch (Exception exception)
        {
            _updateCheckState = UpdateCheckViewState.Failed;
            _updateFailureDetail = exception.Message;
            SetStatus(T("Settings.Update.Failure"), StatusTone.Warning);
        }
        finally
        {
            _isCheckingForUpdates = false;
            CheckForUpdatesButton.IsEnabled = !_isBusy;
            OpenUpdatePageButton.IsEnabled = !_isBusy;
            ApplyUpdateStatusText();
        }
    }

    private void OpenUpdatePageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _isCheckingForUpdates || _latestReleasePage is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_latestReleasePage.AbsoluteUri)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowSettingsError(T("Settings.Update.OpenFailure", exception.Message));
        }
    }

    private void ApplyUpdateStatusText()
    {
        if (UpdateStatusText is null)
        {
            return;
        }

        var currentVersion = GitHubUpdateCheckService.FormatVersion(_applicationVersion);
        UpdateStatusText.Text = _updateCheckState switch
        {
            UpdateCheckViewState.Checking => T("Settings.Update.Checking", currentVersion),
            UpdateCheckViewState.UpToDate => T("Settings.Update.UpToDate", currentVersion),
            UpdateCheckViewState.UpdateAvailable when _latestVersion is not null => T(
                "Settings.Update.Available",
                currentVersion,
                GitHubUpdateCheckService.FormatVersion(_latestVersion)),
            UpdateCheckViewState.Failed => BuildFailureMessage(
                T("Settings.Update.Failure"),
                _updateFailureDetail),
            _ => T("Settings.Update.Current", currentVersion),
        };
    }

    private void CloseSettingsDialog_Click(object sender, RoutedEventArgs e)
    {
        var wasVisible = SettingsDialogOverlay.Visibility == Visibility.Visible;
        SettingsDialogOverlay.Visibility = Visibility.Collapsed;
        SettingsErrorText.Visibility = Visibility.Collapsed;
        if (wasVisible)
        {
            RestoreDialogFocus();
        }
    }

    private void RememberDialogFocus() => _dialogReturnFocus = Keyboard.FocusedElement;

    private void RememberDialogFocusIfOpening(UIElement overlay)
    {
        if (ShouldRememberDialogFocus(overlay.Visibility) && !IsAnyDialogVisible())
        {
            RememberDialogFocus();
        }

        // The overlays are visual siblings of the shell. Disabling only the shell
        // keeps the active dialog operable while preventing hidden controls from
        // being invoked through keyboard navigation or UI Automation.
        MainShell.IsEnabled = false;
    }

    internal static bool ShouldRememberDialogFocus(Visibility currentVisibility) =>
        currentVisibility != Visibility.Visible;

    private void RestoreDialogFocus()
    {
        if (IsAnyDialogVisible())
        {
            return;
        }

        MainShell.IsEnabled = true;
        var returnFocus = _dialogReturnFocus;
        _dialogReturnFocus = null;
        if (returnFocus is UIElement { IsVisible: true, IsEnabled: true } element)
        {
            Keyboard.Focus(element);
        }
    }

    private bool IsAnyDialogVisible() =>
        ProcessDialogOverlay.Visibility == Visibility.Visible ||
        SaveProfileDialogOverlay.Visibility == Visibility.Visible ||
        RenameProfileDialogOverlay.Visibility == Visibility.Visible ||
        DeleteProfileDialogOverlay.Visibility == Visibility.Visible ||
        SettingsDialogOverlay.Visibility == Visibility.Visible ||
        ChangedLoginDialogOverlay.Visibility == Visibility.Visible ||
        SwitchSuccessDialogOverlay.Visibility == Visibility.Visible;

    private void ShowSettingsError(string message)
    {
        SettingsErrorText.Text = message;
        SettingsErrorText.Visibility = Visibility.Visible;
        SetStatus(message, StatusTone.Error);
    }

    private void ApplyLanguageLayout()
    {
        FlowDirection = _localization.FlowDirection;
        Language = XmlLanguage.GetLanguage(_localization.CurrentLanguage);
        AutomationProperties.SetName(
            MaximizeButton,
            T(WindowState == WindowState.Maximized ? "Window.Restore" : "Window.Maximize"));
        ApplyUpdateStatusText();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The pointer can be released before WPF starts the drag operation.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        SystemCommands.MinimizeWindow(this);

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            SetStatus(T("Status.WaitBeforeClose"), StatusTone.Warning);
            return;
        }

        SystemCommands.CloseWindow(this);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _updateCheckCancellationSource?.Cancel();
        if (!_isBusy)
        {
            return;
        }

        e.Cancel = true;
        SetStatus(T("Status.WaitBeforeClose"), StatusTone.Warning);
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
        {
            var isMaximized = WindowState == WindowState.Maximized;
            MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
            RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(
                MaximizeButton,
                T(isMaximized ? "Window.Restore" : "Window.Maximize"));
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        // Window preview handlers run before the ComboBox can consume Escape.
        // Preserve the standard keyboard contract by closing its popup first.
        if (SettingsDialogOverlay.Visibility == Visibility.Visible &&
            LanguageComboBox.IsDropDownOpen)
        {
            LanguageComboBox.IsDropDownOpen = false;
            e.Handled = true;
            return;
        }

        if (SettingsDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CloseSettingsDialog_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (SwitchSuccessDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CloseSwitchSuccessDialog();
            e.Handled = true;
        }
        else if (ChangedLoginDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CancelChangedLoginDialog_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (SaveProfileDialogOverlay.Visibility == Visibility.Visible)
        {
            CloseSaveProfileDialog();
            e.Handled = true;
        }
        else if (RenameProfileDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CloseRenameProfileDialog();
            e.Handled = true;
        }
        else if (DeleteProfileDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CloseDeleteProfileDialog();
            e.Handled = true;
        }
        else if (ProcessDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CancelProcessDialog_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private string ProviderDisplayName(AgentProvider provider) => T(provider switch
    {
        AgentProvider.Codex => "Provider.Codex",
        AgentProvider.ClaudeCode => "Provider.ClaudeCode",
        AgentProvider.OpenCode => "Provider.OpenCode",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    });

    private static ProviderContext CreateProviderContext(
        string storageRoot,
        IAuthenticationAdapter adapter)
    {
        var scopeKey = AuthenticationLocationScope.CreateStorageKey(adapter);
        var vault = new AuthenticationProfileVault(
            System.IO.Path.Combine(storageRoot, "locations", scopeKey));
        var switchService = new AccountSwitchService(
            vault,
            mutexNamePrefix: $"CodingAgentAccountSwitcher.{scopeKey}");
        return new ProviderContext(adapter, vault, switchService);
    }

    private static string FormatAuthenticationPath(string path)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var prefix = userProfile + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? $@"%USERPROFILE%\{path[prefix.Length..]}"
            : path;
    }

    private string BuildFailureMessage(string prefix, string? errorMessage)
    {
        var localizedError = LocalizeOperationError(errorMessage);
        return string.IsNullOrWhiteSpace(localizedError)
            ? T("Failure.WithoutDetail", prefix)
            : T("Failure.WithDetail", prefix, localizedError);
    }

    private string? LocalizeOperationError(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return errorMessage;
        }

        foreach (var environmentVariable in new[]
                 {
                     "OPENCODE_AUTH_CONTENT",
                     "OPENCODE_CONFIG_CONTENT",
                     "OPENCODE_CONFIG_DIR",
                     "OPENCODE_CONFIG",
                     "XDG_CONFIG_HOME",
                     "XDG_DATA_HOME"
                 })
        {
            if (errorMessage.Contains(environmentVariable, StringComparison.Ordinal))
            {
                return T("Status.OpenCodeOverrideBlocked", environmentVariable);
            }
        }

        return errorMessage;
    }

    private string T(string key, params object?[] arguments) => _localization.Get(key, arguments);

    private enum StatusTone
    {
        Ready,
        Success,
        Warning,
        Error,
    }

    private enum UpdateCheckViewState
    {
        Idle,
        Checking,
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    private sealed record PendingAccountOperation(
        AgentProvider Provider,
        Guid ProfileId,
        string DisplayName,
        string? ConfirmationFingerprint)
    {
        public static PendingAccountOperation Switch(
            AgentProvider provider,
            Guid profileId,
            string displayName,
            string? confirmationFingerprint = null) =>
            new(provider, profileId, displayName, confirmationFingerprint);
    }

    private enum DwmWindowCornerPreference
    {
        Round = 2,
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref DwmWindowCornerPreference value,
        int valueSize);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref uint value,
        int valueSize);

    private sealed record ProviderContext(
        IAuthenticationAdapter Adapter,
        AuthenticationProfileVault Vault,
        AccountSwitchService SwitchService);

    private sealed record ProviderNotice(
        string ResourceKey,
        object?[] Arguments,
        StatusTone Tone,
        string? ErrorDetail = null);
}

public sealed class AccountCardViewModel : INotifyPropertyChanged
{
    private readonly DateTimeOffset _capturedAtUtc;
    private readonly LocalizationService _localization;
    private bool _isActive;

    public AccountCardViewModel(
        Guid profileId,
        string name,
        AgentProvider provider,
        DateTimeOffset capturedAtUtc,
        bool isActive,
        LocalizationService localization)
    {
        ProfileId = profileId;
        Name = name;
        Provider = provider;
        _capturedAtUtc = capturedAtUtc;
        _localization = localization;
        _isActive = isActive;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid ProfileId { get; }

    public string Name { get; }

    public string Initials => CreateInitials(Name);

    public string Subtitle => _localization.Get(Provider switch
    {
        AgentProvider.Codex => "Card.CodexProfile",
        AgentProvider.ClaudeCode => "Card.ClaudeProfile",
        AgentProvider.OpenCode => "Card.OpenCodeProfile",
        _ => throw new ArgumentOutOfRangeException(nameof(Provider), Provider, null),
    });

    public string LastSavedText => _localization.Get(
        "Card.SavedAt",
        _capturedAtUtc.ToLocalTime().ToString("g", _localization.Culture));

    public AgentProvider Provider { get; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(AutomationActionLabel));
        }
    }

    public string StatusText => _localization.Get(IsActive ? "Card.LastSelected" : "Card.Saved");

    public string ActionLabel => _localization.Get(IsActive ? "Card.Restore" : "Card.Switch");

    public string AutomationActionLabel => IsActive
        ? _localization.Get("Card.RestoreAutomation", Name)
        : _localization.Get("Card.SwitchAutomation", Name);

    public string RenameHint => _localization.Get("Card.RenameHint", Name);

    public string DeleteAutomationLabel => _localization.Get("Card.DeleteAutomation", Name);

    private static string CreateInitials(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 1)
        {
            return string.Concat(words.Take(2).Select(word => StringInfo.GetNextTextElement(word)))
                .ToUpper(CultureInfo.InvariantCulture);
        }

        var textElements = StringInfo.GetTextElementEnumerator(value);
        var initials = new List<string>(2);
        while (initials.Count < 2 && textElements.MoveNext())
        {
            initials.Add(textElements.GetTextElement());
        }

        return string.Concat(initials).ToUpper(CultureInfo.InvariantCulture);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record RunningProcessViewModel(string DisplayName, string Detail, string StateText);
