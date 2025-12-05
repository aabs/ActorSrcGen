#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

using System.Collections.Generic;
using System.Linq;
using System.Text;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen;

/// <summary>
/// Immutable generation snapshot used during code emission; safe for concurrent access.
/// </summary>
public readonly record struct GenerationContext(SyntaxAndSymbol ActorClass,
                                       IReadOnlyList<IMethodSymbol> StartMethods,
                                       IReadOnlyList<IMethodSymbol> EndMethods,
                                       IReadOnlyDictionary<IMethodSymbol, IReadOnlyList<IMethodSymbol>> DependencyGraph)
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

public class ActorGenerationContext
{
    public ActorGenerationContext(ActorNode actor, System.Text.StringBuilder builder, SourceProductionContext srcGenCtx)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        SrcGenCtx = srcGenCtx;
    }
    public bool HasSingleInputType => InputTypeNames.Distinct().Count() == 1;
    public bool HasMultipleInputTypes => InputTypeNames.Distinct().Count() > 1;
    public bool HasAnyInputTypes => InputTypeNames.Distinct().Count() > 0;
    public bool HasDisjointInputTypes => InputTypeNames.Distinct().Count() == InputTypeNames.Count();

    public bool HasSingleOutputType => OutputMethods.Count() == 1;
    public bool HasMultipleOutputTypes => OutputMethods.Count() > 1;
    public IEnumerable<IMethodSymbol> OutputMethods => Actor.ExitNodes.Select(n => n.Method).Where(s => !s.ReturnsVoid);
    public string Name => Actor.TypeSymbol.Name;
    public IEnumerable<string> InputTypeNames
    {
        get
        {
            return Actor.EntryNodes.Select(n => n.InputTypeName);
        }
    }
    public IEnumerable<string> OutputTypeNames
    {
        get
        {
            foreach (var fm in Actor.ExitNodes)
            {
                if (fm != null)
                {
                    ITypeSymbol returnType = fm.Method.ReturnType;
                    // extract the underlying return type for async methods if necessary
                    if (returnType.Name == "Task")
                    {
                        if (returnType is INamedTypeSymbol nts)
                        {
                            yield return nts.TypeArguments[0].RenderTypename();
                        }
                        yield return returnType.RenderTypename();
                    }
                    yield return fm.Method.ReturnType.RenderTypename();
            }
        }
    }
}

    public ActorNode Actor { get; }
    public StringBuilder Builder { get; }
    public SourceProductionContext SrcGenCtx { get; }
}

