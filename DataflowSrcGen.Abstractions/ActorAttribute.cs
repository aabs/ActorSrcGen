namespace DataflowSrcGen;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class ActorAttribute : Attribute
{
    public ActorAttribute(string template)
    {
    }
}

