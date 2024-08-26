using System.Diagnostics;
using ActorSrcGen.Abstractions.Playground;

var actor = new MyPipeline();

try
{
    if (actor.Call("""
                   { "something": "here" }
                   """))
        Console.WriteLine("Called Synchronously");

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    var t = Task.Run(async () => await actor.Ingest(cts.Token), cts.Token);

    while (!cts.Token.IsCancellationRequested)
    {
        var result = await actor.AcceptAsync(cts.Token);
        Console.WriteLine($"Result: {result}");
    }

    await t;
    await actor.SignalAndWaitForCompletionAsync();
}
catch (OperationCanceledException _)
{
    Console.WriteLine("All Done!");
}

Debugger.Break();