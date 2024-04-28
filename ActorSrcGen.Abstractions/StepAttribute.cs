namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class StepAttribute : Attribute
{
    public StepAttribute(int maxDegreeOfParallelism = 0, int maxBufferSize = 0)
    {
        MaxBufferSize = maxBufferSize;
        MaxDegreeOfParallelism = maxDegreeOfParallelism < 0 ? Environment.ProcessorCount : maxDegreeOfParallelism;
    }

    public int MaxDegreeOfParallelism { get; }
    public int MaxBufferSize { get; }
}
