using System.Windows;
using System.Windows.Threading;

namespace CodingAgentAccountSwitcher.App;

public partial class App : Application
{
    private SingleInstanceCoordinator? _singleInstance;

    internal const string RemoveOwnedStartupRegistrationArgument =
        "--remove-owned-startup-registration";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (IsStartupRegistrationCleanupRequest(e.Args))
        {
            var exitCode = 0;
            try
            {
                new StartupRegistrationService().RemoveOwnedRegistration();
            }
            catch
            {
                // The uninstaller records the non-zero result while continuing
                // to preserve user data and any unowned registry value.
                exitCode = 1;
            }

            Shutdown(exitCode);
            return;
        }

        _singleInstance = SingleInstanceCoordinator.CreateForCurrentUser();
        if (!_singleInstance.IsPrimary)
        {
            _ = _singleInstance.NotifyPrimary();
            Shutdown(0);
            return;
        }
        _singleInstance.ActivationRequested += SingleInstance_ActivationRequested;

        var settingsService = new ApplicationSettingsService();
        var settings = settingsService.Load();
        var localization = new LocalizationService();
        localization.Apply(settings.Language);

        var window = new MainWindow(
            localization,
            settingsService,
            new StartupRegistrationService(),
            settings);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstance is not null)
        {
            _singleInstance.ActivationRequested -= SingleInstance_ActivationRequested;
            _singleInstance.Dispose();
            _singleInstance = null;
        }

        base.OnExit(e);
    }

    internal static bool IsStartupRegistrationCleanupRequest(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 &&
        string.Equals(
            arguments[0],
            RemoveOwnedStartupRegistrationArgument,
            StringComparison.Ordinal);

    private void SingleInstance_ActivationRequested(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            new Action(ActivatePrimaryWindow));
    }

    private void ActivatePrimaryWindow()
    {
        if (MainWindow is not Window window)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }
        if (window.WindowState == WindowState.Minimized)
        {
            SystemCommands.RestoreWindow(window);
        }

        var wasTopmost = window.Topmost;
        window.Topmost = true;
        _ = window.Activate();
        window.Topmost = wasTopmost;
        _ = window.Focus();
    }
}
