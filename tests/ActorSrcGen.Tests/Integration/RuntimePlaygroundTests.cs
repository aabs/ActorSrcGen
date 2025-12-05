using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen.Abstractions.Playground;
using Xunit;

namespace ActorSrcGen.Tests.Integration;

public class RuntimePlaygroundTests
{
    [Fact]
    public async Task MyPipeline_runs_and_returns_result()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = new MyPipeline();

        var posted = pipeline.Call("{ \"something\": \"here\" }");
        Assert.True(posted);

        var result = await pipeline.AcceptAsync(cts.Token);
        Assert.True(result);

        pipeline.Complete();
        await pipeline.SignalAndWaitForCompletionAsync();
    }
}
