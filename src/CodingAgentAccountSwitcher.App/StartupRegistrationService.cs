using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace CodingAgentAccountSwitcher.App;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodingAgentAccountSwitcher";

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var registration = runKey is null
            ? StartupRegistrationValue.Missing
            : ReadRegistration(runKey);
        return registration.IsOwned(BuildCommand());
    }

    public void SetEnabled(bool enabled)
    {
        var expectedCommand = BuildCommand();

        if (enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("The current-user startup registry key could not be opened.");
            var registration = ReadRegistration(runKey);
            if (ShouldMutateRegistration(enabled: true, registration, expectedCommand))
            {
                runKey.SetValue(ValueName, expectedCommand, RegistryValueKind.String);
            }
        }
        else
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return;
            }

            var registration = ReadRegistration(runKey);
            if (ShouldMutateRegistration(enabled: false, registration, expectedCommand))
            {
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }

    public void RemoveOwnedRegistration()
    {
        var expectedCommand = BuildCommand();
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (runKey is null)
        {
            return;
        }

        // Uninstall cleanup must preserve an unexpected value even when it uses
        // the same registry name. Ownership includes both the raw type and bytes.
        if (ReadRegistration(runKey).IsOwned(expectedCommand))
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string BuildCommand()
        => BuildCommand(Environment.ProcessPath, Assembly.GetEntryAssembly()?.Location);

    internal static string BuildCommand(string? executablePath, string? entryAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("The application executable path could not be resolved.");
        }

        var executableName = Path.GetFileName(executablePath);
        string command;
        if (string.Equals(executableName, "dotnet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(executableName, "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new InvalidOperationException("The application DLL path could not be resolved.");
            }

            command = $"{QuotePath(executablePath)} {QuotePath(entryAssemblyPath)}";
        }
        else
        {
            command = QuotePath(executablePath);
        }

        if (command.Length > 260)
        {
            throw new InvalidOperationException("The Windows startup command exceeds the 260-character limit.");
        }

        return command;
    }

    internal static bool ShouldMutateRegistration(
        bool enabled,
        StartupRegistrationValue registration,
        string expectedCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommand);

        if (enabled)
        {
            if (!registration.Exists)
            {
                return true;
            }

            if (registration.IsOwned(expectedCommand))
            {
                return false;
            }

            throw new InvalidOperationException(
                "A different startup command already uses this application's registry name. It was left unchanged.");
        }

        if (!registration.Exists)
        {
            return false;
        }

        if (registration.IsOwned(expectedCommand))
        {
            return true;
        }

        throw new InvalidOperationException(
            "The existing startup command belongs to a different application location. It was left unchanged.");
    }

    private static StartupRegistrationValue ReadRegistration(RegistryKey runKey)
    {
        var exists = runKey.GetValueNames().Any(name =>
            string.Equals(name, ValueName, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            return StartupRegistrationValue.Missing;
        }

        var kind = runKey.GetValueKind(ValueName);
        var command = runKey.GetValue(
            ValueName,
            defaultValue: null,
            RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        return new StartupRegistrationValue(true, kind, command);
    }

    private static string QuotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.IndexOfAny(['\"', '\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException("The startup executable path contains invalid characters.");
        }

        return $"\"{Path.GetFullPath(path)}\"";
    }
}

internal readonly record struct StartupRegistrationValue(
    bool Exists,
    RegistryValueKind Kind,
    string? Command)
{
    public static StartupRegistrationValue Missing { get; } = new(false, RegistryValueKind.None, null);

    public bool IsOwned(string expectedCommand) =>
        Exists &&
        Kind == RegistryValueKind.String &&
        string.Equals(Command, expectedCommand, StringComparison.Ordinal);
}
