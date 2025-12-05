using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Helpers;

/// <summary>
/// Represents a type declaration paired with its semantic symbol and model for generation.
/// </summary>
public sealed record SyntaxAndSymbol(
    ClassDeclarationSyntax Syntax,
    INamedTypeSymbol Symbol,
    SemanticModel SemanticModel
);

