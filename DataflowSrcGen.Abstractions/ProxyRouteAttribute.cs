namespace DataflowSrcGen;

/// <summary>
/// Code generation for method based routing
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ProxyRouteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyRouteAttribute"/> class.
    /// </summary>
    /// <param name="template">The template, if null will use a convention</param>
    public ProxyRouteAttribute(string template = null)
    {
        Template = template;
    }

    public string Template { get; }

    public HttpVerb Verb { get; set; } = HttpVerb.Unknown;
}
