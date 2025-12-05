using System.Linq;
using System.Threading.Tasks;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActorSrcGen.Tests.Integration;

public class DiagnosticSnapshotTests
{
    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        return driver.GetRunResult().Diagnostics;
    }

    [Fact]
    public Task MissingStepMethods_Snapshot()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class MissingSteps
{
    // No step or ingest methods
}
""";

        var diagnostics = GetDiagnostics(source)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .OrderBy(s => s)
            .ToArray();

        return Verifier.Verify(diagnostics, SnapshotHelper.CreateSettings("DiagnosticMessages/MissingStepMethods"));
    }

    [Fact]
    public Task NoInputTypes_Snapshot()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class NoInputs
{
    [FirstStep]
    public void Start() { }
}
""";

        var diagnostics = GetDiagnostics(source)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .OrderBy(s => s)
            .ToArray();

        return Verifier.Verify(diagnostics, SnapshotHelper.CreateSettings("DiagnosticMessages/NoInputTypes"));
    }

    [Fact]
    public Task InvalidIngest_Snapshot()
    {
        const string source = """
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class InvalidIngest
{
    [FirstStep]
    public string Start(string input) => input;

    [Ingest]
    public Task<string> PullAsync() => Task.FromResult("oops");
}
""";

        var diagnostics = GetDiagnostics(source)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .OrderBy(s => s)
            .ToArray();

        return Verifier.Verify(diagnostics, SnapshotHelper.CreateSettings("DiagnosticMessages/InvalidIngest"));
    }
}
