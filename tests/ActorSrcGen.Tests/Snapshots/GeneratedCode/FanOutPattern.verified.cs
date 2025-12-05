#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Threading.Tasks;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class FanOutActor : Dataflow<string, string>, IActor<string>
{
    public FanOutActor() : base(DataflowOptions.Default)
    {
        _Branch1 = new TransformBlock<string,string>((string x) => Branch1(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Branch1);
        _Branch2 = new TransformBlock<string,string>((string x) => Branch2(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Branch2);
        _Join = new TransformBlock<string,string>((string x) => Join(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Join);
        _Start = new TransformBlock<string,string>((string x) => Start(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Start);
    _StartBC = new BroadcastBlock<string>(x => x);
    RegisterChild(_StartBC);
    _Start.LinkTo(_StartBC, new DataflowLinkOptions { PropagateCompletion = true });
        _Branch1.LinkTo(_Join, new DataflowLinkOptions { PropagateCompletion = true });
        _Branch2.LinkTo(_Join, new DataflowLinkOptions { PropagateCompletion = true });
        _StartBC.LinkTo(_Branch1, new DataflowLinkOptions { PropagateCompletion = true });
        _StartBC.LinkTo(_Branch2, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<string,string> _Branch1;

    TransformBlock<string,string> _Branch2;

    TransformBlock<string,string> _Join;

    TransformBlock<string,string> _Start;

    BroadcastBlock<string> _StartBC;
    public override ITargetBlock<string> InputBlock { get => _Start; }
    public override ISourceBlock<string> OutputBlock { get => _Join; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
    public async Task<string> AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _Join.ReceiveAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            return await Task.FromCanceled<string>(cancellationToken);
        }
    }
}
