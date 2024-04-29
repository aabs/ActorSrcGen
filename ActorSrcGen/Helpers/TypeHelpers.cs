using Microsoft.CodeAnalysis;
using System.Text;

namespace ActorSrcGen.Helpers;

public static class TypeHelpers
{
    public static string RenderTypename(this ITypeSymbol? ts, bool stripTask = false)
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

}