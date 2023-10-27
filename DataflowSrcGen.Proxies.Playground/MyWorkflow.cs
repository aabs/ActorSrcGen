namespace DataflowSrcGen.Abstractions.Playground;

[Actor("dummy")]
public partial class MyWorkflow
{
    [DataflowBlock(4, start: true)]
    public Task<string> DoTask1(int x)
    {
        return Task.FromResult("hello world");
    }

    [DataflowBlock(4, end: true)]
    public Task<int> DoTask2(string input)
    {
        return Task.FromResult(int.Parse(input));
    }
}