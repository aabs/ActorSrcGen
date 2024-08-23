using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace ActorSrcGen.Helpers;

public static class TypeHelpers
{
    public static string RenderTypenameOld(this ITypeSymbol? ts, bool stripTask = false)
    {
        if (ts is null)
            return "";
        if (ts.Name == "Task" && ts is INamedTypeSymbol nts)
        {
            var sb = new StringBuilder();
            if (stripTask && nts.TypeArguments.Length == 1)
            {
                return RenderTypename(nts.TypeArguments[0]);
            }
            else
            {
                sb.Append(nts.Name);
                if (nts.TypeArguments.Length > 0)
                {
                    sb.Append("<");
                    var typeArgs = string.Join(", ", nts.TypeArguments.Select(ta => RenderTypename(ta)));
                    sb.Append(typeArgs);
                    sb.Append(">");
                }
            }
            return sb.ToString();
        }
        return ts.Name;
    }

    public static string RenderTypename(this ITypeSymbol? ts, bool stripTask = false)
    {
        if (ts is null)
            return "";
        if (stripTask && ts.Name == "Task" && ts is INamedTypeSymbol nts)
        {
            return nts.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
        return ts.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    public static string RenderTypename(this GenericNameSyntax? ts, Compilation compilation, bool stripTask = false)
    {
        if (ts is null)
            return "";
        var x = ts.ToSymbol(compilation);
        if (stripTask && x is not null && x is INamedTypeSymbol nts && nts.Name == "Task")
        {
            return nts.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
        return x.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    public static bool HasMultipleOnwardSteps(this IMethodSymbol method, GenerationContext ctx)
    {
        if (ctx.DependencyGraph.TryGetValue(method, out var nextSteps))
        {
            return nextSteps.Count > 1;
        }

        return false;
    }

    public static ITypeSymbol? GetFirstTypeParameter(this ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nts && nts.TypeArguments.Length > 0)
        {
            return nts.TypeArguments[0];
        }

        return default;
    }

    public static TypeArgumentListSyntax AsTypeArgumentList(ImmutableArray<ITypeSymbol> typeArguments)
    {
        return SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList<TypeSyntax>(
                typeArguments.Select(t => SyntaxFactory.IdentifierName(t.ToDisplayString()))
            )
        );
    }
}