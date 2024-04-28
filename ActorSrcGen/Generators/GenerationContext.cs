#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

using ActorSrcGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen;

public record struct GenerationContext(SyntaxAndSymbol ActorClass,
                                       IEnumerable<IMethodSymbol> StartMethods,
                                       IEnumerable<IMethodSymbol> EndMethods,
                                       Dictionary<IMethodSymbol, List<IMethodSymbol>> DependencyGraph)
{
    public bool HasSingleInputType => InputTypeNames.Distinct().Count() == 1;
    public bool HasMultipleInputTypes => InputTypeNames.Distinct().Count() > 1;
    public bool HasAnyInputTypes => InputTypeNames.Distinct().Count() > 0;
    public bool HasDisjointInputTypes => InputTypeNames.Distinct().Count() == InputTypeNames.Count();

    public bool HasSingleOutputType => OutputMethods.Count() == 1;
    public bool HasMultipleOutputTypes => OutputMethods.Count() > 1;
    public IEnumerable<IMethodSymbol> OutputMethods => EndMethods.Where(s => !s.ReturnsVoid);
    public string Name => ActorClass.Symbol.Name;
    public readonly IEnumerable<string> InputTypeNames
    {
        get
        {
            foreach (var fm in StartMethods)
            {
                if (fm != null)
                {
                    yield return fm!.Parameters.First()!.Type.Name;
                }
            }
        }
    }
    public readonly IEnumerable<string> OutputTypeNames
    {
        get
        {
            foreach (var fm in EndMethods)
            {
                if (fm != null)
                {
                    ITypeSymbol returnType = fm.ReturnType;
                    // extract the underlying return type for async methods if necessary
                    if (returnType.Name == "Task")
                    {
                        if (returnType is INamedTypeSymbol nts)
                        {
                            yield return nts.TypeArguments[0].RenderTypename();
                        }
                        yield return returnType.RenderTypename();
                    }
                    yield return fm!.ReturnType.RenderTypename();
                }
            }
        }
    }
}
