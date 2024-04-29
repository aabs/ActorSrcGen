using ActorSrcGen.Abstractions.Playground;

var actor = new MyActor();

if (actor.Call(10))
    Console.WriteLine("Called Synchronously");

var result = await actor.ReceiveAsync(CancellationToken.None);
Console.WriteLine($"Result: {result}");

await actor.SignalAndWaitForCompletionAsync();
