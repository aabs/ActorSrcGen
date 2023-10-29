using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Helpers;

internal static class DomainRoslynExtensions
{
    #region AppendHeader

    public static void AppendHeader(this StringBuilder builder,
                                    TypeDeclarationSyntax syntax,
                                    INamedTypeSymbol typeSymbol)
    {
        builder.AppendLine($"// Generated on {DateTimeOffset.UtcNow:yyyy-MM-dd}");
        builder.AppendLine($"#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.");
        builder.AppendLine($"#pragma warning disable CS0108 // hides inherited member.");
        builder.AppendLine();
        var usingLines = syntax.GetUsing();
        foreach (var line in usingLines)
        {
            builder.AppendLine(line.Trim());
        }

        var ns = typeSymbol.ContainingNamespace.ToDisplayString();
        if (ns != null)
            builder.AppendLine($"namespace {ns};");

        var innerusingLines = syntax.GetUsingWithinNamespace();
        foreach (var line in innerusingLines)
        {
            builder.AppendLine(line.Trim());
        }
    }

    #endregion // AppendHeader
}
