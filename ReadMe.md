# Welcome to ActorSrcGen 
 
ActorSrcGen is a C# Source Generator allowing the conversion of simple Actor
based C# POCOs into Dataflow compatible classes supporting the actor model.

## Where to get it

The source generator can be installed using nuget at
[ActorSrcGen](https://www.nuget.org/packages/ActorSrcGen).

**NB. This library is only days old, and will probably change significantly in
the weeks ahead.  Please treat it as experimental for the time being.**

If you notice any issues with the generated code please report them [on
Github](https://github.com/aabs/ActorSrcGen/issues).

## What it does

The aim of the source generator is to help you simplify your code.  It does that
by generating the boilerplate code needed to use TPL Dataflow with a regular
class. So you write a simple *partial* class like this:

```csharp
[Actor]
public partial class MyWorkflow
{
    [InitialStep(next: "DoTask2")]
    public Task<string> DoTask1(int x)
    {
        Console.WriteLine("DoTask1");
        return Task.FromResult(x.ToString());
    }

    [Step(next: "DoTask3")]
    public Task<string> DoTask2(string x)
    {
        Console.WriteLine("DoTask2");
        return Task.FromResult($"100{x}");
    }

    [LastStep]
    public Task<int> DoTask3(string input)
    {
        Console.WriteLine("DoTask3");
        return Task.FromResult(int.Parse(input));
    }
}
```

And the source generator will extend it, adding the TPL Dataflow code to wire the methods together:

```csharp
namespace ActorSrcGen.Abstractions.Playground;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;

public partial class MyWorkflow : Dataflow<Int32, Int32>
{

    public MyWorkflow() : base(DataflowOptions.Default)
    {
        _DoTask1 = new TransformBlock<Int32, String>(DoTask1,
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_DoTask1);
        _DoTask2 = new TransformBlock<String, String>(DoTask2,
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_DoTask2);
        _DoTask3 = new TransformBlock<String, Int32>(DoTask3,
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_DoTask3);
        _DoTask1.LinkTo(_DoTask2, new DataflowLinkOptions { PropagateCompletion = true });
        _DoTask2.LinkTo(_DoTask3, new DataflowLinkOptions { PropagateCompletion = true });
    }
    TransformBlock<Int32,String> _DoTask1;

    TransformBlock<String,String> _DoTask2;

    TransformBlock<String,Int32> _DoTask3;

    public override ITargetBlock<Int32> InputBlock { get => _DoTask1; }
    public override ISourceBlock<Int32> OutputBlock { get => _DoTask3; }

    public async Task<Int32> Post(Int32 input)
    {
        InputBlock.Post(input);
        return await OutputBlock.ReceiveAsync();
    }

}
```

Invocation of your class is a straightforward call to post a message to it:

```csharp
var wf = new MyWorkflow();
await wf.Post(10);
```

## Why Bother?

Writing robust and performant asynchronous and concurrent code in .NET is a
laborious process. TPL Dataflow makes it easier -  it "*provides dataflow
components to help increase the robustness of concurrency-enabled applications.
This dataflow model promotes actor-based programming by providing in-process
message passing for coarse-grained dataflow and pipelining tasks*" (see
[docs](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library)).
This source generator allows you to take advantage of that model without needing
to write a lot of the necessary boilerplate code.

The generated source builds atop
[DataflowEx](https://github.com/gridsum/DataflowEx) for a clean stateful
object-oriented wrapper around your pipeline.

With thanks to:

- [DataflowEx](https://github.com/gridsum/DataflowEx)
- [Bnaya.SourceGenerator.Template](https://github.com/bnayae/Bnaya.SourceGenerator.Template) (see [article](https://blog.stackademic.com/source-code-generators-diy-f04229c59e1a))
