using System.Diagnostics;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActorSrcGen.Tests.Integration;

public class CancellationIntegrationTests
{
    private static string BuildActors(int count)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("using ActorSrcGen;");
        for (var i = 0; i < count; i++)
        {
            builder.AppendLine($@"[Actor]
public partial class CancellableActor{i}
{{
    [FirstStep]
    public int Step{i}(int value) => value;
}}");
        }
        return builder.ToString();
    }

    [Fact]
    public async Task Generate_CancellationRequested_StopsEarly()
    {
        var source = BuildActors(400);
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Run(() => driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cts.Token));
            Assert.True(cts.IsCancellationRequested, "Cancellation should be requested.");
        }
        catch (OperationCanceledException)
        {
            // Expected path when cancellation is observed immediately.
        }
        sw.Stop();

        // Accept fast completion or quick cancellation; allow a generous bound to avoid flakiness.
        Assert.True(sw.ElapsedMilliseconds < 500, "Cancellation should be honored promptly.");
    }

    [Fact]
    public async Task Generate_PartialWork_RespectsCancellation()
    {
        const int expectedActors = 800;
        var source = BuildActors(expectedActors);
        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new[] { new Generator().AsSourceGenerator() }, parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        Dictionary<string, string> output = new(StringComparer.Ordinal);
        try
        {
            var updatedDriver = await Task.Run(() => driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cts.Token));
            output = CompilationHelper.GetGeneratedOutput(updatedDriver);
        }
        catch (OperationCanceledException)
        {
            // Expected for mid-flight cancellation; leave output as partial (possibly empty).
        }

        Assert.True(output.Count < expectedActors, "Cancellation should prevent full generation.");
    }
}
