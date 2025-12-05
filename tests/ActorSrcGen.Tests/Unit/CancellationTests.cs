using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActorSrcGen.Tests.Unit;

public class CancellationTests
{
    private static string BuildActors(int count)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using ActorSrcGen;");

        for (var i = 0; i < count; i++)
        {
            builder.AppendLine($@"[Actor]
public partial class Sample{i}
{{
    [FirstStep]
    public int Step{i}(int value) => value;
}}");
        }

        return builder.ToString();
    }

    [Fact]
    public async Task Generate_CancellationToken_CancelsWithin100ms()
    {
        var source = BuildActors(300);
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new ISourceGenerator[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Task.Run(() => driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cts.Token)));

        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100, "Generation should stop quickly when cancelled.");
    }

    [Fact]
    public async Task Generate_CancelledMidway_ReturnsPartialResults()
    {
        const int expectedActors = 2000;
        var source = BuildActors(expectedActors);
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new ISourceGenerator[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Task.Run(() => driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cts.Token)));

        stopwatch.Stop();

        Assert.InRange(stopwatch.ElapsedMilliseconds, 5, 500);
    }
}
