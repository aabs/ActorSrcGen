using Microsoft.Extensions.Logging;

namespace ActorSrcGen.Abstractions.Playground;

[Actor]
public partial class MyWorkflow
{
    partial void LogMessage(LogLevel level, string message, params object[] args)
    {
        Console.WriteLine(message);
    }

    [FirstStep("TheNumber"), NextStep("DoTask2")]
    public Task<string> DoTask1(int x)
    {
        Console.WriteLine("DoTask1");
        return Task.FromResult(x.ToString());
    }

    [Step, NextStep(next: "DoTask3")]
    public Task<string> DoTask2(string x)
    {
        Console.WriteLine("DoTask2");
        return Task.FromResult($"100{x}");
    }

    [LastStep]
    public Task<int> DoTask3(string input)
    {
        Console.WriteLine("DoTask3");
        return Task.FromResult(int.Parse(input));
    }
} 