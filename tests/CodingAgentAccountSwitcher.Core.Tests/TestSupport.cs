namespace CodingAgentAccountSwitcher.Core.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"caas-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class FixedProcessInspector : IProcessInspector
{
    private readonly ProcessInspectionResult _result;

    public FixedProcessInspector(ProcessInspectionResult result)
    {
        _result = result;
    }

    public ProcessInspectionResult Inspect(IAuthenticationAdapter adapter) => _result;
}
