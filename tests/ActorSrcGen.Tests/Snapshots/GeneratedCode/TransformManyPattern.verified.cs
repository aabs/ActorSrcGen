#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Collections.Generic;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class TransformManyActor : Dataflow<string, string>, IActor<string>
{
    public TransformManyActor() : base(DataflowOptions.Default)
    {
        _Finish = new TransformBlock<string,string>((string x) => Finish(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Finish);
        _Start = new TransformManyBlock<string,string>((string x) => Start(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Start);
        _Start.LinkTo(_Finish, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<string,string> _Finish;

    TransformManyBlock<string,string> _Start;
    public override ITargetBlock<string> InputBlock { get => _Start; }
    public override ISourceBlock<string> OutputBlock { get => _Finish; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
    public async Task<string> AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _Finish.ReceiveAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            return await Task.FromCanceled<string>(cancellationToken);
        }
    }
}
