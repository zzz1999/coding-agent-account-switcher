using System.IO;
using System.Text.Json;

namespace CodingAgentAccountSwitcher.App;

public sealed record ApplicationSettings(
    string Language,
    bool StartWithWindows,
    bool UseDarkTheme = false)
{
    public static ApplicationSettings Default { get; } = new("en-US", false, false);
}

public sealed class ApplicationSettingsService
{
    private readonly string _settingsPath;

    public ApplicationSettingsService()
        : this(ResolveDefaultSettingsPath())
    {
    }

    internal ApplicationSettingsService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    private static string ResolveDefaultSettingsPath()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory could not be resolved.");
        }

        return Path.Combine(localApplicationData, "CodingAgentAccountSwitcher", "settings.json");
    }

    public ApplicationSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return ApplicationSettings.Default;
            }

            var settings = JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(_settingsPath));
            // Retired UI languages must not reset the user's theme or startup
            // preference. Loading only migrates in memory; saving stays explicit.
            if (settings is not null &&
                (string.Equals(settings.Language, "zh-CN", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(settings.Language, "zh-TW", StringComparison.OrdinalIgnoreCase)))
            {
                settings = settings with { Language = ApplicationSettings.Default.Language };
            }
            var normalizedLanguage = LocalizationService.NormalizeLanguage(settings?.Language);
            return settings is not null && normalizedLanguage is not null
                ? settings with { Language = normalizedLanguage }
                : ApplicationSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return ApplicationSettings.Default;
        }
    }

    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalizedLanguage = LocalizationService.NormalizeLanguage(settings.Language);
        if (normalizedLanguage is null)
        {
            throw new ArgumentException("Unsupported language.", nameof(settings));
        }

        settings = settings with { Language = normalizedLanguage };

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The settings directory could not be resolved.");
        Directory.CreateDirectory(directory);

        var transactionId = Guid.NewGuid().ToString("N");
        var temporaryPath = $"{_settingsPath}.{transactionId}.tmp";
        var backupPath = $"{_settingsPath}.{transactionId}.backup";
        var preserveBackup = false;
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_settingsPath))
            {
                File.Replace(temporaryPath, _settingsPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _settingsPath);
            }
        }
        catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(backupPath))
            {
                try
                {
                    File.Move(backupPath, _settingsPath, overwrite: true);
                }
                catch (Exception restoreException) when (
                    restoreException is IOException or UnauthorizedAccessException)
                {
                    preserveBackup = true;
                    throw new IOException(
                        "The application settings write failed and the previous settings could not be restored.",
                        new AggregateException(writeException, restoreException));
                }
            }

            throw;
        }
        finally
        {
            var ownedPaths = preserveBackup ? new[] { temporaryPath } : new[] { temporaryPath, backupPath };
            foreach (var ownedPath in ownedPaths)
            {
                try
                {
                    File.Delete(ownedPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A future cleanup may remove this uniquely named application-owned file.
                }
            }
        }
    }
}
