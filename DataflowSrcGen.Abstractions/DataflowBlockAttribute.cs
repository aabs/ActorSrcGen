namespace DataflowSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class DataflowBlockAttribute : Attribute
{
    public DataflowBlockAttribute(int maxDegreeOfParallelism)
    {
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public int MaxDegreeOfParallelism { get; }
}
