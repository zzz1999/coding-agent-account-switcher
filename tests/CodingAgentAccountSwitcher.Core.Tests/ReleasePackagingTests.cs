using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CodingAgentAccountSwitcher.App;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class ReleasePackagingTests
{
    [Fact]
    public void InstallerCommandSupportsInnoSetup6AndUsesTheReleaseVersion()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "latest-release.yml");
        var compilerCommand = Assert.Single(
            workflow.Split('\n'),
            line => line.TrimStart().StartsWith("& $compilerPath ", StringComparison.Ordinal));

        Assert.Equal(
            "& $compilerPath \"/DAppVersion=$env:APP_VERSION\" .\\installer\\CodingAgentAccountSwitcher.iss",
            compilerCommand.Trim());
        Assert.Contains("Write-Host \"Inno Setup compiler: $compilerPath\"", workflow);
        Assert.Contains(
            "#pragma message \"Inno Setup compiler version: \"",
            ReadRepositoryFile("installer", "CodingAgentAccountSwitcher.iss"));
    }

    [Fact]
    public void InstallerAndReleaseAssetNamesShareTheSameVersionContract()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "latest-release.yml");
        var installer = ReadRepositoryFile("installer", "CodingAgentAccountSwitcher.iss");

        Assert.Contains("APP_VERSION: 1.0.${{ github.run_number }}", workflow);
        Assert.Contains("\"-p:Version=$env:APP_VERSION\"", workflow);
        Assert.Contains("#ifndef AppVersion", installer);
        Assert.Contains("AppVersion={#AppVersion}", installer);
        Assert.Contains("OutputBaseFilename=CAAS-v{#AppVersion}-Setup-x64", installer);

        var assetAssignments = Regex.Matches(
            workflow,
            @"(?m)^\s*\$(?:installerAssetName|portableAssetName|assetName) = ""([^""]+)""\s*$");
        Assert.Equal(5, assetAssignments.Count);
        Assert.All(assetAssignments.Cast<Match>(), match =>
            Assert.Contains(match.Groups[1].Value, new[]
            {
                "CAAS-v$($env:APP_VERSION)-Setup-x64.exe",
                "CAAS-v$($env:APP_VERSION)-Portable-x64.exe",
            }));
    }

    [Fact]
    public void InteractiveInstallerAlwaysAsksForLanguageBeforeTheWizard()
    {
        var installer = ParseMessageFile(ReadRepositoryFile("installer", "CodingAgentAccountSwitcher.iss"));

        Assert.Equal("yes", installer["Setup.ShowLanguageDialog"]);
        Assert.Equal("no", installer["Setup.UsePreviousLanguage"]);
        Assert.Equal("uilanguage", installer["Setup.LanguageDetectionMethod"]);
        // The native dialog obeys /VERYSILENT; do not replace it with a custom [Code] prompt.
        Assert.DoesNotContain("[Code]", ReadRepositoryFile("installer", "CodingAgentAccountSwitcher.iss"));
        Assert.Contains("'/VERYSILENT'", ReadRepositoryFile(".github", "workflows", "latest-release.yml"));
    }

    [Fact]
    public void InstallerLanguagesMatchEveryApplicationLanguageAndUseVendoredFiles()
    {
        var entries = ReadInstallerLanguages();
        var supported = new LocalizationService().SupportedLanguages;
        Assert.Equal(10, entries.Length);
        Assert.DoesNotContain(entries, entry => entry.Code is "zh-CN" or "zh-TW");
        Assert.Equal(supported.Select(language => language.Code), entries.Select(entry => entry.Code));

        foreach (var (entry, language) in entries.Zip(supported))
        {
            Assert.Matches(@"^Languages\\[A-Za-z]+\.isl$", entry.MessagesFile);
            var messages = ReadInstallerMessages(entry.MessagesFile);
            Assert.Equal(language.NativeName, messages["LangOptions.LanguageName"]);
            Assert.Equal(
                CultureInfo.GetCultureInfo(language.Code).LCID,
                int.Parse(messages["LangOptions.LanguageID"].TrimStart('$'), NumberStyles.HexNumber));
            Assert.Equal(
                language.FlowDirection == FlowDirection.RightToLeft,
                messages.GetValueOrDefault("LangOptions.RightToLeft") == "yes");
        }
    }

    [Fact]
    public void EveryInstallerTranslationHasCompleteMessagesAndPreservesPlaceholders()
    {
        var english = ReadInstallerMessages(@"Languages\English.isl");
        var expectedKeys = english.Keys.Where(IsMessageKey).Order().ToArray();
        Assert.Equal(293, expectedKeys.Length); // Inno Setup 6.7.1: 281 standard + 12 custom messages.

        foreach (var entry in ReadInstallerLanguages())
        {
            var messages = ReadInstallerMessages(entry.MessagesFile);
            Assert.Equal(expectedKeys, messages.Keys.Where(IsMessageKey).Order());
            foreach (var key in expectedKeys)
            {
                Assert.Equal(Placeholders(english[key]), Placeholders(messages[key]));
                if (!string.IsNullOrWhiteSpace(english[key]))
                {
                    Assert.False(string.IsNullOrWhiteSpace(messages[key]), $"{entry.Code}: {key} is empty.");
                }
            }
        }
    }

    private static bool IsMessageKey(string key) =>
        key.StartsWith("Messages.", StringComparison.Ordinal) ||
        key.StartsWith("CustomMessages.", StringComparison.Ordinal);

    private static string[] Placeholders(string message) =>
        Regex.Matches(message, @"%[1-9][0-9]*|\[(?:name(?:/ver)?|gb|mb)\]")
            .Select(match => match.Value).Order().ToArray();

    private static (string Code, string MessagesFile)[] ReadInstallerLanguages()
    {
        var installer = ReadRepositoryFile("installer", "CodingAgentAccountSwitcher.iss");
        var section = Regex.Match(installer, @"(?ms)^\[Languages\]\s*\r?\n(.*?)(?=^\[|\z)").Groups[1].Value;
        return Regex.Matches(section, @"(?m)^Name: ""([^""]+)""; MessagesFile: ""([^""]+)""\s*$")
            .Select(match => (match.Groups[1].Value.Replace('_', '-'), match.Groups[2].Value)).ToArray();
    }

    private static Dictionary<string, string> ReadInstallerMessages(string relativePath) =>
        ParseMessageFile(ReadRepositoryFile("installer", relativePath));

    private static Dictionary<string, string> ParseMessageFile(string contents)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = string.Empty;
        foreach (var rawLine in contents.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                continue;
            }
            var separator = line.IndexOf('=');
            if (separator > 0 && section is "Setup" or "LangOptions" or "Messages" or "CustomMessages")
            {
                var key = $"{section}.{line[..separator]}";
                Assert.True(values.TryAdd(key, line[(separator + 1)..]), $"Duplicate installer key: {key}");
            }
        }
        return values;
    }

    private static string ReadRepositoryFile(params string[] components)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodingAgentAccountSwitcher.sln")))
            {
                return File.ReadAllText(
                    Path.Combine([directory.FullName, .. components]),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The release packaging tests require a repository checkout.");
    }
}
