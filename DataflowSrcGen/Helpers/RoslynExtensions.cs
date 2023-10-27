using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataflowSrcGen.Helpers;

internal static class RoslynExtensions
{
    #region MatchAttribute

    /// <summary>
    /// Check if a type matches an attribute.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="attributeName">Name of the attribute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static bool MatchAttribute(
                            this SyntaxNode node,
                            string attributeName,
                            CancellationToken cancellationToken)
    {
        if (node is TypeDeclarationSyntax type)
            return type.MatchAttribute(attributeName, cancellationToken);
        return false;
    }

    /// <summary>
    /// Check if a type matches an attribute.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="attributeName">Name of the attribute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static bool MatchAttribute(
                            this TypeDeclarationSyntax type,
                            string attributeName,
                            CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return false;

        var (attributeName1, attributeName2) = RefineAttributeNames(attributeName);

        bool hasAttributes = type.AttributeLists.Any
                               (m => m.Attributes.Any(m1 =>
                               {
                                   string name = m1.Name.ToString();
                                   bool match = name == attributeName1 || name == attributeName2;
                                   return match;
                               }));
        return hasAttributes;
    }

    #endregion // MatchAttribute

    #region RefineAttributeNames

    private static (string attributeName1, string attributeName2) RefineAttributeNames(
                                                                        string attributeName)
    {
        int len = attributeName.LastIndexOf(".");
        if (len != -1)
            attributeName = attributeName.Substring(len + 1);

        string attributeName2 = attributeName.EndsWith("Attribute")
            ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
            : $"{attributeName}Attribute";
        return (attributeName, attributeName2);
    }

    #endregion // RefineAttributeNames

    #region GetUsingWithinNamespace

    /// <summary>
    /// Gets the using declared within a namespace.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns></returns>
    public static IImmutableList<string> GetUsingWithinNamespace(this TypeDeclarationSyntax typeDeclaration)
    {

        var fileScope = typeDeclaration.Parent! as FileScopedNamespaceDeclarationSyntax;
        return fileScope?.Usings.Select(u => u.ToFullString()).ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    #endregion // GetUsingWithinNamespace

    #region GetUsing

    /// <summary>
    /// Gets the using statements (declared before the namespace).
    /// </summary>
    /// <param name="syntaxNode">The syntax node.</param>
    /// <returns></returns>
    public static IEnumerable<string> GetUsing(this SyntaxNode syntaxNode)
    {
        if (syntaxNode is CompilationUnitSyntax m)
        {
            foreach (var u in m.Usings)
            {
                var match = u.ToString();
                yield return match;
            }
        }

        if (syntaxNode.Parent == null)
            yield break;

        foreach (var u in syntaxNode.Parent.GetUsing())
        {
            yield return u;
        }
    }

    #endregion // GetUsing

    #region TryGetValue

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment.
    public static bool TryGetValue<T>(
                        this INamedTypeSymbol symbol,
                        string attributeName,
                        string name,
                        out T value)
    {
        value = default;
        var atts = symbol.GetAttributes()
                                    .Where(a => a.AttributeClass?.Name == attributeName);
        foreach (var att in atts)
        {
            if (att.TryGetValue(name, out value))
                return true;
        }
        return false;
    }

    public static bool TryGetValue<T>(
                        this IMethodSymbol symbol,
                        string attributeName,
                        string name,
                        out T value)
    {
        value = default;
        var atts = symbol.GetAttributes()
                                    .Where(a => a.AttributeClass?.Name == attributeName);
        foreach (var att in atts)
        {
            if (att.TryGetValue(name, out value))
                return true;
        }
        return false;
    }

    public static bool TryGetValue<T>(
                        this AttributeData? attributeData,
                        string name,
                        out T value)
    {
        value = default;
        if (attributeData == null)
            return false;

        var names = attributeData
                        .AttributeConstructor
                        ?.Parameters
                        .Select(p => p.Name)
                        .ToArray() ?? Array.Empty<string>();
        int i = 0;
        foreach (var prm in attributeData.ConstructorArguments)
        {
            if (string.Compare(names[i], name, true) != 0)
            {
                continue;
            }

            value = (T)prm.Value;
            return true;
        }
        var prop = attributeData.NamedArguments
                                .FirstOrDefault(m => m.Key == name);
        var val = prop.Value;
        if (val.IsNull)
            return false;
        value = (T)val.Value;
        return true;
    }
#pragma warning restore CS8601
#pragma warning restore CS8600

    #endregion // TryGetValue

    #region ToSymbol

    /// <summary>
    /// Converts to symbol.
    /// </summary>
    /// <param name="declarationSyntax">The declaration syntax.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static ISymbol? ToSymbol(this SyntaxNode declarationSyntax,
                                     Compilation compilation,
                                     CancellationToken cancellationToken = default)
    {
        SemanticModel semanticModel = compilation.GetSemanticModel(declarationSyntax.SyntaxTree);
        ISymbol? symbol = semanticModel.GetDeclaredSymbol(declarationSyntax);
        return symbol;
    }

    #endregion // ToSymbol

    #region GetNestedBaseTypesAndSelf

    /// <summary>
    /// Gets the nested base types and self.
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns></returns>
    public static IEnumerable<INamedTypeSymbol> GetNestedBaseTypesAndSelf(this INamedTypeSymbol typeSymbol)
    {
        yield return typeSymbol;
        INamedTypeSymbol? baseType = typeSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }

    #endregion // GetNestedBaseTypesAndSelf

    public static AttributeData GetBlockAttr(this SyntaxAndSymbol s)
        => s.Symbol.GetBlockAttr();

    public static AttributeData GetBlockAttr(this IMethodSymbol ms)
        => ms.GetAttributes().FirstOrDefault(a => a.AttributeClass.Name == Generator.MethodTargetAttribute);

    public static AttributeData GetBlockAttr(this INamedTypeSymbol s)
        => s.GetAttributes().FirstOrDefault(a => a.AttributeClass.Name == Generator.MethodTargetAttribute);

    public static T GetArg<T>(this AttributeData a, int ord) => (T)a.ConstructorArguments[ord].Value;
}
