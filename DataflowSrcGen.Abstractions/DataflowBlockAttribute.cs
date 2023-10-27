namespace DataflowSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class DataflowBlockAttribute : Attribute
{
    public DataflowBlockAttribute(string next = null, int maxDegreeOfParallelism=8, bool start = false, bool end = false)
    {
        NextStep = next;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        IsStartBlock = start;
        IsEndBlock = end;
    }

    public string NextStep { get; }
    public int MaxDegreeOfParallelism { get; }
    public bool IsStartBlock { get; set; }
    public bool IsEndBlock { get; set; }
}
