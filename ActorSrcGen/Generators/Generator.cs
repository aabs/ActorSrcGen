#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

using ActorSrcGen.Generators;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using ActorSrcGen.Templates;
using System.Text;
using ActorSrcGen.Diagnostics;

namespace ActorSrcGen;

[Generator]
public partial class Generator : IIncrementalGenerator
{
    internal const string MethodTargetAttribute = "DataflowBlockAttribute";
    internal const string TargetAttribute = "ActorAttribute";

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
        IncrementalValuesProvider<SyntaxAndSymbol> classDeclarations =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: AttributePredicate,
                    transform: static (ctx, _) => ToGenerationInput(ctx))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxAndSymbol>)> compilationAndClasses =
            context.CompilationProvider.Combine(classDeclarations.Collect());

        // register a code generator for the triggers
        context.RegisterSourceOutput(compilationAndClasses, Generate);

        static SyntaxAndSymbol? ToGenerationInput(GeneratorSyntaxContext context)
        {
            var declarationSyntax = context.Node as ClassDeclarationSyntax;

            if (declarationSyntax is null)
            {
                return null;
            }

            if (context.SemanticModel?.GetDeclaredSymbol(declarationSyntax) is not INamedTypeSymbol namedSymbol)
            {
                // Return null to filter out invalid symbols - diagnostic will be reported by the compiler
                return null;
            }
            return new SyntaxAndSymbol(declarationSyntax, namedSymbol, context.SemanticModel);
        }

        void Generate(
                      SourceProductionContext spc,
                      (Compilation compilation,
                          ImmutableArray<SyntaxAndSymbol> items) source)
        {
            var (compilation, items) = source;
            var orderedItems = items
                .Where(i => i is not null)
                .OrderBy(i => i.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableArray();

            try
            {
                foreach (var item in orderedItems)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    OnGenerate(spc, compilation, item);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop generation early when cancellation is requested. Partial output may have been produced.
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
            context.CancellationToken.ThrowIfCancellationRequested();

            var visitor = new ActorVisitor();
            var result = visitor.VisitActor(input, context.CancellationToken);

            foreach (var diagnostic in result.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }

            var actors = result.Actors
                .OrderBy(a => a.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableArray();

            foreach (var actor in actors)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var generator = new ActorGenerator(context);
                generator.GenerateActor(actor);
                var source = generator.Builder.ToString();

                context.CancellationToken.ThrowIfCancellationRequested();
                context.AddSource($"{actor.Name}.generated.cs", SourceText.From(source, Encoding.UTF8));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var diagnostic = ActorSrcGen.Diagnostics.Diagnostics.CreateDiagnostic(ActorSrcGen.Diagnostics.Diagnostics.ASG0002, input.Syntax.GetLocation(), input.Symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}