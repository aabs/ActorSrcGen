# Welcome to ActorSrcGen 
 
ActorSrcGen is a C# Source Generator allowing the conversion of simple Actor
based C# POCOs into Dataflow compatible classes supporting the actor model.

**NB. This library is brand new, and is likely to change substantially in the weeks ahead.  Please treat this as experimental for the time being.**

The aim of the source generator is to generate the boilerplate code needed to
use TPL Dataflow with a regular class. So you can take something like this:

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

And add something like this to it at compile time:

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


With thanks to:

- [DataflowEx](https://github.com/gridsum/DataflowEx)
- [Bnaya.SourceGenerator.Template](https://github.com/bnayae/Bnaya.SourceGenerator.Template) (see [article](https://blog.stackademic.com/source-code-generators-diy-f04229c59e1a))
