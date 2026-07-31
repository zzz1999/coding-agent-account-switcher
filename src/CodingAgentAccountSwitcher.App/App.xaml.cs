using System.Windows;

namespace CodingAgentAccountSwitcher.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
}
