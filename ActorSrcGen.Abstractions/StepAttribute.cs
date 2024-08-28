namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class StepAttribute : Attribute
{
    public StepAttribute(int maxDegreeOfParallelism = 4, int maxBufferSize = 1)
    {
        MaxBufferSize = maxBufferSize;
        MaxDegreeOfParallelism = maxDegreeOfParallelism < 0 ? Environment.ProcessorCount : maxDegreeOfParallelism;
    }

    public int MaxDegreeOfParallelism { get; }
    public int MaxBufferSize { get; }
}
