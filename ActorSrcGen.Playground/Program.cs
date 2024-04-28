using ActorSrcGen.Abstractions.Playground;

Console.WriteLine("Hello World");

var actor = new MyActor();
if (actor.Call(10))
    Console.WriteLine("Called Synchronously");

await actor.SignalAndWaitForCompletionAsync();

var result = await actor.ReceiveDoTask3Async(CancellationToken.None);

Console.WriteLine($"Result: {result}");