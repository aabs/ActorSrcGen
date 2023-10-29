#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

using ActorSrcGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace ActorSrcGen;

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
            if (a.AttributeClass.Name != "LastStepAttribute")
            {
                var nextArg = m.GetBlockAttr().GetArg<string>(0);
                var toSymbol = methods.FirstOrDefault(n => n.Name == nextArg);
                deps[m] = toSymbol;
            }
        }
        return deps;
    }

    #endregion Initialize

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
        GenerateHeaders(builder, input);
        GenerateClass(builder, input);
        context.AddSource($"{typeSymbol.Name}.generated.cs", builder.ToString());
        //Console.WriteLine(builder.ToString());
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

    private static void GenerateHeaders(StringBuilder builder, SyntaxAndSymbol input)
    {
        // header stuff
        builder.AppendHeader(input.Syntax, input.Symbol);
        builder.AppendLine("using System.Threading.Tasks.Dataflow;");
        builder.AppendLine("using Gridsum.DataflowEx;");
        builder.AppendLine();
    }

    private void GenerateClass(StringBuilder builder, SyntaxAndSymbol input)
    {
        INamedTypeSymbol typeSymbol = input.Symbol;
        string name = typeSymbol.Name;
        var dg = BuildDependencyGraph(input);
        string firstInputType = GetActorInputType(input);
        string lastOutputType = GetActorOutputType(input);
        // start the class
        builder.AppendLine($"public partial class {name} : Dataflow<{firstInputType}, {lastOutputType}>");
        builder.AppendLine("{");
        builder.AppendLine();
        IMethodSymbol[] methods = GenerateCtor(dg, typeSymbol, builder, name);
        // foreach method
        foreach (var ms in methods)
        {
            GenerateBlockDeclaration(builder, ms);
            builder.AppendLine();
        } // foreach member

        GenerateIOBlockAccessors(builder, input);
        builder.AppendLine();
        GeneratePostMethod(builder, firstInputType, lastOutputType);
        builder.AppendLine();
        // end the class
        builder.AppendLine("}");
    }

    private void GeneratePostMethod(StringBuilder builder, string firstInputType, string lastOutputType)
    {
        builder.AppendLine($$"""
                public async Task<{{lastOutputType}}> Post({{firstInputType}} input)
                {
                    InputBlock.Post(input);
                    return await OutputBlock.ReceiveAsync();
                }
            """);
    }

    private void GenerateIOBlockAccessors(StringBuilder builder, SyntaxAndSymbol input)
    {
        string firstInputType = GetActorInputType(input);
        string lastOutputType = GetActorOutputType(input);
        var startMethod = GetStartMethod(input);
        var endMethod = GetEndMethod(input);

        builder.AppendLine($"    public override ITargetBlock<{firstInputType}> InputBlock {{ get => _{startMethod.Name}; }}");
        builder.AppendLine($"    public override ISourceBlock<{lastOutputType}> OutputBlock {{ get => _{endMethod.Name}; }}");
    }

    private static void GenerateBlockDeclaration(StringBuilder builder, IMethodSymbol ms)
    {
        string blockName = $"_{ms.Name}";
        string inputTypeName = RenderTypename(ms.Parameters.First().Type);
        string outputTypeName = RenderTypename(ms.ReturnType, true);
        // generate the block decl
        builder.AppendLine($"    TransformBlock<{inputTypeName},{outputTypeName}> {blockName};");
        // generate the wrapper function
    }

    private static string RenderTypename(ITypeSymbol ts, bool stripTask = false)
    {
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

    private static IMethodSymbol[] GenerateCtor(Dictionary<IMethodSymbol, IMethodSymbol> dg, INamedTypeSymbol typeSymbol, StringBuilder builder, string name)
    {
        // start the ctor
        builder.AppendLine($"    public {name}() : base(DataflowOptions.Default)");
        builder.AppendLine("    {");
        // create the blocks
        var methods = (from m in typeSymbol.GetMembers()
                       let ms = m as IMethodSymbol
                       where ms is not null
                       where ms.Name != ".ctor"
                       select ms).ToArray();
        foreach (var ms in methods)
        {
            string _blockName = $"_{ms.Name}";
            string inputTypeName = RenderTypename(ms.Parameters.First().Type);
            string outputTypeName = RenderTypename(ms.ReturnType, true);
            const int capacity = 5;
            const int maxParallelism = 8;
            builder.AppendLine($$"""
                    {{_blockName}} = new TransformBlock<{{inputTypeName}}, {{outputTypeName}}>({{ms.Name}},
                        new ExecutionDataflowBlockOptions() {
                            BoundedCapacity = {{capacity}},
                            MaxDegreeOfParallelism = {{maxParallelism}}
                    });
            """);
            builder.AppendLine($"        RegisterChild({_blockName});");
        }

        // create the linkage
        foreach (var s in dg.Keys)
        {
            var srcBlockName = "_" + s.Name;
            if (!string.IsNullOrWhiteSpace(srcBlockName) && dg[s] is not null)
            {
                var targetBlockName = "_" + dg[s].Name;
                builder.AppendLine($"        {srcBlockName}.LinkTo({targetBlockName}, new DataflowLinkOptions {{ PropagateCompletion = true }});");
            }
        }

        // end the ctor
        builder.AppendLine("    }");
        return methods;
    }

    private string GetActorOutputType(SyntaxAndSymbol input)
    {
        var fm = GetEndMethod(input);
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
        {
            return "object";
        }
    }

    private IMethodSymbol GetMethodWithAttr(SyntaxAndSymbol input, string attrName)
    {
        var methods = (from m in input.Symbol.GetMembers()
                       let ms = m as IMethodSymbol
                       where ms is not null
                       where ms.Name != ".ctor"
                       select ms).ToArray();
        var fm = methods.FirstOrDefault(m => m.GetAttributes().Any(a => a.AttributeClass.Name == attrName));
        return fm;
    }

    private IMethodSymbol GetStartMethod(SyntaxAndSymbol input) => GetMethodWithAttr(input, "InitialStepAttribute");
    private IMethodSymbol GetEndMethod(SyntaxAndSymbol input) => GetMethodWithAttr(input, "LastStepAttribute");

    private string GetActorInputType(SyntaxAndSymbol input)
    {
        var fm = GetStartMethod(input);
        if (fm != null)
        {
            return fm!.Parameters.First()!.Type.Name;
        }
        else
        {
            return "object";
        }
    }

    #endregion OnGenerate
}