using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace CodingAgentAccountSwitcher.App;

public sealed record LanguageOption(string Code, string NativeName, FlowDirection FlowDirection)
{
    public override string ToString() => NativeName;
}

public sealed class LocalizationService
{
    private static readonly IReadOnlyList<LanguageOption> Languages =
    [
        new("en-US", "English (United States)", FlowDirection.LeftToRight),
        new("zh-CN", "简体中文", FlowDirection.LeftToRight),
        new("zh-TW", "繁體中文", FlowDirection.LeftToRight),
        new("es-ES", "Español", FlowDirection.LeftToRight),
        new("fr-FR", "Français", FlowDirection.LeftToRight),
        new("de-DE", "Deutsch", FlowDirection.LeftToRight),
        new("ja-JP", "日本語", FlowDirection.LeftToRight),
        new("ko-KR", "한국어", FlowDirection.LeftToRight),
        new("pt-BR", "Português (Brasil)", FlowDirection.LeftToRight),
        new("ru-RU", "Русский", FlowDirection.LeftToRight),
        new("ar-SA", "العربية", FlowDirection.RightToLeft),
        new("hi-IN", "हिन्दी", FlowDirection.LeftToRight),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs =
        LocalizationCatalog.Create();

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<LanguageOption> SupportedLanguages => Languages;

    public string CurrentLanguage { get; private set; } = "en-US";

    public CultureInfo Culture => CultureInfo.GetCultureInfo(CurrentLanguage);

    public FlowDirection FlowDirection => Languages.First(language => language.Code == CurrentLanguage).FlowDirection;

    public static bool IsSupported(string? language) => NormalizeLanguage(language) is not null;

    public static string? NormalizeLanguage(string? language) =>
        Languages.FirstOrDefault(option =>
            string.Equals(option.Code, language, StringComparison.OrdinalIgnoreCase))?.Code;

    public void Apply(string language)
    {
        var option = Languages.FirstOrDefault(candidate =>
                         string.Equals(candidate.Code, language, StringComparison.OrdinalIgnoreCase))
                     ?? Languages[0];
        var catalog = Catalogs[option.Code];
        var culture = CultureInfo.GetCultureInfo(option.Code);

        CurrentLanguage = option.Code;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var resources = Application.Current.Resources;
        foreach (var pair in catalog)
        {
            resources[$"L.{pair.Key}"] = pair.Value;
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key, params object?[] arguments)
    {
        var catalog = Catalogs[CurrentLanguage];
        var value = catalog.TryGetValue(key, out var translated)
            ? translated
            : Catalogs["en-US"].TryGetValue(key, out var english)
                ? english
                : key;
        return arguments.Length == 0 ? value : string.Format(Culture, value, arguments);
    }
}

internal static partial class LocalizationCatalog
{
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Create()
    {
        var english = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["App.Title"] = "Coding Agent Account Switcher",
            ["Window.Theme"] = "Switch color theme",
            ["Window.Settings"] = "Settings",
            ["Window.Minimize"] = "Minimize",
            ["Window.Maximize"] = "Maximize",
            ["Window.Restore"] = "Restore",
            ["Window.Close"] = "Close",
            ["Hero.Title"] = "Switch accounts, keep your setup.",
            ["Hero.Description"] = "Choose a saved login snapshot. Settings, MCP servers, skills, plugins, and project history stay exactly where they are.",
            ["Provider.Codex"] = "Codex",
            ["Provider.ClaudeCode"] = "Claude Code",
            ["Provider.Accounts"] = "{0} accounts",
            ["Provider.AuthenticationFile"] = "Authentication file: {0}",
            ["Action.SaveCurrentLogin"] = "Save current login",
            ["Badge.LocalOnly"] = "LOCAL ONLY",
            ["Empty.LoginDetected.Title"] = "Login detected, no saved profiles",
            ["Empty.LoginDetected.Message"] = "Close the provider, then save the current login. Sign in to your second account through the official flow and save that login too.",
            ["Empty.NoLogin.Title"] = "No login detected",
            ["Empty.NoLogin.Message"] = "Sign in through the provider's official flow first. Close its apps, then return here to save the current login.",
            ["Empty.Unavailable.Title"] = "Saved profiles unavailable",
            ["Empty.Unavailable.Message"] = "The encrypted profile store could not be read. No authentication file was changed.",
            ["Security.Title"] = "Only authentication changes",
            ["Security.Message"] = "Credential snapshots remain encrypted for the current Windows user. The app never previews token contents.",
            ["Status.Initial"] = "Ready. No authentication file is being changed.",
            ["Status.NoTelemetry"] = "No telemetry",
            ["Card.EncryptedSnapshot"] = "ENCRYPTED LOCAL SNAPSHOT",
            ["Card.CodexProfile"] = "Codex profile",
            ["Card.ClaudeProfile"] = "Claude Code profile",
            ["Card.SavedAt"] = "Saved {0}",
            ["Card.LastSelected"] = "LAST SELECTED",
            ["Card.Saved"] = "SAVED",
            ["Card.Restore"] = "Restore snapshot",
            ["Card.Switch"] = "Switch",
            ["Card.RestoreAutomation"] = "Restore the saved snapshot for {0}",
            ["Card.SwitchAutomation"] = "Switch to {0}",
            ["Common.Cancel"] = "Cancel",
            ["Common.Close"] = "Close",
            ["Process.Automation"] = "Process check required",
            ["Process.Close.Title"] = "Close running apps before continuing",
            ["Process.Close.Message"] = "The applications below may still be using the authentication file. Close them, then check again. No login file has been changed.",
            ["Process.Unknown.Title"] = "Process check could not complete",
            ["Process.Unknown.Message"] = "The app could not safely confirm that every related process is closed. Close the provider and its extensions, then check again. No login file has been changed.",
            ["Process.CheckAgain"] = "Check again",
            ["Process.Running"] = "RUNNING",
            ["Process.CheckFailed"] = "CHECK FAILED",
            ["Process.Inspection"] = "Process inspection",
            ["Process.Unverified"] = "Windows could not verify whether this process is closed.",
            ["Process.Detail"] = "{0}.exe · PID {1}",
            ["Save.Automation"] = "Save current login",
            ["Save.Title"] = "Save the current login",
            ["Save.Description"] = "Choose a local label such as Personal or Work. The app stores the authentication file as an opaque, DPAPI-encrypted snapshot and never reads the account identity inside it.",
            ["Save.ProfileLabel"] = "PROFILE LABEL",
            ["Save.ProfileLabelAutomation"] = "Profile label",
            ["Save.EnterLabel"] = "Enter a profile label.",
            ["Save.InvalidLabel"] = "Use 80 characters or fewer and no control characters.",
            ["Save.SuggestedPersonal"] = "Personal",
            ["Save.SuggestedWork"] = "Work",
            ["Save.Snapshot"] = "Save encrypted snapshot",
            ["Save.Replace"] = "Replace saved snapshot",
            ["Save.ReplaceConfirm"] = "A profile named {0} already exists. Choose Replace saved snapshot to confirm.",
            ["Changed.Automation"] = "Current login confirmation required",
            ["Changed.Confirm.Title"] = "Confirm the current login",
            ["Changed.Confirm.Message"] = "The live authentication file no longer matches the encrypted snapshot for {0}. This may be a normal token refresh or a different account. Confirm only if the current login still belongs to {0}; otherwise save it as a new profile first.",
            ["Changed.Confirm.Safety"] = "No authentication file has been changed. Confirm only when the live login still belongs to the named profile.",
            ["Changed.Confirm.Action"] = "Confirm & switch",
            ["Changed.Confirm.Automation"] = "Confirm current login and switch",
            ["Changed.Restore.Title"] = "Restore the saved snapshot?",
            ["Changed.Restore.Message"] = "The live authentication file no longer matches {0}. Restoring the saved snapshot will replace the current unsaved login. Save the current login as a new profile first if you may need it.",
            ["Changed.Restore.Safety"] = "No authentication file has been changed. Restore only when you intentionally want to replace the exact live login shown by this warning.",
            ["Changed.Restore.Action"] = "Restore snapshot",
            ["Changed.Restore.Automation"] = "Restore saved authentication snapshot",
            ["Changed.LastSelected"] = "the last selected profile",
            ["Changed.SaveAsNew"] = "Save as new…",
            ["Settings.Automation"] = "Application settings",
            ["Settings.Title"] = "Settings",
            ["Settings.Description"] = "Customize this app. These preferences never modify Codex or Claude Code settings.",
            ["Settings.Language"] = "LANGUAGE",
            ["Settings.Language.Description"] = "Changes apply immediately throughout the app.",
            ["Settings.Startup"] = "START WITH WINDOWS",
            ["Settings.Startup.Description"] = "Launch for the current Windows user after sign-in. Administrator access is not required.",
            ["Settings.Startup.Toggle"] = "Start Coding Agent Account Switcher with Windows",
            ["Settings.Saved"] = "Settings saved.",
            ["Settings.LanguageFailure"] = "The language setting could not be saved: {0}",
            ["Settings.StartupFailure"] = "The Windows startup setting could not be changed: {0}",
            ["Recovery.MetadataInvalid"] = "{0} recovery metadata is invalid: {1}",
            ["Recovery.Recovered"] = "Recovered an interrupted {0} switch.",
            ["Recovery.CloseThenRetry"] = "Close {0}, then choose Save or Switch to retry interrupted-switch recovery.",
            ["Recovery.ProcessUnknown"] = "Windows could not safely check processes for {0} recovery. Try an operation again.",
            ["Recovery.InProgress"] = "Another {0} account operation is in progress.",
            ["Recovery.Attention"] = "{0} recovery requires attention",
            ["Recovery.StartupFailed"] = "Startup recovery failed: {0}",
            ["Status.ReadyProvider"] = "Ready to switch {0} accounts.",
            ["Status.ProfilesLoadFailed"] = "Saved profiles could not be loaded: {0}",
            ["Status.CheckingSave"] = "Checking processes and saving securely. Keep {0} closed until this finishes…",
            ["Status.SaveException"] = "The login could not be saved: {0}",
            ["Status.ProfileCaptured"] = "{0} {1} as an encrypted {2} profile.",
            ["Status.SavedVerb"] = "Saved",
            ["Status.UpdatedVerb"] = "Updated",
            ["Status.LoginMissing"] = "No {0} login file was found. Sign in normally, close the app, then try again.",
            ["Status.AuthenticationEmpty"] = "The authentication file is empty or invalid, so nothing was saved.",
            ["Status.ProfileReplaceMissing"] = "The profile to replace no longer exists. The list has been refreshed.",
            ["Status.OperationInProgress"] = "Another account operation is in progress. Try again in a moment.",
            ["Status.CaptureRecovery"] = "An interrupted switch requires recovery before a login can be saved",
            ["Status.SaveFailed"] = "The login could not be saved",
            ["Status.CheckingSwitch"] = "Checking processes and switching securely. Keep {0} closed until this finishes…",
            ["Status.SwitchException"] = "The account could not be switched: {0}",
            ["Status.NowActive"] = "{0} is now active for {1}.",
            ["Status.AlreadyActive"] = "{0} is already active.",
            ["Status.CurrentAuthMissing"] = "The current authentication file is missing. Sign in and save the current login first.",
            ["Status.ProfileMissing"] = "That encrypted profile no longer exists. The list has been refreshed.",
            ["Status.SwitchRecovery"] = "The switch requires recovery or a current-login capture",
            ["Status.SwitchFailedRestored"] = "The switch failed and the previous login was restored",
            ["Status.SwitchFailed"] = "The account could not be switched",
            ["Status.ConfirmRestore"] = "Confirmation is required before the saved snapshot can replace the live login.",
            ["Status.ConfirmCurrent"] = "Confirmation is required before the last selected profile can be updated.",
            ["Status.SwitchCancelled"] = "Switch cancelled. No authentication file was changed.",
            ["Status.OperationCancelled"] = "Operation cancelled. No authentication file was changed.",
            ["Status.ProcessBlocked"] = "Operation blocked until the process check is clear.",
            ["Status.DarkTheme"] = "Dark appearance enabled.",
            ["Status.LightTheme"] = "Light appearance enabled.",
            ["Status.WaitBeforeClose"] = "Wait for the credential operation to finish before closing the app.",
            ["Failure.WithDetail"] = "{0}: {1}",
            ["Failure.WithoutDetail"] = "{0}.",
        };

        return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = english,
            ["zh-CN"] = Merge("zh-CN", english, ChineseSimplified()),
            ["zh-TW"] = Merge("zh-TW", english, ChineseTraditional()),
            ["es-ES"] = Merge("es-ES", english, Spanish()),
            ["fr-FR"] = Merge("fr-FR", english, French()),
            ["de-DE"] = Merge("de-DE", english, German()),
            ["ja-JP"] = Merge("ja-JP", english, Japanese()),
            ["ko-KR"] = Merge("ko-KR", english, Korean()),
            ["pt-BR"] = Merge("pt-BR", english, PortugueseBrazil()),
            ["ru-RU"] = Merge("ru-RU", english, Russian()),
            ["ar-SA"] = Merge("ar-SA", english, Arabic()),
            ["hi-IN"] = Merge("hi-IN", english, Hindi()),
        };
    }

    private static IReadOnlyDictionary<string, string> Merge(
        string language,
        IReadOnlyDictionary<string, string> english,
        IReadOnlyDictionary<string, string> translation)
    {
        var missingKeys = english.Keys.Except(translation.Keys, StringComparer.Ordinal).ToArray();
        var extraKeys = translation.Keys.Except(english.Keys, StringComparer.Ordinal).ToArray();
        var placeholderMismatches = english.Keys
            .Where(key => translation.ContainsKey(key))
            .Where(key => !PlaceholderIndexes(english[key]).SequenceEqual(PlaceholderIndexes(translation[key])))
            .ToArray();
        if (missingKeys.Length > 0 || extraKeys.Length > 0 || placeholderMismatches.Length > 0)
        {
            throw new InvalidOperationException(
                $"The {language} localization catalog is incomplete or has incompatible format placeholders.");
        }

        var result = new Dictionary<string, string>(english, StringComparer.Ordinal);
        foreach (var pair in translation)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static int[] PlaceholderIndexes(string value) =>
        Regex.Matches(value, @"(?<!\{)\{(\d+)(?:,[^}:]+)?(?::[^}]*)?\}(?!\})")
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToArray();

}
