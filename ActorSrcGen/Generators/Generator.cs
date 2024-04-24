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
        var methods = GetStepMethods(ss.Symbol).ToArray();
        //var methods = (from m in ss.Symbol.GetMembers()
        //               let ms = m as IMethodSymbol
        //               where ms is not null
        //               where ms.GetAttributes().Any(a => a.AttributeClass.Name.EndsWith("StepAttribute"))
        //               where ms.Name != ".ctor"
        //               select ms).ToArray();

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

    private void OnGenerate(
            SourceProductionContext context,
            Compilation compilation,
            SyntaxAndSymbol input)
    {
        try
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
#if DEBUG
            Console.WriteLine(builder.ToString());
#endif
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while generating source: "+e.Message); ;
        }
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
        var dataflowClass = $"Dataflow<{firstInputType}, {lastOutputType}>";

        if (lastOutputType.Equals("void", StringComparison.InvariantCultureIgnoreCase))
        {
            dataflowClass = $"Dataflow<{firstInputType}>";
        }

        // start the class
        builder.AppendLine($"public partial class {name} : {dataflowClass}, IActor<{firstInputType}>");
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
        GeneratePostMethod(builder, firstInputType);
        builder.AppendLine();
        // end the class
        builder.AppendLine("}");
    }

    private void GeneratePostMethod(StringBuilder builder, string firstInputType)
    {
        builder.AppendLine($$"""
                public bool Call({{firstInputType}} input)
                => InputBlock.Post(input);
            """);
        builder.AppendLine();
        builder.AppendLine($$"""
                public async Task<bool> Cast({{firstInputType}} input)
                => await InputBlock.SendAsync(input);
            """);
    }

    private void GenerateIOBlockAccessors(StringBuilder builder, SyntaxAndSymbol input)
    {
        string firstInputType = GetActorInputType(input);
        string lastOutputType = GetActorOutputType(input);
        var startMethod = GetStartMethod(input);
        var endMethod = GetEndMethod(input);

        builder.AppendLine($"    public override ITargetBlock<{firstInputType}> InputBlock {{ get => _{startMethod.Name}; }}");
        if (!lastOutputType.Equals("void", StringComparison.InvariantCultureIgnoreCase))
        {
            builder.AppendLine($"    public override ISourceBlock<{lastOutputType}> OutputBlock {{ get => _{endMethod.Name}; }}");
        }
    }

    private static void GenerateBlockDeclaration(StringBuilder builder, IMethodSymbol ms)
    {
        string blockName = $"_{ms.Name}";
        string inputTypeName = RenderTypename(ms.Parameters.First().Type);
        string outputTypeName = RenderTypename(ms.ReturnType, true);
        // generate the block decl
        var blockType = $"TransformBlock<{inputTypeName}, {outputTypeName}>";
        var name = RenderTypename(ms.ReturnType, false).ToLowerInvariant();

        if (name == "void")
        {
            blockType = $"ActionBlock<{inputTypeName}>";
        }
        else if (name.StartsWith("task<"))
        {
            var collectionType = RenderTypename(GetFirstTypeParameter(ms.ReturnType));
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        else if (name.StartsWith("ienumerable<"))
        {
            var collectionType = RenderTypename(GetFirstTypeParameter(ms.ReturnType));
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        else if (name.StartsWith("task<ienumerable"))
        {
            var collectionType = RenderTypename(GetFirstTypeParameter(ms.ReturnType));
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        else
        {
            var collectionType = RenderTypename(ms.ReturnType);
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        builder.AppendLine($"    {blockType} {blockName};");
        // generate the wrapper function
    }

    private static string RenderTypename(ITypeSymbol? ts, bool stripTask = false)
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
    static IEnumerable<IMethodSymbol> GetStepMethods(INamedTypeSymbol typeSymbol)
    {
        return from m in typeSymbol.GetMembers()
               let ms = m as IMethodSymbol
               where ms is not null
               where ms.GetAttributes().Any(a => a.AttributeClass.Name.EndsWith("StepAttribute"))
               where ms.Name != ".ctor"
               select ms;

    }
    private static IMethodSymbol[] GenerateCtor(Dictionary<IMethodSymbol, IMethodSymbol> dg, INamedTypeSymbol typeSymbol, StringBuilder builder, string name)
    {
        // start the ctor
        builder.AppendLine($"    public {name}() : base(DataflowOptions.Default)");
        builder.AppendLine("    {");
        // create the blocks
        var methods = GetStepMethods(typeSymbol).ToArray();
        foreach (var method in methods)
        {
            GenerateBlockCreation(builder, method);
        }

        // create the linkage
        foreach (var methodName in dg.Keys)
        {
            GenerateBlockLinkage(builder, methodName, dg);
        }

        // end the ctor
        builder.AppendLine("    }");
        return methods;
    }

    private static void GenerateBlockLinkage(StringBuilder builder, IMethodSymbol methodName, Dictionary<IMethodSymbol, IMethodSymbol> dependencyGraph)
    {
        var srcBlockName = "_" + methodName.Name;
        if (!string.IsNullOrWhiteSpace(srcBlockName) && dependencyGraph[methodName] is not null)
        {
            var targetBlockName = "_" + dependencyGraph[methodName].Name;
            builder.AppendLine($"        {srcBlockName}.LinkTo({targetBlockName}, new DataflowLinkOptions {{ PropagateCompletion = true }});");
        }
    }

    private static void GenerateBlockCreation(StringBuilder builder, IMethodSymbol? ms)
    {
        string _blockName = $"_{ms.Name}";
        string inputTypeName = RenderTypename(ms.Parameters.First().Type);
        string outputTypeName = RenderTypename(ms.ReturnType, true);
        const int capacity = 5;
        const int maxParallelism = 8;
        var blockType = $"TransformBlock<{inputTypeName}, {outputTypeName}>";
        if (outputTypeName.Equals("void", StringComparison.InvariantCultureIgnoreCase))
        {
            blockType = $"ActionBlock<{inputTypeName}>";
        }

        // The logic for the TLEH: 1) If the type is "void", just trap
        // exceptions and do nothing. 2) If the type is "IEnumerable<T>", just
        // create a receiver collection, and receive into that. 3) If the type
        // is "Task<T>", create a new signature for "Task<IEnumerable<T>>" and
        // return empty collection on error. 4) if the type is "T", create a new
        // signature for "Task<IEnumerable<T>>" and return empty collection on
        // error.
        var name = RenderTypename(ms.ReturnType, false).ToLowerInvariant();
        var handlerFunc = "";

        if (name == "" || name is null)
        {
            handlerFunc = $$"""
                    ({{inputTypeName}} x) => {
                        try
                        {
                            return {{ms.Name}}(x);
                        }
                        catch
                        {
                            return default;
                        }
                    }
            """;
        }
        else if (name == "void")
        {
            handlerFunc = $$"""
                    ({{inputTypeName}} x) => {
                        try
                        {
                            {{ms.Name}}(x);
                        }catch{}
                    }
            """;
        }
        else if (name == "task")
        {
            handlerFunc = $$"""
                    async ({{inputTypeName}} x) => {
                        try
                        {
                            await {{ms.Name}}(x);
                        }catch{}
                    }
           """;
        }
        else if (name.StartsWith("task<"))
        {
            var collectionType = RenderTypename(GetFirstTypeParameter(ms.ReturnType));
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
            handlerFunc = $$"""
                    async ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.Add(await {{ms.Name}}(x));
                        }catch{}
                        return result;
                    }
            """;
        }
        else if (name.StartsWith("ienumerable<"))
        {
            var collectionType = RenderTypename(GetFirstTypeParameter(ms.ReturnType));
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
            handlerFunc = $$"""
                    ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.AddRange({{ms.Name}}(x));
                        }catch{}
                        return result;
                    }
            """;
        }
        else if (name.StartsWith("task<ienumerable"))
        {
            var collectionType = RenderTypename(GetFirstTypeParameter(ms.ReturnType));
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
            handlerFunc = $$"""
                    async ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.AddRange(await {{ms.Name}}(x));
                        }catch{}
                        return result;
                    }
            """;
        }
        else
        {
            var collectionType = RenderTypename(ms.ReturnType);
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
            handlerFunc = $$"""
                    async ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.Add({{ms.Name}}(x));
                        }catch{}
                        return result;
                    }
             """;
        }



        builder.AppendLine($$"""
                    {{_blockName}} = new {{blockType}}({{handlerFunc}},
                        new ExecutionDataflowBlockOptions() {
                            BoundedCapacity = {{capacity}},
                            MaxDegreeOfParallelism = {{maxParallelism}}
                    });
            """);
        builder.AppendLine($"        RegisterChild({_blockName});");
    }

    private static ITypeSymbol? GetFirstTypeParameter(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nts && nts.TypeArguments.Length > 0)
        {
            return nts.TypeArguments[0];
        }

        return default;
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
                    return RenderTypename(nts.TypeArguments[0]);
                }
                return RenderTypename(returnType);
            }
            return RenderTypename(fm!.ReturnType);
        }
        else
        {
            return string.Empty;
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
}