#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using ActorSrcGen;
namespace <global namespace>;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class SimpleActor : Dataflow<string, string>, IActor<string>
{
    public SimpleActor() : base(DataflowOptions.Default)
    {
        _End = new TransformBlock<string,string>((string x) => End(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_End);
        _Start = new TransformBlock<string,string>((string x) => Start(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Start);
        _Start.LinkTo(_End, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<string,string> _End;

    TransformBlock<string,string> _Start;
    public override ITargetBlock<string> InputBlock { get => _Start; }
    public override ISourceBlock<string> OutputBlock { get => _End; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
    public async Task<string> AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _End.ReceiveAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            return await Task.FromCanceled<string>(cancellationToken);
        }
    }
}