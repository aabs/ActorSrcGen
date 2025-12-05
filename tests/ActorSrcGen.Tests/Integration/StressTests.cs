using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActorSrcGen.Tests.Integration;

public class StressTests
{
    private static string BuildLargeActorSet(int count)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("using ActorSrcGen;");
        for (var i = 0; i < count; i++)
        {
            builder.AppendLine($"[Actor]\npublic partial class StressActor{i}\n{{\n    [FirstStep]\n    public int Step{i}(int value) => value + 1;\n\n    [NextStep(\"Step{i}B\")]\n    [Step]\n    public int Step{i}A(int value) => value + 2;\n\n    [LastStep]\n    public int Step{i}B(int value) => value + 3;\n}}\n");
        }
        return builder.ToString();
    }

    [Fact]
    public void Generate_LargeInputSet_HandlesGracefully()
    {
        var source = BuildLargeActorSet(120);
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var output = CompilationHelper.GetGeneratedOutput(updated);

        Assert.Equal(120, output.Count);
    }

    [Fact]
    public void Generate_DeepNesting_DoesNotStackOverflow()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("using ActorSrcGen;");
        builder.AppendLine("[Actor]\npublic partial class DeepActor {\n    [FirstStep]\n    public int Step0(int x) => x;\n");
        const int depth = 60;
        for (var i = 1; i <= depth; i++)
        {
            builder.AppendLine($"    [NextStep(\"Step{i}\")]\n    [Step]\n    public int Step{i}(int x) => x + {i};\n");
        }
        builder.AppendLine("    [LastStep]\n    public int StepLast(int x) => x;\n}");

        var source = builder.ToString();
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var output = CompilationHelper.GetGeneratedOutput(updated);

        Assert.Single(output);
    }

    [Fact]
    public void Generate_ComplexGraphs_HandlesAllPatterns()
    {
        var source = """
using ActorSrcGen;

[Actor]
public partial class ComplexActor
{
    [FirstStep]
    public int Ingest(int x) => x;

    [NextStep("BranchB")]
    [Step]
    public int BranchA(int x) => x + 1;

    [NextStep("Merge")]
    [Step]
    public int BranchB(int x) => x + 2;

    [Step]
    public int Merge(int x) => x * 2;

    [LastStep]
    public int Final(int x) => x - 1;
}
""";

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var output = CompilationHelper.GetGeneratedOutput(updated);

        Assert.Single(output);
    }
}
