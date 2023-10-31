namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public class StepAttribute : Attribute
{
    public StepAttribute(string next = null, int maxDegreeOfParallelism = 0)
    {
        NextStep = next;
        MaxDegreeOfParallelism = maxDegreeOfParallelism < 0 ? Environment.ProcessorCount : maxDegreeOfParallelism;
    }

    public bool IsEndBlock { get; set; }
    public bool IsStartBlock { get; set; }
    public int MaxDegreeOfParallelism { get; }
    public string NextStep { get; }
}
