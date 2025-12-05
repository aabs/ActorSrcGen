using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class BlockGraphConstructionTests
{
    private static ActorNode Visit(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;
        var sas = new SyntaxAndSymbol(classSyntax, classSymbol, model);
        var visitor = new ActorVisitor();
        var result = visitor.VisitActor(sas);
        Assert.Single(result.Actors);
        return result.Actors[0];
    }

    [Fact]
    public void WireBlocks_LinearChain_LinksInOrder()
    {
        var source = """
using ActorSrcGen;

[Actor]
public partial class Linear
{
    [FirstStep]
    [NextStep(nameof(Middle))]
    public int Start(string input) => input.Length;

    [Step]
    [NextStep(nameof(Finish))]
    public int Middle(int value) => value + 1;

    [LastStep]
    public int Finish(int value) => value;
}
""";

        var actor = Visit(source);
        var start = actor.StepNodes.First(b => b.Method.Name == "Start");
        var middle = actor.StepNodes.First(b => b.Method.Name == "Middle");
        var finish = actor.StepNodes.First(b => b.Method.Name == "Finish");

        Assert.Single(start.NextBlocks);
        Assert.Equal(middle.Id, start.NextBlocks[0]);
        Assert.Single(middle.NextBlocks);
        Assert.Equal(finish.Id, middle.NextBlocks[0]);
        Assert.Empty(finish.NextBlocks);
    }

    [Fact]
    public void WireBlocks_FanOut_SortsAndDeduplicates()
    {
        var source = """
using ActorSrcGen;

[Actor]
public partial class FanOut
{
    [FirstStep]
    [NextStep(nameof(BranchA))]
    [NextStep(nameof(BranchB))]
    [NextStep(nameof(BranchA))]
    public string Start(string input) => input;

    [Step]
    [NextStep(nameof(End))]
    public string BranchA(string input) => input + "a";

    [Step]
    [NextStep(nameof(End))]
    public string BranchB(string input) => input + "b";

    [LastStep]
    public string End(string input) => input;
}
""";

        var actor = Visit(source);
        var start = actor.StepNodes.First(b => b.Method.Name == "Start");

        Assert.Equal(2, start.NextBlocks.Length);
        Assert.Equal(start.NextBlocks.OrderBy(i => i).ToArray(), start.NextBlocks.ToArray());
        Assert.Equal(actor.StepNodes.First(b => b.Method.Name == "BranchA").Id, start.NextBlocks[0]);
        Assert.Equal(actor.StepNodes.First(b => b.Method.Name == "BranchB").Id, start.NextBlocks[1]);
    }

    [Fact]
    public void WireBlocks_MissingTarget_IgnoresUnknown()
    {
        var source = """
using ActorSrcGen;

[Actor]
public partial class MissingTarget
{
    [FirstStep]
    [NextStep("Missing")]
    public int Start(string input) => input.Length;
}
""";

        var actor = Visit(source);
        var start = actor.StepNodes.First();

        Assert.Empty(start.NextBlocks);
    }

    [Fact]
    public void ResolveNodeType_TransformManyForCollections()
    {
        var source = """
using System.Collections.Generic;
using ActorSrcGen;

[Actor]
public partial class Collections
{
    [FirstStep]
    public IEnumerable<string> Start(string input)
    {
        yield return input;
    }
}
""";

        var actor = Visit(source);
        var node = actor.StepNodes.First();

        Assert.Equal(NodeType.TransformMany, node.NodeType);
        Assert.True(node.IsReturnTypeCollection);
    }
}
