using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ActorSrcGen.Model;
using ActorSrcGen.Helpers;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class ActorVisitorThreadSafetyTests
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
    public async Task VisitActor_ConcurrentCalls_ProduceIndependentResults()
    {
        var visitor = new ActorVisitor();
        var inputs = Enumerable.Range(0, 20)
            .Select(i => CreateActor($@"using ActorSrcGen;
public partial class Actor{i}
{{
    [FirstStep]
    public int Step{i}(int value) => value;
}}"))
            .ToArray();

        var results = new ConcurrentBag<VisitorResult>();

        await Parallel.ForEachAsync(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (input, _) =>
        {
            var result = visitor.VisitActor(input);
            results.Add(result);
            return ValueTask.CompletedTask;
        });

        Assert.Equal(inputs.Length, results.Count);
        Assert.All(results, r => Assert.Single(r.Actors));
        Assert.All(results, r => Assert.Empty(r.Diagnostics));

        var actorNames = results.SelectMany(r => r.Actors.Select(a => a.Name)).ToArray();
        Assert.Equal(inputs.Length, actorNames.Distinct().Count());
    }
}
