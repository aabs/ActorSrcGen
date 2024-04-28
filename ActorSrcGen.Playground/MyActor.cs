namespace ActorSrcGen.Abstractions.Playground;

[Actor]
public partial class MyActor
{
    public List<int> Results { get; set; } = [];

    [FirstStep("blah"), NextStep(nameof(DoTask2)), NextStep(nameof(LogMessage))]
    public Task<string> DoTask1(int x)
    {
        Console.WriteLine("DoTask1");
        return Task.FromResult(x.ToString());
    }

    [Step, NextStep(nameof(DoTask3))]
    public Task<string> DoTask2(string x)
    {
        Console.WriteLine("DoTask2");
        return Task.FromResult($"100{x}");
    }

    [LastStep]
    public void DoTask3(string input)
    {
        Console.WriteLine("DoTask3");
        int result = int.Parse(input);
        Results.Add(result);
    }

    [LastStep]
    public void LogMessage(string x)
    {
        Console.WriteLine("Incoming Message: " + x);
    }
}