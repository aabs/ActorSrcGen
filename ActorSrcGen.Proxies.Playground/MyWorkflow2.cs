namespace ActorSrcGen.Abstractions.Playground;

[Actor]
public partial class MyWorkflow2
{
    [InitialStep(next: "DoTask2")]
    public string DoTask1(int x)
    {
        Console.WriteLine("DoTask1");
        return x.ToString();
    }

    [Step(next: "DoTask3")]
    public string DoTask2(string x)
    {
        Console.WriteLine("DoTask2");
        return $"100{x}";
    }

    [LastStep]
    public void DoTask3(string input)
    {
        Console.WriteLine("DoTask3");
    }
}