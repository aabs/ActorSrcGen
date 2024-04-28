namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class FirstStepAttribute : StepAttribute
{
    public FirstStepAttribute(string inputName, int maxDegreeOfParallelism = 0, int maxBufferSize = 0)
        : base(maxDegreeOfParallelism, maxBufferSize)
    {
        InputName = inputName;
    }

    public string InputName { get; }
}