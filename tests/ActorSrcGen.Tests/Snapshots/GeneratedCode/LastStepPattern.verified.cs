#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Threading.Tasks;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class LastStepPatternActor : Dataflow<string, int>, IActor<string>
{
    public LastStepPatternActor() : base(DataflowOptions.Default)
    {
        _Begin = new TransformBlock<string,int>((string x) => Begin(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Begin);
        _Complete = new TransformBlock<int,int>(async (int x) => await Complete(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Complete);
        _Begin.LinkTo(_Complete, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<string,int> _Begin;

    TransformBlock<int,int> _Complete;
    public override ITargetBlock<string> InputBlock { get => _Begin; }
    public override ISourceBlock<int> OutputBlock { get => _Complete; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
    public async Task<int> AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _Complete.ReceiveAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            return await Task.FromCanceled<int>(cancellationToken);
        }
    }
}
