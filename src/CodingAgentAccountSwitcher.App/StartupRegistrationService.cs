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
        var expectedCommand = BuildCommand();
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var registration = runKey is null
            ? StartupRegistrationValue.Missing
            : ReadRegistration(runKey);
        return IsRegistrationEnabled(registration, expectedCommand);
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
        string expectedCommand) =>
        ShouldMutateRegistration(
            enabled,
            registration,
            expectedCommand,
            IsRegisteredExecutableConfirmedMissing);

    internal static bool ShouldMutateRegistration(
        bool enabled,
        StartupRegistrationValue registration,
        string expectedCommand,
        Func<string, bool> isRegisteredExecutableConfirmedMissing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommand);
        ArgumentNullException.ThrowIfNull(isRegisteredExecutableConfirmedMissing);

        var status = ClassifyRegistration(registration, expectedCommand);
        if (enabled)
        {
            if (status == StartupRegistrationStatus.Missing)
            {
                return true;
            }

            if (status == StartupRegistrationStatus.Owned)
            {
                return false;
            }

            if (status == StartupRegistrationStatus.Relocated)
            {
                if (registration.TryGetRelocatedApplicationPath(
                        expectedCommand,
                        out var registeredExecutablePath) &&
                    isRegisteredExecutableConfirmedMissing(registeredExecutablePath))
                {
                    return true;
                }

                throw new InvalidOperationException(
                    "Another existing copy of this application already owns the startup registration. It was left unchanged.");
            }

            throw new InvalidOperationException(
                "A different startup command already uses this application's registry name. It was left unchanged.");
        }

        if (status == StartupRegistrationStatus.Missing)
        {
            return false;
        }

        if (status == StartupRegistrationStatus.Owned)
        {
            return true;
        }

        throw new InvalidOperationException(
            "The existing startup command belongs to a different application location. It was left unchanged.");
    }

    internal static bool IsRegistrationEnabled(
        StartupRegistrationValue registration,
        string expectedCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommand);
        return ClassifyRegistration(registration, expectedCommand) switch
        {
            StartupRegistrationStatus.Missing => false,
            StartupRegistrationStatus.Owned => true,
            // A stale path may be repaired only after the user explicitly enables
            // startup. A status query never opens the registry for writing.
            StartupRegistrationStatus.Relocated => false,
            _ => throw new InvalidOperationException(
                "A different startup command already uses this application's registry name. It was left unchanged."),
        };
    }

    internal static StartupRegistrationStatus ClassifyRegistration(
        StartupRegistrationValue registration,
        string expectedCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommand);
        if (!registration.Exists)
        {
            return StartupRegistrationStatus.Missing;
        }

        if (registration.IsOwned(expectedCommand))
        {
            return StartupRegistrationStatus.Owned;
        }

        return registration.IsRelocatedApplicationCommand(expectedCommand)
            ? StartupRegistrationStatus.Relocated
            : StartupRegistrationStatus.Conflicting;
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

    private static bool IsRegisteredExecutableConfirmedMissing(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return false;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // An inaccessible or otherwise unverified path is not proof that the
            // previous executable is gone, so preserve its registration.
            return false;
        }
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

    public bool IsRelocatedApplicationCommand(string expectedCommand)
        => TryGetRelocatedApplicationPath(expectedCommand, out _);

    public bool TryGetRelocatedApplicationPath(
        string expectedCommand,
        out string registeredPath)
    {
        registeredPath = string.Empty;
        if (!Exists ||
            Kind != RegistryValueKind.String ||
            !TryReadSingleExecutablePath(Command, out var candidateRegisteredPath) ||
            !TryReadSingleExecutablePath(expectedCommand, out var expectedPath))
        {
            return false;
        }

        var registeredName = Path.GetFileName(candidateRegisteredPath);
        var expectedName = Path.GetFileName(expectedPath);
        if (!IsKnownProductExecutableName(registeredName) ||
            !IsKnownProductExecutableName(expectedName))
        {
            return false;
        }

        registeredPath = candidateRegisteredPath;
        return true;
    }

    private static bool TryReadSingleExecutablePath(string? command, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrEmpty(command) ||
            command.Length < 3 ||
            command[0] != '"' ||
            command[^1] != '"' ||
            command.AsSpan(1, command.Length - 2).Contains('"'))
        {
            return false;
        }

        var candidate = command[1..^1];
        if (!Path.IsPathFullyQualified(candidate) ||
            candidate.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static bool IsKnownProductExecutableName(string executableName) =>
        string.Equals(executableName, "CodingAgentAccountSwitcher.exe", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            executableName,
            "coding-agent-account-switcher-portable-win-x64.exe",
            StringComparison.OrdinalIgnoreCase);
}

internal enum StartupRegistrationStatus
{
    Missing,
    Owned,
    Relocated,
    Conflicting,
}
