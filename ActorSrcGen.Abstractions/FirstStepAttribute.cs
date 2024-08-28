namespace ActorSrcGen;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class FirstStepAttribute : StepAttribute
{
    public FirstStepAttribute(string inputName, int maxDegreeOfParallelism = 4, int maxBufferSize = 1)
        : base(maxDegreeOfParallelism, maxBufferSize)
    {
        InputName = inputName;
    }

    public string InputName { get; }
}