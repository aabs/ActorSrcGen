namespace DataflowSrcGen.Abstractions.Playground;

[Actor]
public partial class MyWorkflow
{
    [InitialStep(next: "DoTask2")]
    public Task<string> DoTask1(int x)
    {
        return Task.FromResult(x.ToString());
    }

    [Step(next: "DoTask3")]
    public Task<string> DoTask2(string x)
    {
        return Task.FromResult($"100{x}");
    }

    [LastStep]
    public Task<int> DoTask3(string input)
    {
        return Task.FromResult(int.Parse(input));
    }
}