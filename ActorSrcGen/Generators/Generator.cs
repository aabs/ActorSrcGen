#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

using ActorSrcGen.Generators;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

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
        IncrementalValuesProvider<SyntaxAndSymbol> classDeclarations =
            context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: AttributePredicate,
                        transform: static (ctx, _) => ToGenerationInput(ctx))
                   .Where(static m => m is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxAndSymbol>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        // register a code generator for the triggers
        context.RegisterSourceOutput(compilationAndClasses, Generate);

        static SyntaxAndSymbol ToGenerationInput(GeneratorSyntaxContext context)
        {
            var declarationSyntax = (TypeDeclarationSyntax)context.Node;

            var symbol = context.SemanticModel.GetDeclaredSymbol(declarationSyntax);
            if (symbol is not INamedTypeSymbol namedSymbol) throw new NullReferenceException($"Code generated symbol of {nameof(declarationSyntax)} is missing");
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
                ActorGenerator ag = new(context);
                ag.GenerateActor(actor);
                context.AddSource($"{actor.Name}.generated.cs", ag.Builder.ToString());
#if DEBUG
                Console.WriteLine(ag.Builder.ToString());
#endif
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while generating source: " + e.Message); ;
        }
    }
}