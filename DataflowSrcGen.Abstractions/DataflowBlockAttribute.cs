namespace DataflowSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class DataflowBlockAttribute : Attribute
{
    public DataflowBlockAttribute(int maxDegreeOfParallelism, bool start = false, bool end = false)
    {
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        IsStartBlock = start;
        IsEndBlock = end;
    }

    public int MaxDegreeOfParallelism { get; }
    public bool IsStartBlock { get; set; }
    public bool IsEndBlock { get; set; }
}
