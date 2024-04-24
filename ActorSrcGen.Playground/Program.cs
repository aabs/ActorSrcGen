using ActorSrcGen.Abstractions.Playground;

Console.WriteLine("Hello World");

var wf = new MyWorkflow();
if (await wf.Cast(10))
    Console.WriteLine("Called Asynchronously");

var wf2 = new MyActor();
if (wf2.Call(10))
    Console.WriteLine("Called Synchronously");

await wf2.CompletionTask;