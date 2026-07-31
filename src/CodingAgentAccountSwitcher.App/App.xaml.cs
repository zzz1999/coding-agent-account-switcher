using System.Windows;

namespace CodingAgentAccountSwitcher.App;

public partial class App : Application
{
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

    internal static bool IsStartupRegistrationCleanupRequest(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 &&
        string.Equals(
            arguments[0],
            RemoveOwnedStartupRegistrationArgument,
            StringComparison.Ordinal);
}
