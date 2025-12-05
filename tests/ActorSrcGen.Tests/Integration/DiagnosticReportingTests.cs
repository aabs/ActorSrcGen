using System.Linq;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;

namespace ActorSrcGen.Tests.Integration;

public class DiagnosticReportingTests
{
    [Fact]
    public void MissingInputTypes_ReportsASG0002()
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

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var diagnostics = driver.GetRunResult().Diagnostics;

        Assert.Single(diagnostics);
        Assert.Equal("ASG0002", diagnostics[0].Id);
    }

    [Fact]
    public void InvalidIngestMethod_ReportsASG0003()
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

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var diagnostics = driver.GetRunResult().Diagnostics;

        Assert.Single(diagnostics);
        Assert.Equal("ASG0003", diagnostics[0].Id);
    }

    [Fact]
    public void MultipleErrors_ReportsAllDiagnostics()
    {
        const string source = """
using ActorSrcGen;
using System.Threading.Tasks;

namespace ActorSrcGen.Generated.Tests;
[Actor]
public partial class BrokenActor
{
    [Ingest]
    public Task<string> BadIngest() => Task.FromResult("oops");
}
""";

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var diagnostics = driver.GetRunResult().Diagnostics;
        var ordered = diagnostics.OrderBy(d => d.Id).ToArray();

        Assert.Equal(3, ordered.Length);
        Assert.Equal(new[] { "ASG0001", "ASG0002", "ASG0003" }, ordered.Select(d => d.Id).ToArray());
    }
}
