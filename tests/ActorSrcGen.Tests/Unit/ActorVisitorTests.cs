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
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
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
}