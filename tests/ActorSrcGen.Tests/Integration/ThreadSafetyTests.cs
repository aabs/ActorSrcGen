using System.Collections.Concurrent;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActorSrcGen.Tests.Integration;

public class ThreadSafetyTests
{
    private static string BuildActors(int count)
    {
        var lines = new List<string> { "using ActorSrcGen;" };
        for (var i = 0; i < count; i++)
        {
            lines.Add($@"[Actor]
public partial class ParallelActor{i}
{{
    [FirstStep]
    public int Step{i}(int value) => value;
}}");
        }
        return string.Join('\n', lines);
    }

    [Fact]
    public async Task Generate_ParallelCompilations_NoRaceConditions()
    {
        const int actorCount = 10;
        var source = BuildActors(actorCount);

        var tasks = Enumerable.Range(0, actorCount).Select(_ => Task.Run(() =>
        {
            var compilation = CompilationHelper.CreateCompilation(source);
            var driver = CompilationHelper.CreateGeneratorDriver(compilation);
            var output = CompilationHelper.GetGeneratedOutput(driver);
            return output.Count;
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, count => Assert.Equal(actorCount, count));
    }

    [Fact]
    public async Task Generate_SharedSymbols_IndependentResults()
    {
        const int actorCount = 8;
        var source = BuildActors(actorCount);
        var compilation = CompilationHelper.CreateCompilation(source);

        var results = new ConcurrentBag<Dictionary<string, string>>();

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            var driver = CompilationHelper.CreateGeneratorDriver(compilation);
            var output = CompilationHelper.GetGeneratedOutput(driver);
            results.Add(output);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(8, results.Count);
        var first = results.First();
        Assert.All(results, r => Assert.Equal(first.Count, r.Count));
    }

    [Fact]
    public async Task VisitActor_Parallel_AllProduceValidResults()
    {
        const int actorCount = 12;
        var source = BuildActors(actorCount);
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var classes = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().ToArray();

        var visitor = new ActorVisitor();
        var results = new ConcurrentBag<VisitorResult>();

        await Parallel.ForEachAsync(classes, async (classSyntax, _) =>
        {
            var symbol = model.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;
            if (symbol is null)
            {
                return;
            }

            var sas = new ActorSrcGen.Helpers.SyntaxAndSymbol(classSyntax, symbol, model);
            var result = visitor.VisitActor(sas);
            results.Add(result);
            await Task.CompletedTask;
        });

        Assert.Equal(actorCount, results.Count);
        Assert.All(results, r => Assert.Single(r.Actors));
        Assert.All(results, r => Assert.Empty(r.Diagnostics));
    }
}
