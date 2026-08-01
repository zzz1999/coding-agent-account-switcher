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
            ["Hero.Description"] = "Choose a saved account snapshot. Credentials and selected API fields switch together; MCP servers, skills, plugins, history, and other settings stay put.",
            ["Provider.Codex"] = "Codex",
            ["Provider.ClaudeCode"] = "Claude Code",
            ["Provider.OpenCode"] = "OpenCode",
            ["Provider.Accounts"] = "{0} accounts",
            ["Provider.ManagedFiles"] = "Managed files: {0}",
            ["Action.SaveCurrentLogin"] = "Save current account",
            ["Badge.LocalOnly"] = "LOCAL ONLY",
            ["Empty.LoginDetected.Title"] = "Account configuration detected, no saved profiles",
            ["Empty.LoginDetected.Message"] = "Close the provider, then save the current account. Configure or sign in to the second account through its official flow and save that account too.",
            ["Empty.NoLogin.Title"] = "No account configuration detected",
            ["Empty.NoLogin.Message"] = "Configure or sign in through the provider's official flow first. Close its apps, then return here to save the current account.",
            ["Empty.Unavailable.Title"] = "Saved profiles unavailable",
            ["Empty.Unavailable.Message"] = "The encrypted profile store could not be read. No managed file was changed.",
            ["Security.Title"] = "Only account access changes",
            ["Security.Message"] = "Credentials and selected API-provider fields switch together. Other settings remain untouched, and snapshots stay encrypted for this Windows user.",
            ["Status.Initial"] = "Ready. No managed file is being changed.",
            ["Status.NoTelemetry"] = "No telemetry",
            ["Card.EncryptedSnapshot"] = "ENCRYPTED LOCAL SNAPSHOT",
            ["Card.CodexProfile"] = "Codex profile",
            ["Card.ClaudeProfile"] = "Claude Code profile",
            ["Card.OpenCodeProfile"] = "OpenCode profile",
            ["Card.SavedAt"] = "Saved {0}",
            ["Card.LastSelected"] = "LAST SELECTED",
            ["Card.Saved"] = "SAVED",
            ["Card.Restore"] = "Restore snapshot",
            ["Card.Switch"] = "Switch",
            ["Card.RestoreAutomation"] = "Restore the saved snapshot for {0}",
            ["Card.SwitchAutomation"] = "Switch to {0}",
            ["Card.RenameHint"] = "Double-click to rename {0}",
            ["Card.DeleteAutomation"] = "Delete the saved profile {0}",
            ["Common.Cancel"] = "Cancel",
            ["Common.Close"] = "Close",
            ["Process.Automation"] = "Process check required",
            ["Process.Close.Title"] = "Close running apps before continuing",
            ["Process.Close.Message"] = "The applications below may still be using the managed account files. Close them, then check again. No managed file has been changed.",
            ["Process.Unknown.Title"] = "Process check could not complete",
            ["Process.Unknown.Message"] = "The app could not safely confirm that every related process is closed. Close the provider and its extensions, then check again. No managed file has been changed.",
            ["Process.CheckAgain"] = "Check again",
            ["Process.Running"] = "RUNNING",
            ["Process.CheckFailed"] = "CHECK FAILED",
            ["Process.Inspection"] = "Process inspection",
            ["Process.Unverified"] = "Windows could not verify whether this process is closed.",
            ["Process.Detail"] = "{0}.exe · PID {1}",
            ["Save.Automation"] = "Save current account",
            ["Save.Title"] = "Save the current account",
            ["Save.Description"] = "Choose a local label such as Personal or Work. Credentials and selected API-provider fields are stored in a DPAPI-encrypted local snapshot; token values are never shown in the interface.",
            ["Save.ProfileLabel"] = "PROFILE LABEL",
            ["Save.ProfileLabelAutomation"] = "Profile label",
            ["Save.EnterLabel"] = "Enter a profile label.",
            ["Save.InvalidLabel"] = "Use 80 characters or fewer and no control characters.",
            ["Save.SuggestedPersonal"] = "Personal",
            ["Save.SuggestedWork"] = "Work",
            ["Save.Snapshot"] = "Save encrypted snapshot",
            ["Save.Replace"] = "Replace saved snapshot",
            ["Save.ReplaceConfirm"] = "A profile named {0} already exists. Choose Replace saved snapshot to confirm.",
            ["Rename.Automation"] = "Rename saved profile",
            ["Rename.Title"] = "Rename profile",
            ["Rename.Description"] = "Change the local label for {0}. The encrypted snapshot and provider files are not changed.",
            ["Rename.Action"] = "Rename",
            ["Rename.NameConflict"] = "A profile named {0} already exists.",
            ["Rename.Failure"] = "The profile could not be renamed",
            ["Delete.Automation"] = "Delete saved profile",
            ["Delete.Title"] = "Delete profile?",
            ["Delete.Message"] = "Delete the encrypted local snapshot {0}? This cannot be undone.",
            ["Delete.ActiveMessage"] = "{0} is the last selected profile. Deleting it removes only its local snapshot and selection marker; the provider's live account files remain unchanged. Save the current account again before switching.",
            ["Delete.Safety"] = "This removes app-managed local snapshot files. It does not sign out, delete the provider account, or securely erase disk sectors.",
            ["Delete.Action"] = "Delete profile",
            ["Delete.ActiveConfirmation"] = "This profile became last selected after the dialog opened. Review the warning, then choose Delete profile again.",
            ["Delete.Failure"] = "The profile could not be deleted",
            ["SwitchSuccess.Automation"] = "Account switch completed",
            ["SwitchSuccess.Title"] = "Account switched",
            ["SwitchSuccess.Message"] = "You are now using the {0} account.",
            ["Changed.Automation"] = "Current account confirmation required",
            ["Changed.Confirm.Title"] = "Confirm the current account",
            ["Changed.Confirm.Message"] = "The live account configuration no longer matches the encrypted snapshot for {0}. This may be a normal token refresh, an API-setting change, or a different account. Confirm only if the current account still belongs to {0}; otherwise save it as a new profile first.",
            ["Changed.Confirm.Safety"] = "No managed file has been changed. Confirm only when the live account still belongs to the named profile.",
            ["Changed.Confirm.Action"] = "Confirm & switch",
            ["Changed.Confirm.Automation"] = "Confirm current account and switch",
            ["Changed.Restore.Title"] = "Restore the saved snapshot?",
            ["Changed.Restore.Message"] = "The live account configuration no longer matches {0}. Restoring the saved snapshot will replace the current unsaved account configuration. Save it as a new profile first if you may need it.",
            ["Changed.Restore.Safety"] = "No managed file has been changed. Restore only when you intentionally want to replace the live account configuration shown by this warning.",
            ["Changed.Restore.Action"] = "Restore snapshot",
            ["Changed.Restore.Automation"] = "Restore saved account snapshot",
            ["Changed.LastSelected"] = "the last selected profile",
            ["Changed.SaveAsNew"] = "Save as new…",
            ["Settings.Automation"] = "Application settings",
            ["Settings.Title"] = "Settings",
            ["Settings.Description"] = "Customize this app. Account switching updates credentials and selected API-provider fields only; unrelated provider settings remain untouched.",
            ["Settings.Language"] = "LANGUAGE",
            ["Settings.Language.Description"] = "Changes apply immediately throughout the app.",
            ["Settings.Startup"] = "START WITH WINDOWS",
            ["Settings.Startup.Description"] = "Launch for the current Windows user after sign-in. Administrator access is not required.",
            ["Settings.Startup.Toggle"] = "Start Coding Agent Account Switcher with Windows",
            ["Settings.Update.Title"] = "SOFTWARE UPDATE",
            ["Settings.Update.Check"] = "Check for updates",
            ["Settings.Update.Open"] = "Open download page",
            ["Settings.Update.Current"] = "Installed version: {0}. Update checks run only when you choose Check for updates.",
            ["Settings.Update.Checking"] = "Checking GitHub for an update to version {0}…",
            ["Settings.Update.UpToDate"] = "Version {0} is up to date.",
            ["Settings.Update.Available"] = "Version {1} is available. You have version {0}.",
            ["Settings.Update.Failure"] = "The update check could not be completed",
            ["Settings.Update.OpenFailure"] = "The download page could not be opened: {0}",
            ["Settings.Saved"] = "Settings saved.",
            ["Settings.LanguageFailure"] = "The language setting could not be saved: {0}",
            ["Settings.ThemeFailure"] = "The appearance setting could not be saved: {0}",
            ["Settings.StartupFailure"] = "The Windows startup setting could not be changed: {0}",
            ["Recovery.MetadataInvalid"] = "{0} recovery metadata is invalid: {1}",
            ["Recovery.Recovered"] = "Recovered an interrupted {0} switch.",
            ["Recovery.CloseThenRetry"] = "Close {0}, then choose Save or Switch to retry interrupted-switch recovery.",
            ["Recovery.ProcessUnknown"] = "Windows could not safely check processes for {0} recovery. Try an operation again.",
            ["Recovery.InProgress"] = "Another {0} account operation is in progress.",
            ["Recovery.Attention"] = "{0} recovery requires attention",
            ["Recovery.StartupFailed"] = "Startup recovery failed: {0}",
            ["Status.ReadyProvider"] = "Ready to switch {0} accounts and API configurations.",
            ["Status.ProfilesLoadFailed"] = "Saved profiles could not be loaded: {0}",
            ["Status.ProfilesHidden"] = "Saved profiles hidden because they could not be loaded: {0}.",
            ["Status.CheckingSave"] = "Checking processes and saving securely. Keep {0} closed until this finishes…",
            ["Status.SaveException"] = "The account could not be saved: {0}",
            ["Status.ProfileCaptured"] = "{0} {1} as an encrypted {2} profile.",
            ["Status.SavedVerb"] = "Saved",
            ["Status.UpdatedVerb"] = "Updated",
            ["Status.LoginMissing"] = "No usable {0} account configuration was found. Configure or sign in normally, close the app, then try again.",
            ["Status.AuthenticationEmpty"] = "The managed account configuration is empty or invalid, so nothing was saved.",
            ["Status.ProfileReplaceMissing"] = "The profile to replace no longer exists. The list has been refreshed.",
            ["Status.ProfileRenamed"] = "Renamed {0} to {1}.",
            ["Status.ProfileDeleted"] = "Deleted the encrypted local profile {0}.",
            ["Status.ActiveProfileDeleted"] = "Deleted {0}. Live account files were not changed; save the current account before switching.",
            ["Status.ProfileManagementMissing"] = "That saved profile no longer exists. The list has been refreshed.",
            ["Status.ProfileManagementRecoveryRequired"] = "Finish interrupted-switch recovery before renaming or deleting profiles.",
            ["Status.OperationInProgress"] = "Another account operation is in progress. Try again in a moment.",
            ["Status.CaptureRecovery"] = "An interrupted switch requires recovery before an account can be saved",
            ["Status.SaveFailed"] = "The account could not be saved",
            ["Status.CheckingSwitch"] = "Checking processes and switching securely. Keep {0} closed until this finishes…",
            ["Status.SwitchException"] = "The account could not be switched: {0}",
            ["Status.NowActive"] = "{0} is now active for {1}.",
            ["Status.AlreadyActive"] = "{0} is already active.",
            ["Status.CurrentAuthMissing"] = "The current account configuration is missing. Configure or sign in, then save the current account first.",
            ["Status.ProfileMissing"] = "That encrypted profile no longer exists. The list has been refreshed.",
            ["Status.SwitchRecovery"] = "The switch requires recovery or a current-account capture",
            ["Status.SwitchFailedRestored"] = "The switch failed and the previous account was restored",
            ["Status.SwitchFailed"] = "The account could not be switched",
            ["Status.ConfirmRestore"] = "Confirmation is required before the saved snapshot can replace the live account configuration.",
            ["Status.ConfirmCurrent"] = "Confirmation is required before the last selected profile can be updated.",
            ["Status.SwitchCancelled"] = "Switch cancelled. No managed file was changed.",
            ["Status.OperationCancelled"] = "Operation cancelled. No managed file was changed.",
            ["Status.ProcessBlocked"] = "Operation blocked until the process check is clear.",
            ["Status.DarkTheme"] = "Dark appearance enabled.",
            ["Status.LightTheme"] = "Light appearance enabled.",
            ["Status.WaitBeforeClose"] = "Wait for the account operation to finish before closing the app.",
            ["Status.OpenCodeOverrideBlocked"] = "OpenCode uses a higher-priority account/API override ({0}). Remove or unset that override, then try again.",
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
        var completeTranslation = new Dictionary<string, string>(translation, StringComparer.Ordinal);
        foreach (var pair in ProfileManagementAndUpdateTranslations(language))
        {
            completeTranslation[pair.Key] = pair.Value;
        }

        var missingKeys = english.Keys.Except(completeTranslation.Keys, StringComparer.Ordinal).ToArray();
        var extraKeys = completeTranslation.Keys.Except(english.Keys, StringComparer.Ordinal).ToArray();
        var placeholderMismatches = english.Keys
            .Where(key => completeTranslation.ContainsKey(key))
            .Where(key => !PlaceholderIndexes(english[key]).SequenceEqual(PlaceholderIndexes(completeTranslation[key])))
            .ToArray();
        if (missingKeys.Length > 0 || extraKeys.Length > 0 || placeholderMismatches.Length > 0)
        {
            throw new InvalidOperationException(
                $"The {language} localization catalog is incomplete or has incompatible format placeholders.");
        }

        var result = new Dictionary<string, string>(english, StringComparer.Ordinal);
        foreach (var pair in completeTranslation)
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
