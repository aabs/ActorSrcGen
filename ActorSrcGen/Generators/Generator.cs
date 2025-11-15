#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

using ActorSrcGen.Generators;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using ActorSrcGen.Templates;

namespace ActorSrcGen;

[Generator]
public partial class Generator : IIncrementalGenerator
{
    internal const string MethodTargetAttribute = "DataflowBlockAttribute";
    internal const string TargetAttribute = "ActorAttribute";
    protected IncrementalGeneratorInitializationContext GenContext { get; set; }

    /// <summary>
    ///   Called to initialize the generator and register generation steps via callbacks on the
    ///   <paramref name="context" />
    /// </summary>
    /// <param name="context">
    ///   The <see cref="T:Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext" /> to
    ///   register callbacks on
    /// </param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        GenContext = context;
        IncrementalValuesProvider<SyntaxAndSymbol?> classDeclarations =
            context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: AttributePredicate,
                        transform: static (ctx, _) => ToGenerationInput(ctx))
                   .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxAndSymbol>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        // register a code generator for the triggers
        context.RegisterSourceOutput(compilationAndClasses, Generate);

        static SyntaxAndSymbol? ToGenerationInput(GeneratorSyntaxContext context)
        {
            var declarationSyntax = (TypeDeclarationSyntax)context.Node;

            var symbol = context.SemanticModel.GetDeclaredSymbol(declarationSyntax);
            if (symbol is not INamedTypeSymbol namedSymbol)
            {
                // Return null to filter out invalid symbols - diagnostic will be reported by the compiler
                return null;
            }
            return new SyntaxAndSymbol(declarationSyntax, namedSymbol);
        }

        void Generate(
                      SourceProductionContext spc,
                      (Compilation compilation,
                          ImmutableArray<SyntaxAndSymbol> items) source)
        {
            var (compilation, items) = source;
            foreach (SyntaxAndSymbol item in items)
            {
                OnGenerate(spc, compilation, item);
            }
        }
    }

    private static bool AttributePredicate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        return syntaxNode.MatchAttribute(TargetAttribute, cancellationToken);
    }

    private void OnGenerate(SourceProductionContext context,
                            Compilation compilation,
                            SyntaxAndSymbol input)
    {
        try
        {
            ActorVisitor v = new();
            v.VisitActor(input);
            foreach (var actor in v.Actors)
            {
                var source = new Actor(actor).TransformText();
                context.AddSource($"{actor.Name}.generated.cs", source);
            }
        }
        catch (Exception e)
        {
            var descriptor = new DiagnosticDescriptor(
                "ASG0002",
                "Error generating source",
                "Error while generating source for '{0}': {1}",
                "SourceGenerator",
                DiagnosticSeverity.Error,
                true);
            var diagnostic = Diagnostic.Create(descriptor, input.Syntax.GetLocation(), input.Symbol.Name, e.ToString());
            context.ReportDiagnostic(diagnostic);
        }
    }
}