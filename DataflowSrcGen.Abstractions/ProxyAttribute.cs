namespace DataflowSrcGen;

/// <summary>
/// Code generation for building a typed proxy
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public class ProxyAttribute : Attribute
{
    public ProxyAttribute(string template)
    {
        Template = template;
    }

    public string Template { get; }
}
