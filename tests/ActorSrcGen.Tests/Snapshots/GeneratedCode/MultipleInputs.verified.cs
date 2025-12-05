#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Threading.Tasks;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class MultiInputActor : Dataflow, IActor<int, string>
{
    public MultiInputActor() : base(DataflowOptions.Default)
    {
        _FromNumber = new TransformBlock<int,string>((int x) => FromNumber(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_FromNumber);
        _FromString = new TransformBlock<string,string>((string x) => FromString(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_FromString);
        _Merge = new TransformBlock<string,string>((string x) => Merge(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Merge);
        _FromNumber.LinkTo(_Merge, new DataflowLinkOptions { PropagateCompletion = true });
        _FromString.LinkTo(_Merge, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<int,string> _FromNumber;

    TransformBlock<string,string> _FromString;

    TransformBlock<string,string> _Merge;
    public ITargetBlock<int> FromNumberInputBlock { get => _FromNumber; }
    public ITargetBlock<string> FromStringInputBlock { get => _FromString; }
    public override ISourceBlock<string> OutputBlock { get => _Merge; }
    public bool CallFromNumber(int input)
        => FromNumberInputBlock.Post(input);

    public async Task<bool> CastFromNumber(int input)
        => await FromNumberInputBlock.SendAsync(input);
    public bool CallFromString(string input)
        => FromStringInputBlock.Post(input);

    public async Task<bool> CastFromString(string input)
        => await FromStringInputBlock.SendAsync(input);
    public async Task<string> AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _Merge.ReceiveAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            return await Task.FromCanceled<string>(cancellationToken);
        }
    }
}
