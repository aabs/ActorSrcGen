namespace ActorSrcGen.Abstractions.Playground;

using TResponse = Context<PollRequest, TelemetryResponse>;
using TRequest = Context<PollRequest, PollRequest>;

[Actor]
public partial class MyPipeline
{
    // decode
    [FirstStep("decode poll request")]
    [Receiver]
    [NextStep(nameof(SetupGapTracking))]
    [NextStep(nameof(LogIncomingPollRequest))]
    public TRequest DecodePollRequest(string x)
    {
        throw new NotImplementedException();
    }

    protected partial Task<string> ReceiveDecodePollRequest(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [Step]
    [NextStep(nameof(SplitRequestBySignal))]
    public TRequest SetupGapTracking(TRequest x)
    {
        throw new NotImplementedException();
    }

    [NextStep(nameof(PollForMetrics))]
    public List<TRequest> SplitRequestBySignal(TRequest input)
    {
        throw new NotImplementedException();
    }

    [Step]
    [NextStep(nameof(EncodeResult))]
    public TResponse PollForMetrics(TRequest x)
    {
        throw new NotImplementedException();
    }

    // encode results

    [Step]
    [NextStep(nameof(DeliverResults))]
    public TResponse EncodeResult(TResponse x)
    {
        throw new NotImplementedException();
    }
    // deliver results

    [Step]
    [NextStep(nameof(TrackTelemetryGaps))]
    public TResponse DeliverResults(TResponse x)
    {
        throw new NotImplementedException();
    }
    // track gaps

    [LastStep]
    public List<bool> TrackTelemetryGaps(TResponse x)
    {
        throw new NotImplementedException();
    }

    [LastStep]
    public void LogIncomingPollRequest(TRequest x)
    {
        Console.WriteLine("Incoming Poll Request: " + x.OriginalRequest.Name);
    }

}