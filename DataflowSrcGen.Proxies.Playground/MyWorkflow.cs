namespace DataflowSrcGen.Abstractions.Playground;

[Actor("dummy")]
public partial class MyWorkflow
{
    [DataflowBlock("DoTask2", 4, start: true)]
    public Task<string> DoTask1(int x)
    {
        return Task.FromResult(x.ToString());
    }

    [DataflowBlock("", 4, end: true)]
    public Task<int> DoTask2(string input)
    {
        return Task.FromResult(int.Parse(input));
    }
}