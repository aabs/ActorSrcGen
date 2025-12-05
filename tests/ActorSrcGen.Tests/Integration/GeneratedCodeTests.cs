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

    private static string GenerateCode(string source, string hintName)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var outputs = CompilationHelper.GetGeneratedOutput(driver);
        return outputs[hintName];
    }
}
