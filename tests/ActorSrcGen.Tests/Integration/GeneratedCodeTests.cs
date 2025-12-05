using System.Threading.Tasks;
using ActorSrcGen.Tests.Helpers;

namespace ActorSrcGen.Tests.Integration;

public class GeneratedCodeTests
{
    [Fact]
    public Task GenerateSingleInputOutput_ProducesValidCode()
    {
        var source = TestActorFactory.CreateTestActor("SingleInputOutputActor", new[]
        {
            "        [FirstStep]\n        [NextStep(nameof(Process))]\n        public string Start(string input) => input;",
            "        [Step]\n        public string Process(string input) => input + \"_processed\";"
        });

        var generated = GenerateCode(source, "SingleInputOutputActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/SingleInputOutput");
    }

    [Fact]
    public Task GenerateMultipleInputs_ProducesValidCode()
    {
        var source = TestActorFactory.CreateTestActor("MultiInputActor", new[]
        {
            "        [FirstStep]\n        [NextStep(nameof(Merge))]\n        public string FromString(string input) => input;",
            "        [FirstStep]\n        [NextStep(nameof(Merge))]\n        public string FromNumber(int value) => value.ToString();",
            "        [LastStep]\n        public string Merge(string input) => input + \"_done\";"
        });

        var generated = GenerateCode(source, "MultiInputActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/MultipleInputs");
    }

    [Fact]
    public Task GenerateWithFirstStep_ProducesValidCode()
    {
        var source = TestActorFactory.CreateTestActor("FirstStepPatternActor", new[]
        {
            "        [FirstStep]\n        [NextStep(nameof(Process))]\n        public int Start(string input) => input.Length;",
            "        [Step]\n        [NextStep(nameof(Finish))]\n        public int Process(int value) => value + 1;",
            "        [LastStep]\n        public int Finish(int value) => value;"
        });

        var generated = GenerateCode(source, "FirstStepPatternActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/FirstStepPattern");
    }

    [Fact]
    public Task GenerateWithLastStep_ProducesValidCode()
    {
        var source = TestActorFactory.CreateTestActor("LastStepPatternActor", new[]
        {
            "        [FirstStep]\n        [NextStep(nameof(Complete))]\n        public int Begin(string input) => input.Length;",
            "        [LastStep]\n        public Task<int> Complete(int value) => Task.FromResult(value);"
        });

        var generated = GenerateCode(source, "LastStepPatternActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/LastStepPattern");
    }

    [Fact]
    public Task GenerateFanOut_ProducesBroadcastBlocks()
    {
        var source = TestActorFactory.CreateTestActor("FanOutActor", new[]
        {
            "        [FirstStep]\n        [NextStep(nameof(Branch1))]\n        [NextStep(nameof(Branch2))]\n        public string Start(string input) => input;",
            "        [Step]\n        [NextStep(nameof(Join))]\n        public string Branch1(string input) => input + \"_a\";",
            "        [Step]\n        [NextStep(nameof(Join))]\n        public string Branch2(string input) => input + \"_b\";",
            "        [LastStep]\n        public string Join(string input) => input + \"_done\";"
        });

        var generated = GenerateCode(source, "FanOutActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/FanOutPattern");
    }

    [Fact]
    public Task GenerateIngestAndReceiver_ProducesReceiverAndIngestBlocks()
    {
        const string source = """
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class IngestReceiverActor
{
    [FirstStep]
    [Receiver]
    [NextStep(nameof(Process))]
    public string Start(string input) => input;

    [Step]
    [NextStep(nameof(Finish))]
    public string Process(string input) => input + "_p";

    [LastStep]
    public Task<string> Finish(string input) => Task.FromResult(input + "_f");

    [Ingest]
    public static async Task<string> PullAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        return "ingested";
    }

    [Ingest]
    public static async IAsyncEnumerable<string> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        yield return "stream";
    }
}
""";

        var generated = GenerateCode(source, "IngestReceiverActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/IngestReceiverPattern");
    }

    [Fact]
    public Task GenerateTransformMany_ProducesTransformManyBlock()
    {
        const string source = """
using System.Collections.Generic;
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class TransformManyActor
{
    [FirstStep]
    [NextStep(nameof(Finish))]
    public IEnumerable<string> Start(string input)
    {
        yield return input;
    }

    [LastStep]
    public string Finish(string input) => input;
}
""";

        var generated = GenerateCode(source, "TransformManyActor.generated.cs");
        return SnapshotHelper.VerifyGeneratedOutput(generated, "GeneratedCode/TransformManyPattern");
    }

    private static string GenerateCode(string source, string hintName)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var outputs = CompilationHelper.GetGeneratedOutput(driver);
        return outputs[hintName];
    }
}
