using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen.Tests.Helpers;

namespace ActorSrcGen.Tests.Integration;

public class IngestMethodTests
{
    private static Dictionary<string, string> Generate(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        return CompilationHelper.GetGeneratedOutput(driver);
    }

    [Fact]
    public void Ingest_StaticTask_IsAccepted()
    {
        const string source = """
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class IngestTaskActor
{
    [Ingest]
    public static Task<string> PullAsync() => Task.FromResult("ingest");

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input;
}
""";

        var output = Generate(source);
        Assert.True(output.ContainsKey("IngestTaskActor.generated.cs"));
    }

    [Fact]
    public void Ingest_StaticAsyncEnumerable_IsAccepted()
    {
        const string source = """
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class IngestAsyncEnumerableActor
{
    [Ingest]
    public static async IAsyncEnumerable<string> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        yield return "data";
    }

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input;
}
""";

        var output = Generate(source);
        Assert.True(output.ContainsKey("IngestAsyncEnumerableActor.generated.cs"));
    }

    [Fact]
    public void Ingest_NonStatic_IsRejected()
    {
        const string source = """
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class IngestNonStaticActor
{
    [Ingest]
    public Task<string> PullAsync() => Task.FromResult("ingest");

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input;
}
""";

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var diagnostics = driver.GetRunResult().Results.SelectMany(r => r.Diagnostics).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Ingest_InvalidReturnType_IsRejected()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class IngestInvalidReturnActor
{
    [Ingest]
    public string Pull() => "bad";

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input;
}
""";

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var diagnostics = driver.GetRunResult().Results.SelectMany(r => r.Diagnostics).ToArray();

        Assert.NotEmpty(diagnostics);
    }
}
