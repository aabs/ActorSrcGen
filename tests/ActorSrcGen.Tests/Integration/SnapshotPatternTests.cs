using System.Threading.Tasks;
using ActorSrcGen.Tests.Helpers;

namespace ActorSrcGen.Tests.Integration;

public class SnapshotPatternTests
{
    private static string GenerateCode(string source, string hintName)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var outputs = CompilationHelper.GetGeneratedOutput(driver);
        return outputs[hintName];
    }

    [Fact]
    public Task SimpleActor_Snapshot()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class SimpleActor
{
    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input + "_done";
}
""";

        var generated = GenerateCode(source, "SimpleActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/SimpleActor");
    }

    [Fact]
    public Task PipelineActor_Snapshot()
    {
        const string source = """
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class PipelineActor
{
    [FirstStep]
    [NextStep(nameof(Process))]
    public int Start(string input) => input.Length;

    [Step]
    [NextStep(nameof(End))]
    public Task<int> Process(int value) => Task.FromResult(value + 1);

    [LastStep]
    public int End(int value) => value;
}
""";

        var generated = GenerateCode(source, "PipelineActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/PipelineActor");
    }

    [Fact]
    public Task MultiInputActor_Snapshot()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class MultiInputActor
{
    [FirstStep]
    [NextStep(nameof(Merge))]
    public string FromString(string input) => input;

    [FirstStep]
    [NextStep(nameof(Merge))]
    public string FromNumber(int value) => value.ToString();

    [LastStep]
    public string Merge(string input) => input + "_m";
}
""";

        var generated = GenerateCode(source, "MultiInputActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/MultiInputActor");
    }

    [Fact]
    public Task IngestActor_Snapshot()
    {
        const string source = """
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class IngestActor
{
    [Ingest]
    public static Task<string> PullAsync() => Task.FromResult("ingest");

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input + "_end";
}
""";

        var generated = GenerateCode(source, "IngestActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/IngestActor");
    }

    [Fact]
    public Task ReceiverActor_Snapshot()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class ReceiverActor
{
    [FirstStep]
    [Receiver]
    [NextStep(nameof(Process))]
    public string Start(string input) => input;

    [Step]
    [NextStep(nameof(End))]
    public string Process(string input) => input + "_p";

    [LastStep]
    public string End(string input) => input + "_end";
}
""";

        var generated = GenerateCode(source, "ReceiverActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/ReceiverActor");
    }

    [Fact]
    public Task ComplexActor_Snapshot()
    {
        const string source = """
using System.Collections.Generic;
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class ComplexActor
{
    [Ingest]
    public static Task<string> PullAsync() => Task.FromResult("ingest");

    [FirstStep]
    [Receiver]
    [NextStep(nameof(FanOut))]
    public string Start(string input) => input;

    [Step]
    [NextStep(nameof(Join))]
    public string FanOut(string input) => input + "_a";

    [Step]
    [NextStep(nameof(Join))]
    public string FanOut2(string input) => input + "_b";

    [Step]
    [NextStep(nameof(End))]
    public IEnumerable<string> Join(string input)
    {
        yield return input + "_1";
        yield return input + "_2";
    }

    [LastStep]
    public Task<string> End(string input) => Task.FromResult(input + "_end");
}
""";

        var generated = GenerateCode(source, "ComplexActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/ComplexActor");
    }
}
