using Gridsum.DataflowEx;
using Microsoft.Extensions.Logging;

namespace ActorSrcGen.Abstractions.Playground;

public record Context<TRequestType, TPayloadType>(TRequestType OriginalRequest,
    TPayloadType Data,
    Stack<IDisposable> Activities);

public record PollRequest(string Id, string Name);

public record DataPoint(DateTimeOffset Timestamp, double Value);
public record TelemetryResponse(string Id, string Name, DataPoint[] Result);