#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
using System.Collections.Immutable;
using System;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;
using DataflowSrcGen.Helpers;
using DataflowSrcGen.Generators;

namespace DataflowSrcGen;

[Generator]
public partial class Generator : IIncrementalGenerator
{
    protected const string TargetAttribute = "ActorAttribute";
    protected const string MethodTargetAttribute = "DataflowBlockAttribute";

    private static bool AttributePredicate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        return syntaxNode.MatchAttribute(TargetAttribute, cancellationToken);
    }

    #region Initialize

    /// <summary>
    /// Called to initialize the generator and register generation steps via callbacks
    /// on the <paramref name="context" />
    /// </summary>
    /// <param name="context">The <see cref="T:Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext" /> to register callbacks on</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
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

    #endregion // Initialize

    #region OnGenerate

    private void OnGenerate(
            SourceProductionContext context,
            Compilation compilation,
            SyntaxAndSymbol input)
    {
        INamedTypeSymbol typeSymbol = input.Symbol;
        TypeDeclarationSyntax syntax = input.Syntax;
        var cancellationToken = context.CancellationToken;
        if (cancellationToken.IsCancellationRequested)
            return;
        StringBuilder builder = new StringBuilder();
        string type = syntax.Keyword.Text;
        string name = typeSymbol.Name;
        var asm = GetType().Assembly.GetName();
        string firstInputType = "string";
        string lastOutputType = "int";
        // header stuff
        builder.AppendHeader(syntax, typeSymbol);
        builder.AppendLine("using System.Net.Http.Json;");
        builder.AppendLine("using System.Threading.Tasks.Dataflow;");
        builder.AppendLine("using Gridsum.DataflowEx;");
        builder.AppendLine();
        // start the class 
        builder.AppendLine($"public partial class {name} : Dataflow<{firstInputType}, {lastOutputType}>");
        builder.AppendLine("{");
        // start the ctor
        builder.AppendLine($"public {name}() : base(DataflowOptions.Default)");
        builder.AppendLine("{");
        //   create the blocks
        var methods = (from m in typeSymbol.GetMembers()
                       let ms = m as IMethodSymbol
                       where ms is not null
                       where ms.Name != ".ctor"
                       select ms).ToArray();
        foreach (var ms in methods)
        {
            string _blockName = $"_{ms.Name}";
            string inputTypeName = ms.Parameters.First().Type.Name;
            string outputTypeName = ms.ReturnType.Name;
            
            builder.AppendLine(SourceTemplates.CreateBlockDefinition(_blockName, ms.Name, inputTypeName, outputTypeName, 5, 8));
        }
        //   create the linkage
        /*
        foreach (var ms in methods)
        {
            builder.AppendLine(SourceTemplates.CreateBlockDefinition("someName", "DoTask2", "string", "int", 5, 8));
        }
        */
        // end the ctor
        builder.AppendLine("}");
        // foreach method
        foreach (var ms in methods)
        {
            string _blockName = $"_{ms.Name}";
            string inputTypeName = ms.Parameters.First().Type.Name;
            string outputTypeName = ms.ReturnType.Name;
            //   generate the block decl
            builder.AppendLine(SourceTemplates.CreateBlockDeclaration(_blockName, inputTypeName, outputTypeName));
            //   generate the wrapper function
        } // foreach member

        builder.AppendLine("public override ITargetBlock<string> InputBlock { get => throw new NotImplementedException(); }");
        builder.AppendLine("public override ISourceBlock<int> OutputBlock { get => throw new NotImplementedException(); }");


        // end the class
        builder.AppendLine("}");
        context.AddSource($"{name}.generated.cs", builder.ToString());

        /*
        builder = new StringBuilder();
        builder.AppendHeader(syntax, typeSymbol);
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"{asm.Name}\",\"{asm.Version}\")]");
        builder.AppendLine($"public static class {name}DiExtensions");
        builder.AppendLine("{");
        builder.AppendLine($"\tpublic static IServiceCollection Add{name}Client(this IServiceCollection services, Uri baseUrl)");
        builder.AppendLine("\t{");
        builder.AppendLine($"\t\tservices.AddHttpClient<{typeSymbol.Name}, {name}>(\"{clsTemplate}\", client =>  client.BaseAddress = baseUrl);");
        builder.AppendLine("\treturn services;");
        builder.AppendLine("\t}");
        builder.AppendLine("}");
        context.AddSource($"{name}DiExtensions.generated.cs", builder.ToString());
        */
    }

    #endregion // OnGenerate
}