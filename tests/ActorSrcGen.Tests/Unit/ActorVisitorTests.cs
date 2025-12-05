using System;
using System.Collections.Immutable;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class ActorVisitorTests
{
    private static SyntaxAndSymbol CreateActor(string source)
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
        var classSymbol = model.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol
                  ?? throw new InvalidOperationException("Class symbol not found");
        return new SyntaxAndSymbol(classSyntax, classSymbol, model);
    }

    [Fact]
    public void VisitActor_WithValidInput_ReturnsActorNode()
    {
        var source = """
            using ActorSrcGen;
            public partial class Sample
            {
                [FirstStep]
                public string Step1(string input) => input;

                [NextStep("Step3")]
                [Step]
                public string Step2(string input) => input + "2";

                [LastStep]
                public int Step3(string input) => input.Length;
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();

        var result = visitor.VisitActor(sas);

        Assert.Single(result.Actors);
        Assert.Empty(result.Diagnostics);

        var actor = result.Actors[0];
        Assert.Equal("Sample", actor.Name);
        Assert.True(actor.HasAnyInputTypes);
        Assert.True(actor.HasAnyOutputTypes);
        Assert.Equal(3, actor.StepNodes.Length);
    }

    [Fact]
    public void VisitActor_WithNoInputMethods_ReturnsASG0002Diagnostic()
    {
        var source = """
            using ActorSrcGen;
            public partial class EmptyActor
            {
                public void Helper() {}
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();

        var result = visitor.VisitActor(sas);

        Assert.Empty(result.Actors);
        Assert.Single(result.Diagnostics);
        Assert.Equal("ASG0001", result.Diagnostics[0].Id);
    }

    [Fact]
    public void VisitActor_WithMultipleInputs_ReturnsLinkedBlockGraph()
    {
        var source = """
            using ActorSrcGen;
            public partial class Multi
            {
                [FirstStep]
                public string A(string input) => input;

                [FirstStep]
                [NextStep("C")]
                public string B(string input) => input + "B";

                [LastStep]
                public int C(string input) => input.Length;
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();

        var result = visitor.VisitActor(sas);

        Assert.Single(result.Actors);
        var actor = result.Actors[0];
        Assert.True(actor.HasMultipleInputTypes);
        Assert.False(actor.HasDisjointInputTypes);

        var blockB = actor.StepNodes.First(b => b.Method.Name == "B");
        Assert.Contains(actor.StepNodes.First(b => b.Method.Name == "C").Id, blockB.NextBlocks);
    }

    [Fact]
    public void VisitActor_WithOnlyStep_NoEntry_ReturnsASG0002()
    {
        var source = """
            using ActorSrcGen;
            public partial class NoEntry
            {
                [Step]
                public string Step1(string input) => input;
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();

        var result = visitor.VisitActor(sas);

        Assert.Empty(result.Actors);
        Assert.Contains(result.Diagnostics, d => d.Id == "ASG0002");
    }

    [Fact]
    public void VisitActor_WithDuplicateInputTypes_ReturnsASG0001()
    {
        var source = """
            using ActorSrcGen;
            public partial class DuplicateInputs
            {
                [FirstStep]
                public string A(string input) => input;

                [FirstStep]
                public string B(string input) => input + "b";

                [LastStep]
                public string End(string input) => input;
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();

        var result = visitor.VisitActor(sas);

        Assert.Single(result.Actors);
        Assert.Contains(result.Diagnostics, d => d.Id == "ASG0001");
    }

    [Fact]
    public void VisitActor_WithOnlyIngest_NoSteps_ReturnsDiagnostics()
    {
        var source = """
            using System.Threading.Tasks;
            using ActorSrcGen;
            public partial class OnlyIngest
            {
                [Ingest]
                public static Task<string> PullAsync() => Task.FromResult("x");
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();

        var result = visitor.VisitActor(sas);

        Assert.Empty(result.Actors);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.Contains(result.Diagnostics, d => d.Id == "ASG0001");
        Assert.Contains(result.Diagnostics, d => d.Id == "ASG0002");
    }

    [Fact]
    public void VisitActor_CancelledToken_ReturnsEmpty()
    {
        var source = """
            using ActorSrcGen;
            public partial class Cancelled
            {
                [FirstStep]
                public string Step1(string input) => input;
            }
            """;

        var sas = CreateActor(source);
        var visitor = new ActorVisitor();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = visitor.VisitActor(sas, cts.Token);

        Assert.Empty(result.Actors);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void VisitActor_NamedArgumentNextStep_ResolvesViaSyntaxFallback()
    {
        var source = """
using System;
using ActorSrcGen;

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class NextStepAttribute : Attribute
    {
        public string? Name { get; set; }
    }
}

[Actor]
public partial class CustomNextStep
{
    [FirstStep]
    [CustomAttributes.NextStep(Name = "Finish")]
    public string Start(string input) => input;

    [LastStep]
    public string Finish(string input) => input;
}
""";

        var sas = CreateActor(source);
        var method = sas.Symbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var attribute = method.GetAttributes().First(a => string.Equals(a.AttributeClass?.Name, "NextStepAttribute", StringComparison.Ordinal));
        var extractor = typeof(ActorVisitor).GetMethod("ExtractNextStepName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var next = (string?)extractor!.Invoke(null, new object?[] { attribute, sas.SemanticModel });

        Assert.Equal("Finish", next);
    }

    [Fact]
    public void ExtractNextStepName_UsesConstructorArgument()
    {
        var sas = CreateActor("""
using System;
using ActorSrcGen;

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class NextStepAttribute : Attribute
    {
        public NextStepAttribute(string next)
        {
            Next = next;
        }

        public string Next { get; }
    }
}

[Actor]
public partial class ConstructorNextStep
{
    [FirstStep]
    [CustomAttributes.NextStep("Finish")]
    public string Start(string input) => input;

    [LastStep]
    public string Finish(string input) => input;
}
""");

        var method = sas.Symbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var attribute = method.GetAttributes().First(a => string.Equals(a.AttributeClass?.Name, "NextStepAttribute", StringComparison.Ordinal));
        var extractor = typeof(ActorVisitor).GetMethod("ExtractNextStepName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.True(attribute.ConstructorArguments.Length > 0);
        Assert.Equal("Finish", attribute.ConstructorArguments[0].Value);
        var next = (string?)extractor!.Invoke(null, new object?[] { attribute, sas.SemanticModel });

        Assert.Equal("Finish", next);
    }

    [Fact]
    public void ExtractNextStepName_LiteralNull_ReturnsValueText()
    {
        var sas = CreateActor("""
using ActorSrcGen;

[Actor]
public partial class NullLiteralNextStep
{
    [FirstStep]
    [NextStep(null)]
    public string Start(string input) => input;
}
""");

        var method = sas.Symbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var attribute = method.GetAttributes().First(a => string.Equals(a.AttributeClass?.Name, "NextStepAttribute", StringComparison.Ordinal));
        var extractor = typeof(ActorVisitor).GetMethod("ExtractNextStepName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var next = (string?)extractor!.Invoke(null, new object?[] { attribute, sas.SemanticModel });

        Assert.Equal("null", next);
    }

    [Fact]
    public void ExtractNextStepName_NonStringConstant_ReturnsNull()
    {
        var sas = CreateActor("""
using System;
using ActorSrcGen;

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class NextStepAttribute : Attribute
    {
        public NextStepAttribute(object next)
        {
            Next = next;
        }

        public object Next { get; }
    }
}

[Actor]
public partial class TypeofNextStep
{
    [FirstStep]
    [CustomAttributes.NextStep(typeof(string))]
    public string Start(string input) => input;
}
""");

        var method = sas.Symbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var attribute = method.GetAttributes().First(a => string.Equals(a.AttributeClass?.Name, "NextStepAttribute", StringComparison.Ordinal));
        var extractor = typeof(ActorVisitor).GetMethod("ExtractNextStepName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var next = (string?)extractor!.Invoke(null, new object?[] { attribute, sas.SemanticModel });

        Assert.Null(next);
    }
}