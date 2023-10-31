namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class LastStepAttribute : StepAttribute
{
    public LastStepAttribute(int maxDegreeOfParallelism = 0) : base(null, maxDegreeOfParallelism)
    {
        IsEndBlock = true;
    }
}