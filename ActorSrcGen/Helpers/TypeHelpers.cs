using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

#nullable enable
#pragma warning disable CS8602, CS8603, CS8604, CS8605

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
        if (ts is null)
        {
            return string.Empty;
        }

        var t = ts;
        if (stripTask && string.Equals(t.Name, "Task", StringComparison.Ordinal) && t is INamedTypeSymbol ntTask && ntTask.TypeArguments.Length > 0)
        {
            t = ntTask.TypeArguments[0];
        }

        if (stripCollection && t.IsCollection() && t is INamedTypeSymbol ntColl && ntColl.TypeArguments.Length > 0)
        {
            t = ntColl.TypeArguments[0];
        }

        return t?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? string.Empty;

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
        if (x is null)
        {
            if (stripTask && string.Equals(ts.Identifier.Text, "Task", StringComparison.Ordinal) && ts.TypeArgumentList.Arguments.Count > 0)
            {
                return ts.TypeArgumentList.Arguments[0].ToString();
            }
            return ts.ToString();
        }

        if (stripTask && x is INamedTypeSymbol nts && nts.Name == "Task")
        {
            return nts.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
        return x.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    public static bool IsCollection(this ITypeSymbol? ts)
        => ts is INamedTypeSymbol { Name: "List" or "IEnumerable" or "ImmutableArray" or "ImmutableList" or "IImmutableList" };

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

        if (string.Equals(t.Name, "Task", StringComparison.Ordinal) && t is INamedTypeSymbol nts && nts.TypeArguments.Length > 0)
        {
            t = nts.TypeArguments[0];
        }

        return t.IsCollection();
    }

    public static bool IsAsynchronous(this IMethodSymbol method)
    {
        return (method.IsAsync || method.ReturnType.Name == "Task");
    }

    public static int GetMaxDegreeOfParallelism(this IMethodSymbol method)
    {
        var attr = method.GetBlockAttr();
        if (attr?.AttributeConstructor is not null)
        {
            var parameter = attr.AttributeConstructor.Parameters.FirstOrDefault(p => string.Equals(p.Name, "maxDegreeOfParallelism", StringComparison.Ordinal));
            if (parameter != null)
            {
                var ordinal = parameter.Ordinal;
                if (attr.ConstructorArguments.Length > ordinal && attr.ConstructorArguments[ordinal].Value is int value)
                {
                    return value;
                }
            }
        }

        return 1;
    }
    public static int GetMaxBufferSize(this IMethodSymbol method)
    {
        var attr = method.GetBlockAttr();
        if (attr?.AttributeConstructor is not null)
        {
            var parameter = attr.AttributeConstructor.Parameters.FirstOrDefault(p => string.Equals(p.Name, "maxBufferSize", StringComparison.Ordinal));
            if (parameter != null)
            {
                var ordinal = parameter.Ordinal;
                if (attr.ConstructorArguments.Length > ordinal && attr.ConstructorArguments[ordinal].Value is int value)
                {
                    return value;
                }
            }
        }

        return 1;
    }

    public static string GetReturnTypeCollectionType(this IMethodSymbol method)
    {
        if (method.ReturnTypeIsCollection())
        {
            return method.ReturnType.GetFirstTypeParameter()?.RenderTypename() ?? string.Empty;
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