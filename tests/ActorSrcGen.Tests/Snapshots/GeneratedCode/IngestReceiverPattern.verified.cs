#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS0108 // hides inherited member.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen;
namespace ActorSrcGen.Generated.Tests;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
public partial class IngestReceiverActor : Dataflow<string, string>, IActor<string>
{
    public IngestReceiverActor() : base(DataflowOptions.Default)
    {
        _Finish = new TransformBlock<string,string>(async (string x) => await Finish(x),
            new ExecutionDataflowBlockOptions() {
                BoundedCapacity = 5,
                MaxDegreeOfParallelism = 8
        });
        RegisterChild(_Finish);
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
        _Process.LinkTo(_Finish, new DataflowLinkOptions { PropagateCompletion = true });
        _Start.LinkTo(_Process, new DataflowLinkOptions { PropagateCompletion = true });
    }

    TransformBlock<string,string> _Finish;

    TransformBlock<string,string> _Process;

    TransformBlock<string,string> _Start;
    protected partial Task<string> ReceiveStart(CancellationToken cancellationToken);
    public async Task ListenForReceiveStart(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string incomingValue = await ReceiveStart(cancellationToken);
            Call(incomingValue);
        }
    }
    public override ITargetBlock<string> InputBlock { get => _Start; }
    public override ISourceBlock<string> OutputBlock { get => _Finish; }
    public bool Call(string input)
        => InputBlock.Post(input);

    public async Task<bool> Cast(string input)
        => await InputBlock.SendAsync(input);
    public async Task Ingest(CancellationToken cancellationToken)
    {
        var ingestTasks = new List<Task>();
        ingestTasks.Add(Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await PullAsync(cancellationToken);
                if (result is not null)
                {
                    Call(result);
                }
            }
        }, cancellationToken));
        ingestTasks.Add(Task.Run(async () =>
        {
            await foreach (var result in StreamAsync(cancellationToken))
            {
                if (result is not null)
                {
                    Call(result);
                }
            }
        }, cancellationToken));
        await Task.WhenAll(ingestTasks);
    }
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
