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
        Console.WriteLine(nameof(DecodePollRequest));
        var pollRequest = new PollRequest(Guid.NewGuid().ToString(), "x");
        return new TRequest(pollRequest,pollRequest, []);
    }

    protected partial async Task<string> ReceiveDecodePollRequest(CancellationToken cancellationToken)
    {
        await Console.Out.WriteLineAsync(nameof(ReceiveDecodePollRequest));
        await Task.Delay(250);
        return Guid.NewGuid().ToString();
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