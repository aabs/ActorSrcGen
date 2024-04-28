namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class LastStepAttribute : StepAttribute
{
    public LastStepAttribute(int maxDegreeOfParallelism = 0, int maxBufferSize = 0) 
    {
    }
}