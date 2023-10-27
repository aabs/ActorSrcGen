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
    internal const string TargetAttribute = "ActorAttribute";
    internal const string MethodTargetAttribute = "DataflowBlockAttribute";

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

    private Dictionary<IMethodSymbol, IMethodSymbol> BuildDependencyGraph(SyntaxAndSymbol ss)
    {
        var methods = (from m in ss.Symbol.GetMembers()
                       let ms = m as IMethodSymbol
                       where ms is not null
                       where ms.Name != ".ctor"
                       select ms).ToArray();

        var deps = new Dictionary<IMethodSymbol, IMethodSymbol>();
        foreach (var m in methods)
        {
            var a = m.GetBlockAttr();
            var nextArg = m.GetBlockAttr().GetArg<string>(0);
            var toSymbol = methods.FirstOrDefault(n => n.Name == nextArg);
            deps[m] = toSymbol;
        }
        return deps;
    }

    #endregion // Initialize

    #region OnGenerate

    private void OnGenerate(
            SourceProductionContext context,
            Compilation compilation,
            SyntaxAndSymbol input)
    {
        var dg = BuildDependencyGraph(input);
        INamedTypeSymbol typeSymbol = input.Symbol;
        TypeDeclarationSyntax syntax = input.Syntax;
        var cancellationToken = context.CancellationToken;
        if (cancellationToken.IsCancellationRequested)
            return;
        StringBuilder builder = new StringBuilder();
        string type = syntax.Keyword.Text;
        string name = typeSymbol.Name;
        var asm = GetType().Assembly.GetName();
        string firstInputType = GetActorInputType(input);
        string lastOutputType = GetActorOutputType(input);
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

        foreach (var s in dg.Keys)
        {
            var srcBlockName = "_" + s.Name;
            if (!string.IsNullOrWhiteSpace(srcBlockName) && dg[s] is not null)
            {
                var targetBlockName = "_" + dg[s].Name;
                builder.AppendLine($"{srcBlockName}.LinkTo({targetBlockName}, new DataflowLinkOptions {{ PropagateCompletion = true }});");
            }
        }

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

        builder.AppendLine($"public override ITargetBlock<{firstInputType}> InputBlock {{ get => throw new NotImplementedException(); }}");
        builder.AppendLine($"public override ISourceBlock<{lastOutputType}> OutputBlock {{ get => throw new NotImplementedException(); }}");


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

    private string GetActorOutputType(SyntaxAndSymbol input)
    {
        const int ordinalOfEndParam = 3;
        INamedTypeSymbol typeSymbol = input.Symbol;
        TypeDeclarationSyntax syntax = input.Syntax;
        var methods = (from m in typeSymbol.GetMembers()
                       let ms = m as IMethodSymbol
                       where ms is not null
                       where ms.Name != ".ctor"
                       select ms).ToArray();
        var fm = (from m in methods
                  let a = m.GetAttributes().First(a => a.AttributeClass.Name == MethodTargetAttribute)
                  let isLast = (bool)a.ConstructorArguments[3].Value
                  where isLast
                  select m).FirstOrDefault();
        if (fm != null)
        {
            ITypeSymbol returnType = fm.ReturnType;
            if (returnType.Name == "Task")
            {
                if (returnType is INamedTypeSymbol nts)
                {
                    return nts.TypeArguments[0].Name;
                }
                return returnType.Name;
            }
            return fm!.ReturnType.Name;
        }
        else
            return "object";
    }

    private string GetActorInputType(SyntaxAndSymbol input)
    {
        const int ordinalOfStartParam = 2;
        INamedTypeSymbol typeSymbol = input.Symbol;
        TypeDeclarationSyntax syntax = input.Syntax;
        var methods = (from m in typeSymbol.GetMembers()
                       let ms = m as IMethodSymbol
                       where ms is not null
                       where ms.Name != ".ctor"
                       select ms).ToArray();
        var fm = (from m in methods
                  let a = m.GetAttributes().First(a => a.AttributeClass.Name == MethodTargetAttribute)
                  let isFirst = (bool)a.ConstructorArguments[ordinalOfStartParam].Value
                  where isFirst
                  select m).FirstOrDefault();
        if (fm != null)
        {
            return fm!.Parameters.First()!.Type.Name;
        }
        else
            return "object";
    }


    #endregion // OnGenerate
}

