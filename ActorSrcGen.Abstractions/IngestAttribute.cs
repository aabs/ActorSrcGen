[AttributeUsage(AttributeTargets.Method)]
public class IngestAttribute : Attribute
{
    public IngestAttribute(int priority)
    {
        Priority = priority;
    }
    public int Priority { get; set; }
}