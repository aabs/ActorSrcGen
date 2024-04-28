namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class NextStepAttribute : Attribute
{
    public NextStepAttribute(string next)
    {
        NextStep = next;
    }

    public string NextStep { get; }
}
