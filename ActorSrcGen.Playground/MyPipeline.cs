namespace ActorSrcGen.Abstractions.Playground;

using TResponse = Context<PollRequest, TelemetryResponse>;
using TRequest = Context<PollRequest, PollRequest>;
using Microsoft.Extensions.Logging;

[Actor]
public partial class MyPipeline
{
    partial void LogMessage(LogLevel level, string message, params object[] args);
    private int counter = 0;
    partial void LogMessage(LogLevel level, string message, params object[] args) => Console.WriteLine(message);


    [Ingest(1)]
    [NextStep(nameof(DecodePollRequest))]
    public async Task<string> ReceivePollRequest(CancellationToken cancellationToken)
    {
        if (++counter % 3 != 0)
        {
            return null;
        }
        await Task.Delay(250);
        return nameof(ReceivePollRequest) + Guid.NewGuid();
    }

    [Ingest(3)]
    [NextStep(nameof(DecodePollRequest))]
    public async Task<string> ReceiveFcasRequest(CancellationToken cancellationToken)
    {
        if (++counter % 5 != 0)
        {
            return null;
        }        await Task.Delay(250);
        return nameof(ReceiveFcasRequest) + Guid.NewGuid();
    }

    [Ingest(2)]
    [NextStep(nameof(DecodePollRequest))]
    public async Task<string> ReceiveBackfillRequest(CancellationToken cancellationToken)
    {
        if (++counter % 7 != 0)
        {
            return null;
        }        await Task.Delay(250);
        return nameof(ReceiveBackfillRequest) + Guid.NewGuid();
    }

    // decode
    [FirstStep("decode poll request", 8, 1)]
    [NextStep(nameof(SetupGapTracking))]
    [NextStep(nameof(LogIncomingPollRequest))]
    public TRequest DecodePollRequest(string x)
    {
        Console.WriteLine(nameof(DecodePollRequest));
        var pollRequest = new PollRequest(Guid.NewGuid().ToString(), x);
        return new TRequest(pollRequest,pollRequest, []);
    }

    [Step]
    [NextStep(nameof(SplitRequestBySignal))]
    public TRequest SetupGapTracking(TRequest x)
    {
        Console.WriteLine(nameof(SetupGapTracking));
        return x;
    }

    [Step]
    [NextStep(nameof(PollForMetrics))]
    public IEnumerable<TRequest> SplitRequestBySignal(TRequest input)
    {
        Console.WriteLine(nameof(SplitRequestBySignal));
        yield return input;
    }

    [Step]
    [NextStep(nameof(EncodeResult))]
    public TResponse PollForMetrics(TRequest x)
    {
        Console.WriteLine(nameof(PollForMetrics));
        return new TResponse(x.OriginalRequest, new TelemetryResponse(x.OriginalRequest.Id, "somesig", []), []);
    }

    // encode results

    [Step]
    [NextStep(nameof(DeliverResults))]
    public TResponse EncodeResult(TResponse x)
    {
        Console.WriteLine(nameof(EncodeResult));
        return x;
    }
    // deliver results

    [Step]
    [NextStep(nameof(TrackTelemetryGaps))]
    public TResponse DeliverResults(TResponse x)
    {
        Console.WriteLine(nameof(DeliverResults));
        return x;
    }
    // track gaps

    [LastStep]
    public bool TrackTelemetryGaps(TResponse x)
    {
        Console.WriteLine(nameof(TrackTelemetryGaps));
        return true;
    }

    [LastStep]
    public void LogIncomingPollRequest(TRequest x)
    {
        Console.WriteLine("Incoming Poll Request: " + x.OriginalRequest.Name);
    }

}