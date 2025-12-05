using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ActorSrcGen.Tests.Helpers;

namespace ActorSrcGen.Tests.Integration;

public class DeterminismTests
{
    [Fact]
    public void Generate_MultipleRuns_ProduceIdenticalOutput()
    {
        const string source = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class FirstActor
{
    [FirstStep]
    [NextStep(nameof(SecondStep))]
    public string FirstStepMethod(string input) => input;

    [Step]
    public string SecondStep(string input) => input + "_next";
}

[Actor]
public partial class SecondActor
{
    [FirstStep]
    public void Start(string input) { }
}
""";

        var hashes = Enumerable.Range(0, 5)
            .Select(_ => ComputeHash(CompilationHelper.GetGeneratedOutput(CompilationHelper.CreateGeneratorDriver(CompilationHelper.CreateCompilation(source)))))
            .ToArray();

        Assert.All(hashes, h => Assert.Equal(hashes[0], h));
    }

    [Fact]
    public void Generate_DifferentRunOrder_SameOutput()
    {
        const string source1 = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class AlphaActor
{
    [FirstStep]
    public void Start(string input) { }
}

[Actor]
public partial class BetaActor
{
    [FirstStep]
    public void Begin(string input) { }
}
""";

        const string source2 = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class BetaActor
{
    [FirstStep]
    public void Begin(string input) { }
}

[Actor]
public partial class AlphaActor
{
    [FirstStep]
    public void Start(string input) { }
}
""";

        var hash1 = ComputeHash(CompilationHelper.GetGeneratedOutput(CompilationHelper.CreateGeneratorDriver(CompilationHelper.CreateCompilation(source1))));
        var hash2 = ComputeHash(CompilationHelper.GetGeneratedOutput(CompilationHelper.CreateGeneratorDriver(CompilationHelper.CreateCompilation(source2))));

        Assert.Equal(hash1, hash2);
    }

    private static string ComputeHash(Dictionary<string, string> outputs)
    {
        var normalized = outputs
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => kvp.Key + "::" + kvp.Value)
            .ToArray();

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(string.Join("|", normalized));
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
