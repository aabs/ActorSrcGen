#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Threading.Tasks;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class FirstStepPatternActor : Dataflow<string, int>, IActor<string>
{
    public FirstStepPatternActor() : base(DataflowOptions.Default)
    {
        _Finish = new TransformBlock<int,int>((int x) => Finish(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Finish);
        _Process = new TransformBlock<int,int>((int x) => Process(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Process);
        _Start = new TransformBlock<string,int>((string x) => Start(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Start);
        _Process.LinkTo(_Finish, new DataflowLinkOptions { PropagateCompletion = true });
        _Start.LinkTo(_Process, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<int,int> _Finish;

    TransformBlock<int,int> _Process;

    TransformBlock<string,int> _Start;
    public override ITargetBlock<string> InputBlock { get => _Start; }
    public override ISourceBlock<int> OutputBlock { get => _Finish; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
    public async Task<int> AcceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _Finish.ReceiveAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            return await Task.FromCanceled<int>(cancellationToken);
        }
    }
}
