#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Threading.Tasks;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class SingleInputOutputActor : Dataflow<string>, IActor<string>
{
    public SingleInputOutputActor() : base(DataflowOptions.Default)
    {
        _Process = new TransformBlock<string,string>((string x) => Process(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Process);
        _Start = new TransformBlock<string,string>((string x) => Start(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Start);
        _Start.LinkTo(_Process, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<string,string> _Process;

    TransformBlock<string,string> _Start;
    public override ITargetBlock<string> InputBlock { get => _Start; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
}
