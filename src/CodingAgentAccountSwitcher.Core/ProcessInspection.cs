using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CodingAgentAccountSwitcher.Core;

public enum ProcessInspectionStatus
{
    Clear,
    Running,
    Unknown
}

public sealed record DetectedProcess(int ProcessId, string ProcessName);

public sealed record ProcessInspectionIssue(string ProcessName, string Message);

public enum ProcessNameMatchKind
{
    Exact,
    Prefix
}

public sealed record ProcessNameRule
{
    private ProcessNameRule(string pattern, ProcessNameMatchKind matchKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern;
        MatchKind = matchKind;
    }

    public string Pattern { get; }

    public ProcessNameMatchKind MatchKind { get; }

    public static ProcessNameRule Exact(string processName) =>
        new(processName, ProcessNameMatchKind.Exact);

    public static ProcessNameRule Prefix(string processNamePrefix) =>
        new(processNamePrefix, ProcessNameMatchKind.Prefix);

    public bool Matches(string processName)
    {
        ArgumentNullException.ThrowIfNull(processName);
        return MatchKind switch
        {
            ProcessNameMatchKind.Exact => string.Equals(Pattern, processName, StringComparison.OrdinalIgnoreCase),
            ProcessNameMatchKind.Prefix => processName.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase),
            _ => throw new InvalidOperationException("The process matching rule is invalid.")
        };
    }
}

public sealed record ProcessInspectionResult
{
    public required ProcessInspectionStatus Status { get; init; }

    public IReadOnlyList<DetectedProcess> Processes { get; init; } = Array.Empty<DetectedProcess>();

    public IReadOnlyList<ProcessInspectionIssue> Issues { get; init; } = Array.Empty<ProcessInspectionIssue>();

    public static ProcessInspectionResult Clear { get; } = new()
    {
        Status = ProcessInspectionStatus.Clear
    };
}

public interface IProcessInspector
{
    ProcessInspectionResult Inspect(IAuthenticationAdapter adapter);
}

public sealed class SystemProcessInspector : IProcessInspector
{
    public ProcessInspectionResult Inspect(IAuthenticationAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        var detected = new Dictionary<int, DetectedProcess>();
        var issues = new List<ProcessInspectionIssue>();

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception exception) when (exception is InvalidOperationException or SystemException)
        {
            return new ProcessInspectionResult
            {
                Status = ProcessInspectionStatus.Unknown,
                Issues = [new ProcessInspectionIssue("*", exception.Message)]
            };
        }

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    var processName = process.ProcessName;
                    if (adapter.BlockingProcessRules.Any(rule => rule.Matches(processName)))
                    {
                        detected[process.Id] = new DetectedProcess(process.Id, process.ProcessName);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process exited after enumeration, so it is no longer capable of using the file.
                }
                catch (SystemException exception)
                {
                    issues.Add(new ProcessInspectionIssue("*", exception.Message));
                }
            }
        }

        var status = issues.Count > 0
            ? ProcessInspectionStatus.Unknown
            : detected.Count > 0
                ? ProcessInspectionStatus.Running
                : ProcessInspectionStatus.Clear;

        return new ProcessInspectionResult
        {
            Status = status,
            Processes = new ReadOnlyCollection<DetectedProcess>(detected.Values.OrderBy(x => x.ProcessId).ToList()),
            Issues = new ReadOnlyCollection<ProcessInspectionIssue>(issues)
        };
    }
}
