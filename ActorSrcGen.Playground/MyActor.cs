using Gridsum.DataflowEx;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActorSrcGen.Abstractions.Playground;

public record Context<TRequestType, TPayloadType>(TRequestType OriginalRequest,
    TPayloadType Data,
    Stack<IDisposable> Activities);

public record PollRequest(string Id, string Name);
public record PollResults(string Id, double[] Values);

public record DataPoint(DateTimeOffset Timestamp, double Value);
public record TelemetryResponse(string Id, string Name, DataPoint[] Result);


[Actor]
public partial class MyActor
{
    partial void LogMessage(LogLevel level, string message, params object[] args);

    partial void LogMessage(LogLevel level, string message, params object[] args)
    {
        Console.WriteLine($"[{level}] {string.Format(message, args)}");
    }

    [Ingest(1)]
    [NextStep(nameof(DecodeRequest))]
    public async Task<string> ReceivePollRequest(CancellationToken cancellationToken)
    {
        return await GetTheNextRequest();
    }

    [FirstStep("decode incoming poll request")]
    [NextStep(nameof(ActOnTheRequest))]
    public PollRequest DecodeRequest(string json)
    {
        Console.WriteLine(nameof(DecodeRequest));
        var pollRequest = JsonSerializer.Deserialize<PollRequest>(json);
        return pollRequest;
    }

    [Step]
    [NextStep(nameof(DeliverResults))]
    public PollResults ActOnTheRequest(PollRequest req)
    {
        Console.WriteLine(nameof(ActOnTheRequest));
        var result = GetTheResults(req.Id);
        return result;
    }

    [LastStep]
    public bool DeliverResults(PollResults res)
    {
        return TryPush(res);
    }

    private bool TryPush(PollResults res)
    {
        return true;
    }

    private async Task<string> GetTheNextRequest()
    {
        return """
               {"property":  "value"}
               """;
    }

    private PollResults GetTheResults(string id)
    {
        return new PollResults(id, [1,2,3,4,5]);
    }
}