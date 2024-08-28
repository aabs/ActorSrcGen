using ActorSrcGen.Model;
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

    public static string RenderTypename(this ITypeSymbol? ts, bool stripTask = false, bool stripCollection = false)
    {
        var t = ts;
        if (stripTask && t.Name == "Task" && t is INamedTypeSymbol nt)
        {
            t = nt.TypeArguments[0];
        }

        if (stripCollection && t.IsCollection())
        {
            nt = t as INamedTypeSymbol;
            t = nt.TypeArguments[0];
        }

        return t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        //if (ts is null)
        //    return "";
        //if (stripTask && ts.Name == "Task" && ts is INamedTypeSymbol nts)
        //{
        //    return nts.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        //}
        //return ts.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
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

    public static bool IsCollection(this ITypeSymbol ts)
        => ts.Name is "List" or "IEnumerable";

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

public static class MethodExtensions
{
    public static bool ReturnTypeIsCollection(this IMethodSymbol method)
    {
        var t = method.ReturnType;

        if (t.Name == "Task")
        {
            t = t.GetFirstTypeParameter();
        }
        var returnTypeIsEnumerable = t.IsCollection();
        return returnTypeIsEnumerable;
    }

    public static bool IsAsynchronous(this IMethodSymbol method)
    {
        return (method.IsAsync || method.ReturnType.Name == "Task");
    }

    public static int GetMaxDegreeOfParallelism(this IMethodSymbol method)
    {
        var attr = method.GetBlockAttr();
        if (attr != null)
        {
            var ord = attr.AttributeConstructor.Parameters.First(p => p.Name == "maxDegreeOfParallelism").Ordinal;
            return (int)attr.ConstructorArguments[ord].Value;
        }

        return 1;
    }
    public static int GetMaxBufferSize(this IMethodSymbol method)
    {
        var attr = method.GetBlockAttr();
        if (attr != null)
        {
            var ord = attr.AttributeConstructor.Parameters.First(p => p.Name == "maxBufferSize").Ordinal;
            return (int)attr.ConstructorArguments[ord].Value;
        }

        return 1;
    }

    public static string GetReturnTypeCollectionType(this IMethodSymbol method)
    {
        if (method.ReturnTypeIsCollection())
        {
            return method.ReturnType.GetFirstTypeParameter().RenderTypename();
        }
        return method.ReturnType.RenderTypename(stripTask: true);
    }

    public static string? GetInputTypeName(this IMethodSymbol method)
    {
        if (method.Parameters.IsDefaultOrEmpty)
        {
            return default;
        }
        return method.Parameters.First().Type.RenderTypename();
    }
}