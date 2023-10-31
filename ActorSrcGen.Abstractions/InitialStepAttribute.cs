namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class InitialStepAttribute : StepAttribute
{
    public InitialStepAttribute(string next = null, int maxDegreeOfParallelism = 0) : base(next, maxDegreeOfParallelism)
    {
        IsStartBlock = true;
    }
}
