namespace ActorSrcGen.Abstractions.Playground;

[Actor]
public partial class MyActor
{
    public List<int> Results { get; set; } = [];
    [InitialStep(next: "DoTask2")]
    public Task<string> DoTask1(int x)
    {
        Console.WriteLine("DoTask1");
        return Task.FromResult(x.ToString());
    }

    [Step(next: "DoTask3")]
    public Task<string> DoTask2(string x)
    {
        Console.WriteLine("DoTask2");
        return Task.FromResult($"100{x}");
    }

    [LastStep]
    public Task<int> DoTask3(string input)
    {
        Console.WriteLine("DoTask3");
        int result = int.Parse(input);
        Results.Add(result);
        return Task.FromResult(result);
    }
}