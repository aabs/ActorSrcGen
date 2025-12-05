using System;
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
        var classSyntax = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(attr =>
                    attr.Name.ToString().Contains("Actor", StringComparison.Ordinal) ||
                    attr.Name.ToString().Contains("Step", StringComparison.Ordinal) ||
                    attr.Name.ToString().Contains("Ingest", StringComparison.Ordinal)))
            ?? tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
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

    private static SyntaxAndSymbol CreateSymbol(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(attr =>
                    attr.Name.ToString().Contains("Actor", StringComparison.Ordinal) ||
                    attr.Name.ToString().Contains("Step", StringComparison.Ordinal) ||
                    attr.Name.ToString().Contains("Ingest", StringComparison.Ordinal)))
            ?? tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;
        return new SyntaxAndSymbol(classSyntax, classSymbol, model);
    }

    [Fact]
    public void ActorNode_NormalizesDefaultCollections()
    {
        var sas = CreateSymbol("""
using ActorSrcGen;

[Actor]
public partial class Minimal
{
    [FirstStep]
    public string Start(string input) => input;
}
""");

        var actor = new ActorNode(default, default, sas);

        Assert.Empty(actor.StepNodes);
        Assert.Empty(actor.Ingesters);
        Assert.False(actor.HasAnyInputTypes);
        Assert.False(actor.HasAnyOutputTypes);
    }

    [Fact]
    public void ActorNode_CopyConstructor_PreservesNormalizedCollections()
    {
        var sas = CreateSymbol("""
using ActorSrcGen;

[Actor]
public partial class Minimal
{
    [FirstStep]
    public string Start(string input) => input;
}
""");

        var actor = new ActorNode(default, default, sas);
        var clone = actor with { };

        Assert.Empty(clone.StepNodes);
        Assert.Empty(clone.Ingesters);
        Assert.False(clone.HasAnyInputTypes);
        Assert.False(clone.HasAnyOutputTypes);
    }

    [Fact]
    public void BlockNode_NormalizesDefaultNextBlocks()
    {
        var sas = CreateSymbol("""
using ActorSrcGen;

[Actor]
public partial class Single
{
    [FirstStep]
    public string Start(string input) => input;
}
""");

        var method = sas.Symbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var block = new BlockNode(
            HandlerBody: "x => x",
            Id: 1,
            Method: method,
            NodeType: NodeType.Transform,
            NextBlocks: default,
            IsEntryStep: true,
            IsExitStep: false,
            IsAsync: false,
            IsReturnTypeCollection: false);

        Assert.Empty(block.NextBlocks);
    }

    [Fact]
    public void IngestMethod_Priority_DefaultsToMaxValueWhenMissingAttribute()
    {
        var sas = CreateSymbol("""
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class Ingestless
{
    public static Task<string> PullAsync() => Task.FromResult("data");
}
""");

        var method = sas.Symbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "PullAsync");
        var ingest = new IngestMethod(method);

        Assert.Equal(int.MaxValue, ingest.Priority);
    }

    [Fact]
    public void WireBlocks_InvalidNextStepArgument_IgnoresMissingName()
    {
        var actor = Visit("""
using ActorSrcGen;

[Actor]
public partial class MissingNextStepName
{
    [FirstStep]
    [NextStep]
    public string Start(string input) => input;
}
""");

        var start = actor.StepNodes.First();
        Assert.Empty(start.NextBlocks);
    }

    [Fact]
    public void BuildBlocks_VoidStep_UsesActionNodeAndVoidHandler()
    {
        var actor = Visit("""
using ActorSrcGen;

[Actor]
public partial class VoidStepActor
{
    [FirstStep]
    public void Step(string input) { }
}
""");

        var block = actor.StepNodes.First();

        Assert.Equal(NodeType.Action, block.NodeType);
        Assert.Equal("input => { }", block.HandlerBody);
    }

    [Fact]
    public void ActorNode_OutputTypeNames_UnwrapsTaskResults()
    {
        var actor = Visit("""
    using System;
    using System.Threading.Tasks;
    using ActorSrcGen;

[Actor]
public partial class AsyncOutput
{
    [FirstStep]
    [LastStep]
    public Task<int> Step(string input) => Task.FromResult(input.Length);
}
""");

        Assert.Single(actor.OutputTypeNames);
        Assert.Equal("int", actor.OutputTypeNames[0]);
    }

    [Fact]
    public void ActorNode_SingleInputOutputFlags_AreComputed()
    {
        var actor = Visit("""
using ActorSrcGen;

[Actor]
public partial class SingleIO
{
    [FirstStep]
    [LastStep]
    public int Step(string input) => input.Length;
}
""");

        Assert.True(actor.HasSingleInputType);
        Assert.False(actor.HasMultipleInputTypes);
        Assert.True(actor.HasSingleOutputType);
        Assert.False(actor.HasMultipleOutputTypes);
        Assert.True(actor.HasAnyInputTypes);
        Assert.True(actor.HasAnyOutputTypes);
        Assert.Single(actor.InputTypes);
        Assert.Single(actor.OutputTypes);
        Assert.Single(actor.OutputMethods);
    }

    [Fact]
    public void ActorNode_OutputTypeNames_TaskWithoutResult_UsesTaskName()
    {
        var actor = Visit("""
using System;
using System.Threading.Tasks;
using ActorSrcGen;

[Actor]
public partial class TaskOnly
{
    [FirstStep]
    [LastStep]
    public Task Step(string input) => Task.CompletedTask;
}
""");

        Assert.Single(actor.OutputTypeNames);
        Assert.Equal("Task", actor.OutputTypeNames[0]);
    }

    [Fact]
    public void IngestMethod_Priority_UsesAttributeValue()
    {
        var actor = Visit("""
using System;
using System.Threading.Tasks;
using ActorSrcGen;

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class IngestAttribute : Attribute
    {
        public int Priority { get; set; }
    }
}

[Actor]
public partial class IngestWithPriority
{
    [FirstStep]
    public string Start(string input) => input;

    [CustomAttributes.Ingest(Priority = 2)]
    public static Task<string> PullAsync() => Task.FromResult("data");
}
""");

        var ingest = Assert.Single(actor.Ingesters);
        var attribute = ingest.Method.GetAttributes().First(a => string.Equals(a.AttributeClass?.Name, "IngestAttribute", StringComparison.Ordinal));

        Assert.Equal("CustomAttributes.IngestAttribute", attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)));
        Assert.Empty(attribute.ConstructorArguments);
        Assert.True(attribute.NamedArguments.Any());
        Assert.Equal(2, (int)attribute.NamedArguments.First().Value.Value!);
        Assert.Equal(2, ingest.Priority);
    }

    [Fact]
    public void IngestMethod_Priority_UsesConstructorArgument()
    {
        var actor = Visit("""
using System;
using System.Threading.Tasks;
using ActorSrcGen;

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class IngestAttribute : Attribute
    {
        public IngestAttribute(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; }
    }
}

[Actor]
public partial class IngestWithCtorPriority
{
    [FirstStep]
    public string Start(string input) => input;

    [CustomAttributes.Ingest(3)]
    public static Task<string> PullAsync() => Task.FromResult("data");
}
""");

        var ingest = Assert.Single(actor.Ingesters);
        var attribute = ingest.Method.GetAttributes().First(a => string.Equals(a.AttributeClass?.Name, "IngestAttribute", StringComparison.Ordinal));

        var ctorArgument = Assert.Single(attribute.ConstructorArguments);
        Assert.Equal(3, (int)ctorArgument.Value!);
        Assert.Equal(3, ingest.Priority);
    }

    [Fact]
    public void ActorNode_MultipleOutputs_ReportOutputCollections()
    {
        var actor = Visit("""
using ActorSrcGen;

[Actor]
public partial class MultiOutput
{
    [FirstStep]
    [NextStep(nameof(ToInt))]
    [NextStep(nameof(ToUpper))]
    public string Start(string input) => input;

    [LastStep]
    public int ToInt(string input) => input.Length;

    [LastStep]
    public string ToUpper(string input) => input.ToUpperInvariant();
}
""");

        Assert.True(actor.HasMultipleOutputTypes);
        Assert.False(actor.HasSingleOutputType);
        Assert.Equal(2, actor.OutputMethods.Length);
        Assert.Equal(2, actor.OutputTypes.Length);
        Assert.Equal(2, actor.OutputTypeNames.Length);
        Assert.Equal(new[] { "int", "string" }, actor.OutputTypeNames.OrderBy(n => n).ToArray());
    }

    [Fact]
    public void BlockNode_OutputTypeName_UsesRenderTypename()
    {
        var actor = Visit("""
using ActorSrcGen;

[Actor]
public partial class OutputTypeActor
{
    [FirstStep]
    [LastStep]
    public string Step(string input) => input;
}
""");

        var block = actor.StepNodes.First();

        Assert.Equal("string", block.OutputTypeName);
    }

    [Fact]
    public void IngestMethod_InputAndOutputTypes_AreExposed()
    {
        var actor = Visit("""
using System;
using System.Threading.Tasks;
using ActorSrcGen;

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class IngestAttribute : Attribute
    {
        public int Priority { get; set; }
    }
}

[Actor]
public partial class IngestWithInputs
{
    [FirstStep]
    public string Start(string input) => input;

    [CustomAttributes.Ingest(Priority = 5)]
    public static Task<string> PullAsync(int count) => Task.FromResult(count.ToString());
}
""");

        var ingest = Assert.Single(actor.Ingesters);

        Assert.Equal("Int32", ingest.InputTypes.Single().Name);
        Assert.Equal("Task", ingest.OutputType.Name);
    }
}
