using System.Linq;
using ActorSrcGen.Tests.Helpers;

namespace ActorSrcGen.Tests.Integration;

public class DiagnosticMessageSnapshotTests
{
    [Fact]
    public Task ASG0001_Message_MatchesSnapshot()
    {
        const string source = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;
[Actor]
public partial class BrokenActor
{
}
""";

        return VerifyMessages(source, "DiagnosticMessages/ASG0001");
    }

    [Fact]
    public Task ASG0002_Message_MatchesSnapshot()
    {
        const string source = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;
[Actor]
public partial class NoEntryActor
{
    [FirstStep]
    public void Start() { }
}
""";

        return VerifyMessages(source, "DiagnosticMessages/ASG0002");
    }

    [Fact]
    public Task ASG0003_Message_MatchesSnapshot()
    {
        const string source = """
using ActorSrcGen;
using System.Threading.Tasks;

namespace ActorSrcGen.Generated.Tests;
[Actor]
public partial class BadIngestActor
{
    [FirstStep]
    public void Start(string input) { }

    [Ingest]
    public Task<string> BadIngest() => Task.FromResult("oops");
}
""";

        return VerifyMessages(source, "DiagnosticMessages/ASG0003");
    }

    private static Task VerifyMessages(string source, string fileName)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var diagnostics = driver.GetRunResult().Diagnostics
            .OrderBy(d => d.Id)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .ToArray();

        var settings = SnapshotHelper.CreateSettings(fileName);
        return Verifier.Verify(diagnostics, settings);
    }
}
