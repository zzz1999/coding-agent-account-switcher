using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using CodingAgentAccountSwitcher.Core;

namespace CodingAgentAccountSwitcher.App;

public partial class MainWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly ApplicationSettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistration;
    private readonly IReadOnlyDictionary<AgentProvider, ProviderContext> _contexts;
    private readonly Dictionary<AgentProvider, ProviderNotice> _providerNotices = [];

    private IReadOnlyList<AccountCardViewModel> _visibleAccounts = [];
    private AgentProvider _selectedProvider = AgentProvider.Codex;
    private PendingAccountOperation? _pendingOperation;
    private Guid? _confirmedReplacementProfileId;
    private bool _isBusy;
    private bool _isDarkTheme;
    private bool _suppressSettingsEvents;
    private bool _startupStateKnown = true;
    private ApplicationSettings _settings;

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

        InitializeComponent();

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
        ShowProvider(AgentProvider.Codex);
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

        ShowProvider(ReferenceEquals(sender, ClaudeSegment)
            ? AgentProvider.ClaudeCode
            : AgentProvider.Codex);
    }

    private bool ShowProvider(AgentProvider provider)
    {
        _selectedProvider = provider;
        var adapter = _contexts[provider].Adapter;
        ProviderTitle.Text = T("Provider.Accounts", ProviderDisplayName(provider));
        ProviderDescription.Text = T(
            "Provider.AuthenticationFile",
            FormatAuthenticationPath(adapter.AuthenticationFilePath));

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
            _visibleAccounts = vault.ListProfiles(provider)
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
                var authenticationFileExists = System.IO.File.Exists(
                    _contexts[provider].Adapter.AuthenticationFilePath);
                EmptyStateTitle.Text = authenticationFileExists
                    ? T("Empty.LoginDetected.Title")
                    : T("Empty.NoLogin.Title");
                EmptyStateMessage.Text = authenticationFileExists
                    ? T("Empty.LoginDetected.Message")
                    : T("Empty.NoLogin.Message");
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
            SetStatus(T("Status.ProfilesLoadFailed", exception.Message), StatusTone.Error);
            return false;
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

    private void SaveCurrentLogin_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var personalLabel = T("Save.SuggestedPersonal");
        var workLabel = T("Save.SuggestedWork");
        var suggestedName = _visibleAccounts.All(account =>
            !string.Equals(account.Name, personalLabel, StringComparison.CurrentCultureIgnoreCase))
            ? personalLabel
            : _visibleAccounts.All(account =>
                !string.Equals(account.Name, workLabel, StringComparison.CurrentCultureIgnoreCase))
                ? workLabel
                : string.Empty;

        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = T("Save.Snapshot");
        ProfileNameTextBox.Text = suggestedName;
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
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
        if (!ValidateProfileName(displayName))
        {
            return;
        }

        var existingProfile = _visibleAccounts.FirstOrDefault(account =>
            string.Equals(account.Name, displayName, StringComparison.CurrentCultureIgnoreCase));
        if (existingProfile is not null && _confirmedReplacementProfileId != existingProfile.ProfileId)
        {
            _confirmedReplacementProfileId = existingProfile.ProfileId;
            ProfileNameValidationText.Text = T("Save.ReplaceConfirm", existingProfile.Name);
            ProfileNameValidationText.Visibility = Visibility.Visible;
            ConfirmSaveProfileButton.Content = T("Save.Replace");
            return;
        }

        SaveProfileDialogOverlay.Visibility = Visibility.Collapsed;
        await AttemptCaptureAsync(
            _selectedProvider,
            existingProfile?.Name ?? displayName,
            existingProfile?.ProfileId);
    }

    private bool ValidateProfileName(string displayName)
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
            ProfileNameValidationText.Visibility = Visibility.Collapsed;
            return true;
        }

        ProfileNameValidationText.Text = validationMessage;
        ProfileNameValidationText.Visibility = Visibility.Visible;
        ProfileNameTextBox.Focus();
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
        SaveProfileDialogOverlay.Visibility = Visibility.Collapsed;
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
        ProfileNameTextBox.Clear();
        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = T("Save.Snapshot");
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
            T("Status.CheckingSave", ProviderDisplayName(provider)),
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
            SetStatus(T("Status.SaveException", exception.Message), StatusTone.Error);
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
            case AccountOperationStatus.BlockedByRunningProcesses:
            case AccountOperationStatus.ProcessInspectionUnknown:
                _pendingOperation = PendingAccountOperation.Capture(provider, displayName, profileToReplace);
                ShowProcessDialog(result.ProcessInspection);
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
            SetStatus(T("Status.SwitchException", exception.Message), StatusTone.Error);
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
                if (switchProfilesLoaded)
                {
                    SetStatus(T("Status.NowActive", displayName, ProviderDisplayName(provider)),
                        StatusTone.Success);
                }
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

        if (pendingOperation.Kind == PendingOperationKind.Capture)
        {
            await AttemptCaptureAsync(
                pendingOperation.Provider,
                pendingOperation.DisplayName,
                pendingOperation.ProfileId == Guid.Empty ? null : pendingOperation.ProfileId);
        }
        else
        {
            await AttemptSwitchAsync(
                pendingOperation.Provider,
                pendingOperation.ProfileId,
                pendingOperation.DisplayName,
                pendingOperation.ConfirmationFingerprint);
        }
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

        ChangedLoginDialogOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => Keyboard.Focus(ConfirmChangedLoginButton));
    }

    private async void ConfirmChangedLoginSwitch_Click(object sender, RoutedEventArgs e)
    {
        var pendingOperation = _pendingOperation;
        if (_isBusy || pendingOperation is null || pendingOperation.Kind != PendingOperationKind.Switch)
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
        ChangedLoginDialogOverlay.Visibility = Visibility.Collapsed;
        ChangedLoginDialogMessage.Text = string.Empty;
        ChangedLoginSafetyText.Text = string.Empty;
        if (clearPendingOperation)
        {
            _pendingOperation = null;
        }
    }

    private void CancelProcessDialog_Click(object sender, RoutedEventArgs e)
    {
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
        ProcessDialogOverlay.Visibility = Visibility.Visible;
        SetStatus(T("Status.ProcessBlocked"), StatusTone.Warning);
        Dispatcher.BeginInvoke(() => Keyboard.Focus(RecheckButton));
    }

    private void CloseProcessDialog()
    {
        ProcessDialogOverlay.Visibility = Visibility.Collapsed;
        BlockingProcessItems.ItemsSource = null;
        _pendingOperation = null;
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        ProviderSelector.IsEnabled = !isBusy;
        SaveCurrentLoginButton.IsEnabled = !isBusy;
        SettingsButton.IsEnabled = !isBusy;
        AccountItems.IsEnabled = !isBusy;
        RecheckButton.IsEnabled = !isBusy;
        ConfirmSaveProfileButton.IsEnabled = !isBusy;
        ConfirmChangedLoginButton.IsEnabled = !isBusy;
        LanguageComboBox.IsEnabled = !isBusy;
        StartupToggle.IsEnabled = !isBusy && _startupStateKnown;
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
        _isDarkTheme = !_isDarkTheme;
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

        ThemeButton.Content = _isDarkTheme ? "☼" : "☾";
        SetStatus(T(_isDarkTheme ? "Status.DarkTheme" : "Status.LightTheme"), StatusTone.Ready);
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

    private void CloseSettingsDialog_Click(object sender, RoutedEventArgs e)
    {
        SettingsDialogOverlay.Visibility = Visibility.Collapsed;
        SettingsErrorText.Visibility = Visibility.Collapsed;
    }

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
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
            AutomationProperties.SetName(
                MaximizeButton,
                T(WindowState == WindowState.Maximized ? "Window.Restore" : "Window.Maximize"));
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (SettingsDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CloseSettingsDialog_Click(this, new RoutedEventArgs());
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
        else if (ProcessDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
        {
            CancelProcessDialog_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private string ProviderDisplayName(AgentProvider provider) =>
        T(provider == AgentProvider.Codex ? "Provider.Codex" : "Provider.ClaudeCode");

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

    private string BuildFailureMessage(string prefix, string? errorMessage) =>
        string.IsNullOrWhiteSpace(errorMessage)
            ? T("Failure.WithoutDetail", prefix)
            : T("Failure.WithDetail", prefix, errorMessage);

    private string T(string key, params object?[] arguments) => _localization.Get(key, arguments);

    private enum StatusTone
    {
        Ready,
        Success,
        Warning,
        Error,
    }

    private enum PendingOperationKind
    {
        Capture,
        Switch,
    }

    private sealed record PendingAccountOperation(
        PendingOperationKind Kind,
        AgentProvider Provider,
        Guid ProfileId,
        string DisplayName,
        string? ConfirmationFingerprint)
    {
        public static PendingAccountOperation Capture(
            AgentProvider provider,
            string displayName,
            Guid? profileToReplace) =>
            new(PendingOperationKind.Capture, provider, profileToReplace ?? Guid.Empty, displayName, null);

        public static PendingAccountOperation Switch(
            AgentProvider provider,
            Guid profileId,
            string displayName,
            string? confirmationFingerprint = null) =>
            new(PendingOperationKind.Switch, provider, profileId, displayName, confirmationFingerprint);
    }

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

    public string Subtitle => _localization.Get(
        Provider == AgentProvider.Codex ? "Card.CodexProfile" : "Card.ClaudeProfile");

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

    private static string CreateInitials(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 1)
        {
            return string.Concat(words.Take(2).Select(word => word[0])).ToUpper(CultureInfo.InvariantCulture);
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
