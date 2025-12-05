using System.Collections.Immutable;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class ActorNodeTests
{
    private static (SyntaxAndSymbol sas, ImmutableArray<IMethodSymbol> methods) CreateActor(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = model.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol
                          ?? throw new InvalidOperationException("Class symbol not found");
        var methods = classSymbol.GetMembers().OfType<IMethodSymbol>().Where(m => !string.Equals(m.Name, ".ctor", StringComparison.Ordinal)).ToImmutableArray();
        return (new SyntaxAndSymbol(classSyntax, classSymbol, model), methods);
    }

    [Fact]
    public void ActorNode_ComputedProperties_SingleInput()
    {
        var source = """
            using ActorSrcGen;
            public partial class Sample
            {
                [FirstStep]
                public string Step1(string input) => input;

                [LastStep]
                public int Step2(string input) => input.Length;
            }
            """;

        var (sas, methods) = CreateActor(source);
        var step1 = new BlockNode("", 1, methods.First(m => m.Name == "Step1"), NodeType.Transform, ImmutableArray<int>.Empty, true, false, false, false);
        var step2 = new BlockNode("", 2, methods.First(m => m.Name == "Step2"), NodeType.Transform, ImmutableArray<int>.Empty, false, true, false, false);

        var actor = new ActorNode(ImmutableArray.Create(step1, step2), ImmutableArray<IngestMethod>.Empty, sas);

        Assert.True(actor.HasAnyInputTypes);
        Assert.True(actor.HasDisjointInputTypes);
        Assert.True(actor.HasAnyOutputTypes);
        Assert.Equal("Sample", actor.Name);
    }

    [Fact]
    public void ActorNode_DisjointInputs_FalseWhenDuplicateTypes()
    {
        var source = """
            using ActorSrcGen;
            public partial class MultiInput
            {
                [FirstStep]
                public void Step1(string input) { }

                [FirstStep]
                public void Step2(string input) { }
            }
            """;

        var (sas, methods) = CreateActor(source);
        var step1 = new BlockNode("", 1, methods.First(m => m.Name == "Step1"), NodeType.Transform, ImmutableArray<int>.Empty, true, false, false, false);
        var step2 = new BlockNode("", 2, methods.First(m => m.Name == "Step2"), NodeType.Transform, ImmutableArray<int>.Empty, true, false, false, false);

        var actor = new ActorNode(ImmutableArray.Create(step1, step2), ImmutableArray<IngestMethod>.Empty, sas);

        Assert.False(actor.HasDisjointInputTypes);
        Assert.True(actor.HasMultipleInputTypes);
    }
}
