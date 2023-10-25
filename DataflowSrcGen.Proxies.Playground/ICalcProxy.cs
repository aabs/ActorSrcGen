namespace DataflowSrcGen.Abstractions.Playground;

[Proxy("calc")]
public interface ICalcProxy
{
    [ProxyRoute]
    Task<int> GetAsync();

    [ProxyRoute("add", Verb = HttpVerb.POST)]
    Task<int> AppendAsync(Payload payload);

    [ProxyRoute("subtract", Verb = HttpVerb.POST)]
    Task<int> SubtractAsync(Payload payload);
}