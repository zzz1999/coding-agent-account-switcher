using System.Text;
using System.Text.Json;
using System.Windows;
using CodingAgentAccountSwitcher.App;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class ApplicationPreferencesTests
{
    [Fact]
    public void SettingsRoundTripUsesOnlyTheRequestedFile()
    {
        using var temporary = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporary.Path, "preferences", "settings.json");
        var service = new ApplicationSettingsService(settingsPath);
        var first = new ApplicationSettings("ja-JP", true, true);
        var second = new ApplicationSettings("ar-SA", false);

        service.Save(first);
        Assert.Equal(first, service.Load());

        service.Save(second);

        Assert.Equal(second, service.Load());
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(settingsPath)!));
        Assert.Equal("settings.json", Path.GetFileName(Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Single()));
    }

    [Fact]
    public void LegacySettingsWithoutThemePreferenceDefaultToLightTheme()
    {
        using var temporary = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporary.Path, "settings.json");
        File.WriteAllText(settingsPath, "{\"Language\":\"de-DE\",\"StartWithWindows\":true}");
        var service = new ApplicationSettingsService(settingsPath);

        Assert.Equal(new ApplicationSettings("de-DE", true, false), service.Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Language\":\"unsupported\",\"StartWithWindows\":true}")]
    public void InvalidSettingsFallBackWithoutChangingTheFile(string contents)
    {
        using var temporary = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporary.Path, "settings.json");
        File.WriteAllText(settingsPath, contents);
        var service = new ApplicationSettingsService(settingsPath);

        Assert.Equal(ApplicationSettings.Default, service.Load());
        Assert.Equal(contents, File.ReadAllText(settingsPath));
    }

    [Fact]
    public void SupportedLanguageCodesAreNormalizedWhenLoaded()
    {
        using var temporary = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporary.Path, "settings.json");
        File.WriteAllText(settingsPath, "{\"Language\":\"JA-jp\",\"StartWithWindows\":true}");
        var service = new ApplicationSettingsService(settingsPath);

        Assert.Equal(new ApplicationSettings("ja-JP", true), service.Load());

        service.Save(new ApplicationSettings("PT-br", false));

        Assert.Equal(new ApplicationSettings("pt-BR", false), service.Load());
        Assert.Contains("\"Language\": \"pt-BR\"", File.ReadAllText(settingsPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zh-CN", false, false)]
    [InlineData("zh-CN", false, true)]
    [InlineData("zh-CN", true, false)]
    [InlineData("zh-CN", true, true)]
    [InlineData("zh-TW", false, false)]
    [InlineData("zh-TW", false, true)]
    [InlineData("zh-TW", true, false)]
    [InlineData("zh-TW", true, true)]
    [InlineData("ZH-cn", true, true)]
    [InlineData("zH-tW", true, true)]
    public void RetiredChineseLanguageFallsBackWithoutDiscardingOtherPreferences(
        string retiredLanguage,
        bool startWithWindows,
        bool useDarkTheme)
    {
        using var temporary = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporary.Path, "settings.json");
        var previousSettings = new ApplicationSettings(retiredLanguage, startWithWindows, useDarkTheme);
        var originalContents = JsonSerializer.Serialize(previousSettings);
        File.WriteAllText(settingsPath, originalContents);
        var service = new ApplicationSettingsService(settingsPath);
        var expected = previousSettings with { Language = "en-US" };

        var loaded = service.Load();

        Assert.Equal(expected, loaded);
        Assert.Equal(originalContents, File.ReadAllText(settingsPath));
        Assert.False(LocalizationService.IsSupported(retiredLanguage));
        Assert.Null(LocalizationService.NormalizeLanguage(retiredLanguage));

        // Retired codes may be migrated when reading old files, but must not be
        // accepted as a newly selected language or silently written back again.
        Assert.Throws<ArgumentException>(() => service.Save(previousSettings));
        Assert.Equal(originalContents, File.ReadAllText(settingsPath));

        service.Save(loaded);

        Assert.Equal(expected, service.Load());
        Assert.Equal(expected, JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(settingsPath)));
        Assert.Contains("\"Language\": \"en-US\"", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        Assert.Equal(new[] { settingsPath }, Directory.GetFiles(temporary.Path));
    }

    [Fact]
    public void LocalizationCatalogsAreCompleteAndArabicUsesRightToLeftFlow()
    {
        var localization = new LocalizationService();
        var expectedLanguages = new[]
        {
            "en-US", "es-ES", "fr-FR", "de-DE",
            "ja-JP", "ko-KR", "pt-BR", "ru-RU", "ar-SA", "hi-IN",
        };

        Assert.Equal(expectedLanguages, localization.SupportedLanguages.Select(language => language.Code));
        Assert.Equal(
            FlowDirection.RightToLeft,
            localization.SupportedLanguages.Single(language => language.Code == "ar-SA").FlowDirection);

        var catalogs = LocalizationCatalog.Create();
        Assert.Equal(expectedLanguages.Length, catalogs.Count);
        Assert.Equal(162, catalogs["en-US"].Count);
        foreach (var language in expectedLanguages)
        {
            Assert.Equal(catalogs["en-US"].Keys.Order(), catalogs[language].Keys.Order());
            Assert.All(catalogs[language].Values, value =>
            {
                Assert.DoesNotContain("Â·", value, StringComparison.Ordinal);
                Assert.DoesNotContain("â€", value, StringComparison.Ordinal);
                Assert.DoesNotContain("ï»¿", value, StringComparison.Ordinal);
                Assert.DoesNotContain("�", value, StringComparison.Ordinal);
            });

            foreach (var pair in catalogs["en-US"])
            {
                var expectedArguments = CompositeFormat.Parse(pair.Value).MinimumArgumentCount;
                var translatedArguments = CompositeFormat.Parse(catalogs[language][pair.Key])
                    .MinimumArgumentCount;
                Assert.Equal(expectedArguments, translatedArguments);
            }
        }
    }

    [Fact]
    public void AccountInitialsKeepCompleteUnicodeTextElements()
    {
        var card = new AccountCardViewModel(
            Guid.NewGuid(),
            "😀 Work",
            AgentProvider.Codex,
            DateTimeOffset.UtcNow,
            false,
            new LocalizationService());

        Assert.Equal("😀W", card.Initials);
    }

    [Fact]
    public void StartupCommandsQuoteApphostAndDotnetPaths()
    {
        var apphost = Path.GetFullPath(@"C:\Program Files\Account Switcher\Switcher.exe");
        var dotnet = Path.GetFullPath(@"C:\Program Files\dotnet\dotnet.exe");
        var entryAssembly = Path.GetFullPath(@"C:\Apps\Account Switcher\Switcher.dll");

        Assert.Equal($"\"{apphost}\"", StartupRegistrationService.BuildCommand(apphost, null));
        Assert.Equal(
            $"\"{dotnet}\" \"{entryAssembly}\"",
            StartupRegistrationService.BuildCommand(dotnet, entryAssembly));
    }

    [Fact]
    public void StartupCommandsRejectMissingInjectedOrOverlongPaths()
    {
        Assert.Throws<InvalidOperationException>(() => StartupRegistrationService.BuildCommand(null, null));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.BuildCommand(@"C:\Program Files\dotnet\dotnet.exe", null));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.BuildCommand("C:\\Unsafe\"Path\\app.exe", null));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.BuildCommand(@"C:\" + new string('a', 270) + ".exe", null));
    }

    [Fact]
    public void StartupOwnershipRequiresAnExactCommandMatch()
    {
        const string expected = "\"C:\\Apps\\Switcher.exe\"";
        var missing = StartupRegistrationValue.Missing;
        var owned = new StartupRegistrationValue(true, Microsoft.Win32.RegistryValueKind.String, expected);
        var empty = new StartupRegistrationValue(true, Microsoft.Win32.RegistryValueKind.String, string.Empty);
        var expandable = new StartupRegistrationValue(
            true,
            Microsoft.Win32.RegistryValueKind.ExpandString,
            expected);

        Assert.True(StartupRegistrationService.ShouldMutateRegistration(true, missing, expected));
        Assert.False(StartupRegistrationService.ShouldMutateRegistration(true, owned, expected));
        Assert.False(StartupRegistrationService.ShouldMutateRegistration(false, missing, expected));
        Assert.True(StartupRegistrationService.ShouldMutateRegistration(false, owned, expected));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.ShouldMutateRegistration(true, empty, expected));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.ShouldMutateRegistration(false, expandable, expected));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.ShouldMutateRegistration(
                false,
                owned with { Command = expected.ToUpperInvariant() },
                expected));
    }

    [Fact]
    public void StartupRegistrationClassificationRepairsOnlyStrictProductRelocations()
    {
        const string currentCommand =
            "\"C:\\Tools\\CAAS-v1.0.13-Portable-x64.exe\"";
        const string previousCommand = "\"D:\\Apps\\CodingAgentAccountSwitcher.exe\"";
        var relocated = new StartupRegistrationValue(
            true,
            Microsoft.Win32.RegistryValueKind.String,
            previousCommand);

        Assert.Equal(
            StartupRegistrationStatus.Relocated,
            StartupRegistrationService.ClassifyRegistration(relocated, currentCommand));
        Assert.False(StartupRegistrationService.IsRegistrationEnabled(relocated, currentCommand));
        Assert.Equal(
            StartupRegistrationStatus.Relocated,
            StartupRegistrationService.ClassifyRegistration(
                relocated with
                {
                    Command = "\"D:\\Apps\\CAAS-v1.0.12-Portable-x64.exe\""
                },
                currentCommand));

        string? inspectedRegisteredPath = null;
        Assert.True(StartupRegistrationService.ShouldMutateRegistration(
            true,
            relocated,
            currentCommand,
            registeredPath =>
            {
                inspectedRegisteredPath = registeredPath;
                return true;
            }));
        Assert.Equal(@"D:\Apps\CodingAgentAccountSwitcher.exe", inspectedRegisteredPath);
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.ShouldMutateRegistration(
                true,
                relocated,
                currentCommand,
                _ => false));
        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.ShouldMutateRegistration(false, relocated, currentCommand));
        Assert.Equal(
            StartupRegistrationStatus.Conflicting,
            StartupRegistrationService.ClassifyRegistration(
                relocated with { Command = previousCommand + " --unexpected" },
                currentCommand));
        Assert.Equal(
            StartupRegistrationStatus.Conflicting,
            StartupRegistrationService.ClassifyRegistration(
                relocated with { Kind = Microsoft.Win32.RegistryValueKind.ExpandString },
                currentCommand));
        Assert.Equal(
            StartupRegistrationStatus.Conflicting,
            StartupRegistrationService.ClassifyRegistration(
                relocated with { Command = "\"D:\\Apps\\DifferentProduct.exe\"" },
                currentCommand));
    }

    [Theory]
    [InlineData("CodingAgentAccountSwitcher.exe", true)]
    [InlineData("coding-agent-account-switcher-portable-win-x64.exe", true)]
    [InlineData("CAAS-v1.0.13-Portable-x64.exe", true)]
    [InlineData("caas-v2.4.0-portable-X64.EXE", true)]
    [InlineData("CAAS-v1.0-Portable-x64.exe", false)]
    [InlineData("CAAS-v1.0.13.0-Portable-x64.exe", false)]
    [InlineData("CAAS-v01.0.13-Portable-x64.exe", false)]
    [InlineData("CAAS-v0.9.1-Portable-x64.exe", false)]
    [InlineData("CAAS-v1.0.13-Setup-x64.exe", false)]
    [InlineData("CAAS-v1.0.13-Portable-arm64.exe", false)]
    [InlineData("CAAS-v1.0.13-Portable-x64.exe.bak", false)]
    [InlineData("CAAS-vnot-a-version-Portable-x64.exe", false)]
    public void StartupRegistrationRecognizesOnlyCanonicalVersionedPortableNames(
        string executableName,
        bool expectedKnownProduct)
    {
        const string registeredCommand = "\"C:\\Previous\\CodingAgentAccountSwitcher.exe\"";
        var registration = new StartupRegistrationValue(
            true,
            Microsoft.Win32.RegistryValueKind.String,
            registeredCommand);
        var currentCommand = $"\"D:\\Current\\{executableName}\"";

        Assert.Equal(
            expectedKnownProduct
                ? StartupRegistrationStatus.Relocated
                : StartupRegistrationStatus.Conflicting,
            StartupRegistrationService.ClassifyRegistration(registration, currentCommand));
    }

    [Fact]
    public void StartupRepairRequiresThePreviouslyRegisteredExecutableToBeMissing()
    {
        using var temporary = new TemporaryDirectory();
        var previousExecutablePath = Path.Combine(
            temporary.Path,
            "previous",
            "CodingAgentAccountSwitcher.exe");
        var currentExecutablePath = Path.Combine(
            temporary.Path,
            "current",
            "CAAS-v1.0.13-Portable-x64.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(previousExecutablePath)!);
        File.WriteAllBytes(previousExecutablePath, [0x4D, 0x5A]);

        var previousCommand = StartupRegistrationService.BuildCommand(previousExecutablePath, null);
        var currentCommand = StartupRegistrationService.BuildCommand(currentExecutablePath, null);
        var registration = new StartupRegistrationValue(
            true,
            Microsoft.Win32.RegistryValueKind.String,
            previousCommand);

        Assert.Throws<InvalidOperationException>(() =>
            StartupRegistrationService.ShouldMutateRegistration(
                true,
                registration,
                currentCommand));

        File.Delete(previousExecutablePath);

        Assert.True(StartupRegistrationService.ShouldMutateRegistration(
            true,
            registration,
            currentCommand));
    }

    [Theory]
    [InlineData(Visibility.Collapsed, true)]
    [InlineData(Visibility.Hidden, true)]
    [InlineData(Visibility.Visible, false)]
    public void DialogFocusIsCapturedOnlyWhenAnOverlayIsOpening(
        Visibility currentVisibility,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldRememberDialogFocus(currentVisibility));
    }

    [Theory]
    [InlineData(1, 0, 100, 0, true)]
    [InlineData(-1, 0, 100, 0, true)]
    [InlineData(0, 1, 0, 100, true)]
    [InlineData(0, 0, 100, 0, false)]
    [InlineData(1, 0, 0, 0, false)]
    public void TransientScrollIndicatorAppearsOnlyForAnActualScrollableOffsetChange(
        double verticalChange,
        double horizontalChange,
        double scrollableHeight,
        double scrollableWidth,
        bool expected)
    {
        Assert.Equal(
            expected,
            AutoHideScrollBarBehavior.ShouldRevealIndicator(
                verticalChange,
                horizontalChange,
                scrollableHeight,
                scrollableWidth));
    }

    [Theory]
    [InlineData(true, "--remove-owned-startup-registration")]
    [InlineData(false)]
    [InlineData(false, "--REMOVE-OWNED-STARTUP-REGISTRATION")]
    [InlineData(false, "--remove-owned-startup-registration", "unexpected")]
    public void UninstallCleanupModeRequiresOneExactArgument(bool expected, params string[] arguments)
    {
        Assert.Equal(
            expected,
            global::CodingAgentAccountSwitcher.App.App.IsStartupRegistrationCleanupRequest(arguments));
    }
}
