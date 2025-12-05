using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen.Tests.Helpers;

namespace ActorSrcGen.Tests.Integration;

public class ActorPatternTests
{
    private static Dictionary<string, string> Generate(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        return CompilationHelper.GetGeneratedOutput(driver);
    }

    private static string BuildSingleStep(string name, string body) => $@"using ActorSrcGen;

[Actor]
public partial class {name}
{{
    [FirstStep]
    public string Step(string input) => {body};
}}
";

    private static string BuildPipeline(string name, string midBody, string endBody) => $@"using ActorSrcGen;
using System.Threading.Tasks;

[Actor]
public partial class {name}
{{
    [FirstStep]
    [NextStep(nameof(Process))]
    public int Start(string input) => input.Length;

    [Step]
    [NextStep(nameof(Finish))]
    public Task<int> Process(int value) => Task.FromResult({midBody});

    [LastStep]
    public int Finish(int value) => {endBody};
}}
";

    private static string BuildReceiver(string name) => $@"using ActorSrcGen;

[Actor]
public partial class {name}
{{
    [FirstStep]
    [Receiver]
    [NextStep(nameof(Work))]
    public string Start(string input) => input;

    [Step]
    [NextStep(nameof(Done))]
    public string Work(string input) => input + ""_work"";

    [LastStep]
    public string Done(string input) => input + ""_done"";
}}
";

    private static string BuildIngest(string name) => $@"using System.Collections.Generic;
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class {name}
{{
    [Ingest]
    public static Task<string> PullAsync() => Task.FromResult(""ingest"");

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input + ""_end"";
}}
";

    private static string BuildMultiInput(string name) => $@"using ActorSrcGen;

[Actor]
public partial class {name}
{{
    [FirstStep]
    [NextStep(nameof(Merge))]
    public string FromString(string input) => input;

    [FirstStep]
    [NextStep(nameof(Merge))]
    public string FromNumber(int value) => value.ToString();

    [LastStep]
    public string Merge(string input) => input + ""_m"";
}}
";

    [Fact]
    public void SingleStep_SimpleExpression_Generates()
    {
        var src = BuildSingleStep("SingleStepActor1", "input");
        var output = Generate(src);

        Assert.True(output.ContainsKey("SingleStepActor1.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["SingleStepActor1.generated.cs"]));
    }

    [Fact]
    public void SingleStep_WithLiteralConcat_Generates()
    {
        var src = BuildSingleStep("SingleStepActor2", "input + \"_x\"");
        var output = Generate(src);

        Assert.True(output.ContainsKey("SingleStepActor2.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["SingleStepActor2.generated.cs"]));
    }

    [Fact]
    public void SingleStep_WithReturnVoid_Generates()
    {
        var src = """
using ActorSrcGen;

[Actor]
public partial class SingleStepActor3
{
    [FirstStep]
    public void Step(string input) {}
}
""";

        var output = Generate(src);
        Assert.True(output.ContainsKey("SingleStepActor3.generated.cs"));
    }

    [Fact]
    public void Pipeline_TaskMiddle_Completes()
    {
        var src = BuildPipeline("PipelineActor1", "value + 1", "value");
        var output = Generate(src);

        Assert.True(output.ContainsKey("PipelineActor1.generated.cs"));
        Assert.Contains("Process", output["PipelineActor1.generated.cs"]);
    }

    [Fact]
    public void Pipeline_ModifiesEnd_Completes()
    {
        var src = BuildPipeline("PipelineActor2", "value", "value + 10");
        var output = Generate(src);

        Assert.True(output.ContainsKey("PipelineActor2.generated.cs"));
        Assert.Contains("Finish", output["PipelineActor2.generated.cs"]);
    }

    [Fact]
    public void Pipeline_UsesDifferentName_Completes()
    {
        var src = BuildPipeline("PipelineActor3", "value * 2", "value - 1");
        var output = Generate(src);

        Assert.True(output.ContainsKey("PipelineActor3.generated.cs"));
    }

    [Fact]
    public void Receiver_WithFanOut_Generates()
    {
        var src = BuildReceiver("ReceiverActor1");
        var output = Generate(src);

        Assert.True(output.ContainsKey("ReceiverActor1.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["ReceiverActor1.generated.cs"]));
    }

    [Fact]
    public void Receiver_DifferentName_Generates()
    {
        var src = BuildReceiver("ReceiverActor2");
        var output = Generate(src);

        Assert.True(output.ContainsKey("ReceiverActor2.generated.cs"));
    }

    [Fact]
    public void Receiver_WithExtraStep_Generates()
    {
        var src = """
using ActorSrcGen;

[Actor]
public partial class ReceiverActor3
{
    [FirstStep]
    [Receiver]
    [NextStep(nameof(Work))]
    public string Start(string input) => input;

    [Step]
    [NextStep(nameof(Work2))]
    public string Work(string input) => input + "_1";

    [Step]
    [NextStep(nameof(Done))]
    public string Work2(string input) => input + "_2";

    [LastStep]
    public string Done(string input) => input + "_done";
}
""";

        var output = Generate(src);
        Assert.True(output.ContainsKey("ReceiverActor3.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["ReceiverActor3.generated.cs"]));
    }

    [Fact]
    public void Ingest_StaticTask_Generates()
    {
        var src = BuildIngest("IngestActor1");
        var output = Generate(src);

        Assert.True(output.ContainsKey("IngestActor1.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["IngestActor1.generated.cs"]));
    }

    [Fact]
    public void Ingest_WithAsyncEnumerable_Generates()
    {
        var src = """
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class IngestActor2
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

        var output = Generate(src);
        Assert.True(output.ContainsKey("IngestActor2.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["IngestActor2.generated.cs"]));
    }

    [Fact]
    public void Ingest_WithMultipleIngests_Generates()
    {
        var src = """
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class IngestActor3
{
    [Ingest]
    public static Task<string> PullA() => Task.FromResult("a");

    [Ingest]
    public static Task<string> PullB() => Task.FromResult("b");

    [FirstStep]
    [NextStep(nameof(End))]
    public string Start(string input) => input;

    [LastStep]
    public string End(string input) => input;
}
""";

        var output = Generate(src);
        Assert.True(output.ContainsKey("IngestActor3.generated.cs"));
    }

    [Fact]
    public void MultiInput_WithStringAndInt_Generates()
    {
        var src = BuildMultiInput("MultiInputActor1");
        var output = Generate(src);

        Assert.True(output.ContainsKey("MultiInputActor1.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["MultiInputActor1.generated.cs"]));
    }

    [Fact]
    public void MultiInput_WithAdditionalEntry_Generates()
    {
        var src = """
using ActorSrcGen;

[Actor]
public partial class MultiInputActor2
{
    [FirstStep]
    [NextStep(nameof(Merge))]
    public string FromString(string input) => input;

    [FirstStep]
    [NextStep(nameof(Merge))]
    public int FromNumber(int value) => value;

    [FirstStep]
    [NextStep(nameof(Merge))]
    public double FromDouble(double value) => value;

    [LastStep]
    public string Merge(string input) => input + "_m";
}
""";

        var output = Generate(src);
        Assert.True(output.ContainsKey("MultiInputActor2.generated.cs"));
    }

    [Fact]
    public void MultiInput_WithReceivers_Generates()
    {
        var src = """
using ActorSrcGen;

[Actor]
public partial class MultiInputActor3
{
    [FirstStep]
    [NextStep(nameof(Merge))]
    [Receiver]
    public string FromString(string input) => input;

    [FirstStep]
    [NextStep(nameof(Merge))]
    public int FromNumber(int value) => value;

    [LastStep]
    public string Merge(string input) => input + "_m";
}
""";

        var output = Generate(src);
        Assert.True(output.ContainsKey("MultiInputActor3.generated.cs"));
        Assert.False(string.IsNullOrWhiteSpace(output["MultiInputActor3.generated.cs"]));
    }
}
