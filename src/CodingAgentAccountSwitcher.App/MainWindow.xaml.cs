using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CodingAgentAccountSwitcher.Core;

namespace CodingAgentAccountSwitcher.App;

public partial class MainWindow : Window
{
    private readonly IReadOnlyDictionary<AgentProvider, ProviderContext> _contexts;
    private readonly Dictionary<AgentProvider, (string Message, StatusTone Tone)> _providerNotices = [];

    private IReadOnlyList<AccountCardViewModel> _visibleAccounts = [];
    private AgentProvider _selectedProvider = AgentProvider.Codex;
    private PendingAccountOperation? _pendingOperation;
    private Guid? _confirmedReplacementProfileId;
    private bool _isBusy;
    private bool _isDarkTheme;

    public MainWindow()
    {
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
                    _providerNotices[adapter.Provider] = (
                        $"{adapter.DisplayName} recovery metadata is invalid: {exception.Message}",
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
                        _providerNotices[adapter.Provider] = (
                            $"Recovered an interrupted {adapter.DisplayName} switch.",
                            StatusTone.Success);
                        break;
                    case RecoveryStatus.BlockedByRunningProcesses:
                        _providerNotices[adapter.Provider] = (
                            $"Close {adapter.DisplayName}, then choose Save or Switch to retry interrupted-switch recovery.",
                            StatusTone.Warning);
                        break;
                    case RecoveryStatus.ProcessInspectionUnknown:
                        _providerNotices[adapter.Provider] = (
                            $"Windows could not safely check processes for {adapter.DisplayName} recovery. Try an operation again.",
                            StatusTone.Warning);
                        break;
                    case RecoveryStatus.LockUnavailable:
                        _providerNotices[adapter.Provider] = (
                            $"Another {adapter.DisplayName} account operation is in progress.",
                            StatusTone.Warning);
                        break;
                    case RecoveryStatus.ManualInterventionRequired:
                    case RecoveryStatus.Failed:
                        _providerNotices[adapter.Provider] = (
                            BuildFailureMessage($"{adapter.DisplayName} recovery requires attention", result.ErrorMessage),
                            StatusTone.Error);
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            _providerNotices[_selectedProvider] = ($"Startup recovery failed: {exception.Message}", StatusTone.Error);
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

    private void ShowProvider(AgentProvider provider)
    {
        _selectedProvider = provider;
        var adapter = _contexts[provider].Adapter;
        ProviderTitle.Text = $"{adapter.DisplayName} accounts";
        ProviderDescription.Text = $"Authentication file: {FormatAuthenticationPath(adapter.AuthenticationFilePath)}";

        if (ReloadProfiles(provider))
        {
            ApplyProviderStatus(provider);
        }
    }

    private void ApplyProviderStatus(AgentProvider provider)
    {
        if (_providerNotices.TryGetValue(provider, out var notice))
        {
            SetStatus(notice.Message, notice.Tone);
            return;
        }

        SetStatus($"Ready to switch {_contexts[provider].Adapter.DisplayName} accounts.", StatusTone.Ready);
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
                    activeProfile?.ProfileId == profile.ProfileId))
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
                    ? "Login detected, no saved profiles"
                    : "No login detected";
                EmptyStateMessage.Text = authenticationFileExists
                    ? "Close the provider, then save the current login. Sign in to your second account through the official flow and save that login too."
                    : "Sign in through the provider's official flow first. Close its apps, then return here to save the current login.";
            }

            return true;
        }
        catch (Exception exception)
        {
            _visibleAccounts = [];
            AccountItems.ItemsSource = _visibleAccounts;
            EmptyStatePanel.Visibility = Visibility.Visible;
            EmptyStateTitle.Text = "Saved profiles unavailable";
            EmptyStateMessage.Text =
                "The encrypted profile store could not be read. No authentication file was changed.";
            SetStatus($"Saved profiles could not be loaded: {exception.Message}", StatusTone.Error);
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

        var suggestedName = _visibleAccounts.All(account =>
            !string.Equals(account.Name, "Personal", StringComparison.OrdinalIgnoreCase))
            ? "Personal"
            : _visibleAccounts.All(account =>
                !string.Equals(account.Name, "Work", StringComparison.OrdinalIgnoreCase))
                ? "Work"
                : string.Empty;

        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = "Save encrypted snapshot";
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
            ProfileNameValidationText.Text =
                $"A profile named {existingProfile.Name} already exists. Click Replace saved snapshot to confirm.";
            ProfileNameValidationText.Visibility = Visibility.Visible;
            ConfirmSaveProfileButton.Content = "Replace saved snapshot";
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
            validationMessage = "Enter a profile label.";
        }
        else if (displayName.Length > 80 || displayName.Any(char.IsControl))
        {
            validationMessage = "Use 80 characters or fewer and no control characters.";
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
        ConfirmSaveProfileButton.Content = "Save encrypted snapshot";
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
    }

    private void CloseSaveProfileDialog()
    {
        SaveProfileDialogOverlay.Visibility = Visibility.Collapsed;
        ProfileNameValidationText.Visibility = Visibility.Collapsed;
        ProfileNameTextBox.Clear();
        _confirmedReplacementProfileId = null;
        ConfirmSaveProfileButton.Content = "Save encrypted snapshot";
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
            $"Checking processes and saving securely. Keep {ProviderDisplayName(provider)} closed until this finishes…",
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
            SetStatus($"The login could not be saved: {exception.Message}", StatusTone.Error);
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
                    var captureVerb = profileToReplace.HasValue ? "Updated" : "Saved";
                    SetStatus($"{captureVerb} {displayName} as an encrypted {ProviderDisplayName(provider)} profile.",
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
                    $"No {ProviderDisplayName(provider)} login file was found. Sign in normally, close the app, then try again.",
                    StatusTone.Error);
                break;
            case AccountOperationStatus.AuthenticationFileEmpty:
                CloseProcessDialog();
                SetStatus("The authentication file is empty or invalid, so nothing was saved.", StatusTone.Error);
                break;
            case AccountOperationStatus.ProfileNotFound:
                CloseProcessDialog();
                ReloadProfiles(provider);
                SetStatus("The profile to replace no longer exists. The list has been refreshed.", StatusTone.Error);
                break;
            case AccountOperationStatus.LockUnavailable:
                CloseProcessDialog();
                SetStatus("Another account operation is in progress. Try again in a moment.", StatusTone.Warning);
                break;
            case AccountOperationStatus.RecoveryRequired:
                CloseProcessDialog();
                var captureRecoveryMessage = BuildFailureMessage(
                    "An interrupted switch requires recovery before a login can be saved",
                    result.ErrorMessage);
                _providerNotices[provider] = (captureRecoveryMessage, StatusTone.Error);
                SetStatus(captureRecoveryMessage, StatusTone.Error);
                break;
            default:
                CloseProcessDialog();
                SetStatus(BuildFailureMessage("The login could not be saved", result.ErrorMessage), StatusTone.Error);
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
            $"Checking processes and switching securely. Keep {ProviderDisplayName(provider)} closed until this finishes…",
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
            SetStatus($"The account could not be switched: {exception.Message}", StatusTone.Error);
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
                    SetStatus($"{displayName} is now active for {ProviderDisplayName(provider)}.",
                        StatusTone.Success);
                }
                break;
            case AccountOperationStatus.AlreadyActive:
                CloseProcessDialog();
                SetStatus($"{displayName} is already active.", StatusTone.Ready);
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
                SetStatus("The current authentication file is missing. Sign in and save the current login first.",
                    StatusTone.Error);
                break;
            case AccountOperationStatus.ProfileNotFound:
                CloseProcessDialog();
                ReloadProfiles(provider);
                SetStatus("That encrypted profile no longer exists. The list has been refreshed.", StatusTone.Error);
                break;
            case AccountOperationStatus.LockUnavailable:
                CloseProcessDialog();
                SetStatus("Another account operation is in progress. Try again in a moment.", StatusTone.Warning);
                break;
            case AccountOperationStatus.RecoveryRequired:
                CloseProcessDialog();
                var switchRecoveryMessage = BuildFailureMessage(
                    "The switch requires recovery or a current-login capture",
                    result.ErrorMessage);
                _providerNotices[provider] = (switchRecoveryMessage, StatusTone.Error);
                SetStatus(switchRecoveryMessage, StatusTone.Error);
                break;
            default:
                CloseProcessDialog();
                var prefix = result.RolledBack
                    ? "The switch failed and the previous login was restored"
                    : "The account could not be switched";
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
            ?.Name ?? "the last selected profile";

        if (isSavedSnapshotRestore)
        {
            ChangedLoginDialogTitle.Text = "Restore the saved snapshot?";
            ChangedLoginDialogMessage.Text =
                $"The live authentication file no longer matches {sourceName}. Restoring the saved snapshot will replace the current unsaved login. Save the current login as a new profile first if you may need it.";
            ChangedLoginSafetyText.Text =
                "No authentication file has been changed. Restore only when you intentionally want to replace the exact live login shown by this warning.";
            ConfirmChangedLoginButton.Content = "Restore snapshot";
            AutomationProperties.SetName(ConfirmChangedLoginButton, "Restore saved authentication snapshot");
            SetStatus("Confirmation is required before the saved snapshot can replace the live login.",
                StatusTone.Warning);
        }
        else
        {
            ChangedLoginDialogTitle.Text = "Confirm the current login";
            ChangedLoginDialogMessage.Text =
                $"The live authentication file no longer matches the encrypted snapshot for {sourceName}. This may be a normal token refresh or a different account. Confirm only if the current login still belongs to {sourceName}; otherwise save it as a new profile first.";
            ChangedLoginSafetyText.Text =
                "No authentication file has been changed. Confirm only when the live login still belongs to the named profile.";
            ConfirmChangedLoginButton.Content = "Confirm & switch";
            AutomationProperties.SetName(ConfirmChangedLoginButton, "Confirm current login and switch");
            SetStatus("Confirmation is required before the last selected profile can be updated.",
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
        SetStatus("Switch cancelled. No authentication file was changed.", StatusTone.Ready);
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
        SetStatus("Operation cancelled. No authentication file was changed.", StatusTone.Ready);
    }

    private void ShowProcessDialog(ProcessInspectionResult inspection)
    {
        var rows = inspection.Processes
            .Select(process => new RunningProcessViewModel(
                process.ProcessName,
                $"{process.ProcessName}.exe · PID {process.ProcessId}",
                "RUNNING"))
            .Concat(inspection.Issues.Select(issue => new RunningProcessViewModel(
                issue.ProcessName == "*" ? "Process inspection" : issue.ProcessName,
                "Windows could not verify whether this process is closed.",
                "CHECK FAILED")))
            .ToArray();

        if (inspection.Status == ProcessInspectionStatus.Unknown)
        {
            ProcessDialogTitle.Text = "Process check could not complete";
            ProcessDialogMessage.Text =
                "The app could not safely confirm that every related process is closed. Close the provider and its extensions, then check again. No login file has been changed.";
        }
        else
        {
            ProcessDialogTitle.Text = "Close running apps before continuing";
            ProcessDialogMessage.Text =
                "The applications below may still be using the authentication file. Close them, then check again. No login file has been changed.";
        }

        BlockingProcessItems.ItemsSource = rows;
        ProcessDialogOverlay.Visibility = Visibility.Visible;
        SetStatus("Operation blocked until the process check is clear.", StatusTone.Warning);
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
        AccountItems.IsEnabled = !isBusy;
        RecheckButton.IsEnabled = !isBusy;
        ConfirmSaveProfileButton.IsEnabled = !isBusy;
        ConfirmChangedLoginButton.IsEnabled = !isBusy;
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
        SetStatus(_isDarkTheme ? "Dark appearance enabled." : "Light appearance enabled.", StatusTone.Ready);
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
            SetStatus("Wait for the credential operation to finish before closing the app.", StatusTone.Warning);
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
        SetStatus("Wait for the credential operation to finish before closing the app.", StatusTone.Warning);
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
                WindowState == WindowState.Maximized ? "Restore" : "Maximize");
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (ChangedLoginDialogOverlay.Visibility == Visibility.Visible && !_isBusy)
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

    private static string ProviderDisplayName(AgentProvider provider) =>
        provider == AgentProvider.Codex ? "Codex" : "Claude Code";

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

    private static string BuildFailureMessage(string prefix, string? errorMessage) =>
        string.IsNullOrWhiteSpace(errorMessage) ? $"{prefix}." : $"{prefix}: {errorMessage}";

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
}

public sealed class AccountCardViewModel : INotifyPropertyChanged
{
    private bool _isActive;

    public AccountCardViewModel(
        Guid profileId,
        string name,
        AgentProvider provider,
        DateTimeOffset capturedAtUtc,
        bool isActive)
    {
        ProfileId = profileId;
        Name = name;
        Provider = provider;
        LastSavedText = $"Saved {capturedAtUtc.ToLocalTime().ToString("MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture)}";
        _isActive = isActive;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid ProfileId { get; }

    public string Name { get; }

    public string Initials => CreateInitials(Name);

    public string Subtitle => Provider == AgentProvider.Codex ? "Codex profile" : "Claude Code profile";

    public string LastSavedText { get; }

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

    public string StatusText => IsActive ? "LAST SELECTED" : "SAVED";

    public string ActionLabel => IsActive ? "Restore snapshot" : "Switch";

    public string AutomationActionLabel => IsActive
        ? $"Restore the saved snapshot for {Name}"
        : $"Switch to {Name}";

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
