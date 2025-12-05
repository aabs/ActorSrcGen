using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using ActorSrcGen;
using ActorSrcGen.Generators;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class ActorGeneratorTests
{
    [Fact]
    public void GenerateActor_WithMultipleInputsAndReceiver_UsesSpecificCallMethod()
    {
        const string source = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class MultiReceiverActor
{
    [FirstStep]
    [Receiver]
    [NextStep(nameof(Process))]
    public string Start(string input) => input;

    [FirstStep]
    [NextStep(nameof(Process))]
    public int Alt(int value) => value;

    [LastStep]
    public string Process(string input) => input;
}
""";

        var syntaxAndSymbol = GetSyntaxAndSymbol(source, "MultiReceiverActor");
        var visitor = new ActorVisitor();
        var result = visitor.VisitActor(syntaxAndSymbol);
        var actor = Assert.Single(result.Actors);

        var diagnostics = new List<Diagnostic>();
        var context = default(SourceProductionContext);
        var generator = new ActorGenerator(context);

        generator.GenerateActor(actor);
        var generated = generator.Builder.ToString();

        Assert.Contains("CallStart", generated);
        Assert.Contains("ListenForReceiveStart", generated);
    }

    [Fact]
    public void PrivateHelpers_HandleNonStandardNodeTypes()
    {
        const string source = """
using System.Collections.Generic;
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class NodeTypeActor
{
    [Step]
    public int TransformStep(string input) => input.Length;
}
""";

        var methodSymbol = GetMethodSymbol(source, "TransformStep");

        var broadcastNode = CreateBlockNode(methodSymbol, NodeType.Broadcast);
        var blockType = InvokePrivateStatic<string>("ChooseBlockType", broadcastNode);
        Assert.Contains("BroadcastBlock", blockType);

        var bufferNode = CreateBlockNode(methodSymbol, NodeType.Buffer);
        var methodBody = InvokePrivateStatic<string>("ChooseMethodBody", bufferNode);
        Assert.Contains(methodSymbol.Name, methodBody);

        var getBlockBaseType = typeof(ActorGenerator)
            .GetMethod("GetBlockBaseType", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(getBlockBaseType);

        foreach (var nodeType in new[]
                 {
                     NodeType.Batch,
                     NodeType.BatchedJoin,
                     NodeType.Buffer,
                     NodeType.Join,
                     NodeType.WriteOnce,
                     NodeType.TransformMany
                 })
        {
            var baseType = getBlockBaseType!.Invoke(null, new object[] { CreateBlockNode(methodSymbol, nodeType) });
            Assert.NotNull(baseType);
        }
    }

    [Fact]
    public void GenerateIoBlockAccessors_WithMultipleOutputs_EmitsPerStepOutputs()
    {
        const string source = """
using ActorSrcGen;

namespace ActorSrcGen.Generated.Tests;

[Actor]
public partial class MultiOutputActor
{
    [FirstStep]
    [NextStep(nameof(FinishA))]
    public string StartA(string input) => input;

    [FirstStep]
    [NextStep(nameof(FinishB))]
    public int StartB(int input) => input;

    [LastStep]
    public string FinishA(string input) => input;

    [LastStep]
    public int FinishB(int input) => input;
}
""";

        var (_, context) = CreateGenerationContext(source, "MultiOutputActor");
        var generator = new ActorGenerator(context.SrcGenCtx);

        var generateIoAccessors = typeof(ActorGenerator)
            .GetMethod("GenerateIoBlockAccessors", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(generateIoAccessors);

        generateIoAccessors!.Invoke(generator, new object[] { context });
        var generated = context.Builder.ToString();

        Assert.Contains("StartAInputBlock", generated);
        Assert.Contains("StartBInputBlock", generated);
        Assert.Contains("FinishAOutputBlock", generated);
        Assert.Contains("FinishBOutputBlock", generated);
    }

    private static SyntaxAndSymbol GetSyntaxAndSymbol(string source, string className)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == className);
        var symbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classSyntax)!;
        return new SyntaxAndSymbol(classSyntax, symbol, semanticModel);
    }

    private static IMethodSymbol GetMethodSymbol(string source, string methodName)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);
        var methodSyntax = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == methodName);
        return (IMethodSymbol)semanticModel.GetDeclaredSymbol(methodSyntax)!;
    }

    private static BlockNode CreateBlockNode(IMethodSymbol methodSymbol, NodeType nodeType)
    {
        return new BlockNode(
            HandlerBody: "handler",
            Id: 1,
            Method: methodSymbol,
            NodeType: nodeType,
            NextBlocks: ImmutableArray<int>.Empty,
            IsEntryStep: true,
            IsExitStep: true,
            IsAsync: methodSymbol.IsAsynchronous(),
            IsReturnTypeCollection: methodSymbol.ReturnTypeIsCollection());
    }

    private static T InvokePrivateStatic<T>(string methodName, BlockNode node)
    {
        var method = typeof(ActorGenerator).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(null, new object[] { node });
        return (T)result!;
    }

    private static (ActorNode Actor, ActorGenerationContext Context) CreateGenerationContext(string source, string className)
    {
        var syntaxAndSymbol = GetSyntaxAndSymbol(source, className);
        var visitor = new ActorVisitor();
        var visitResult = visitor.VisitActor(syntaxAndSymbol);
        var actor = Assert.Single(visitResult.Actors);

        var builder = new StringBuilder();
        var srcCtx = default(SourceProductionContext);
        var context = new ActorGenerationContext(actor, builder, srcCtx);
        return (actor, context);
    }
}
