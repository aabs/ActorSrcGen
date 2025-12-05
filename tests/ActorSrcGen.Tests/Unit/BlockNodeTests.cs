using System.Collections.Immutable;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class BlockNodeTests
{
    private static IMethodSymbol GetMethod(string methodName)
    {
        var source = """
            using ActorSrcGen;
            public partial class Sample
            {
                [FirstStep]
                public string Step1(string input) => input;
            }
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = model.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol
                          ?? throw new InvalidOperationException("Class symbol not found");
        return classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == methodName);
    }

    [Fact]
    public void BlockNode_StoresMetadata()
    {
        var method = GetMethod("Step1");
        var node = new BlockNode("handler", 1, method, NodeType.Transform, ImmutableArray<int>.Empty, true, false, false, false);

        Assert.Equal(1, node.Id);
        Assert.Equal(NodeType.Transform, node.NodeType);
        Assert.Equal("handler", node.HandlerBody);
        Assert.True(node.IsEntryStep);
        Assert.Equal(method, node.Method);
    }

    [Fact]
    public void BlockNode_NextBlocksImmutable()
    {
        var method = GetMethod("Step1");
        var node = new BlockNode("handler", 1, method, NodeType.Transform, ImmutableArray<int>.Empty, true, false, false, false);

        var updated = node with { NextBlocks = node.NextBlocks.Add(2) };

        Assert.True(node.NextBlocks.IsEmpty);
        Assert.Single(updated.NextBlocks);
        Assert.Equal(2, updated.NextBlocks[0]);
    }
}
