using ActorSrcGen.Abstractions.Playground;

var wf = new MyWorkflow();
if (await wf.Cast(10))
    Console.WriteLine("Called Asynchronously");

var wf2 = new MyWorkflow2();
if (wf2.Call(10))
    Console.WriteLine("Called Synchronously");

await wf2.CompletionTask;