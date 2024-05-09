namespace ActorSrcGen.Abstractions.Playground;

[Actor]
public partial class MyActor
{
    public List<int> Results { get; set; } = [];
    public int Counter { get; set; }

    [FirstStep("blah")]
    [Receiver]
    [NextStep(nameof(DoTask2))]
    [NextStep(nameof(LogMessage))]
    public Task<string> DoTask1(int x)
    {
        Console.WriteLine("DoTask1");

        return Task.FromResult(x.ToString());
    }

    protected async partial Task<int> ReceiveDoTask1(CancellationToken ct)
    {
        await Task.Delay(1000, ct);

        return Counter++;
    }


    [Step]
    [NextStep(nameof(DoTask3))]
    public Task<string> DoTask2(string x)
    {
        Console.WriteLine("DoTask2");

        return Task.FromResult($"100{x}");
    }

    [LastStep]
    public async Task<int> DoTask3(string input)
    {
        await Console.Out.WriteLineAsync("DoTask3");
        var result = int.Parse(input);
        Results.Add(result);

        return result;
    }

    [LastStep]
    public void LogMessage(string x)
    {
        Console.WriteLine("Incoming Message: " + x);
    }
}