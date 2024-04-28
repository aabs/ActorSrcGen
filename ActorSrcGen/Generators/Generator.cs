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

    private static void GenerateBlockCreation(StringBuilder builder, IMethodSymbol? ms)
    {
        string _blockName = $"_{ms.Name}";
        string inputTypeName = ms.Parameters.First().Type.RenderTypename();
        string outputTypeName = ms.ReturnType.RenderTypename(true);
        const int capacity = 5;
        const int maxParallelism = 8;
        var blockType = $"TransformBlock<{inputTypeName}, {outputTypeName}>";
        if (outputTypeName.Equals("void", StringComparison.InvariantCultureIgnoreCase))
        {
            blockType = $"ActionBlock<{inputTypeName}>";
        }

        // The logic for the TLEH: 1) If the type is "void", just trap exceptions and do nothing. 2)
        // If the type is "IEnumerable<T>", just create a receiver collection, and receive into
        // that. 3) If the type is "Task<T>", create a new signature for "Task<IEnumerable<T>>" and
        // return empty collection on error. 4) if the type is "T", create a new signature for
        // "Task<IEnumerable<T>>" and return empty collection on error.
        var name = ms.ReturnType.RenderTypename(false).ToLowerInvariant();
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
            var collectionType = GetFirstTypeParameter(ms.ReturnType).RenderTypename();
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
            var collectionType = GetFirstTypeParameter(ms.ReturnType).RenderTypename();
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
            var collectionType = GetFirstTypeParameter(ms.ReturnType).RenderTypename();
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
            var collectionType = ms.ReturnType.RenderTypename();
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

    private static void GenerateBlockDeclaration(StringBuilder builder, IMethodSymbol ms)
    {
        string blockName = $"_{ms.Name}";
        string inputTypeName = ms.Parameters.First().Type.RenderTypename();
        string outputTypeName = ms.ReturnType.RenderTypename(true);
        // generate the block decl
        var blockType = $"TransformBlock<{inputTypeName}, {outputTypeName}>";
        var name = ms.ReturnType.RenderTypename(false).ToLowerInvariant();

        if (name == "void")
        {
            blockType = $"ActionBlock<{inputTypeName}>";
        }
        else if (name.StartsWith("task<"))
        {
            var collectionType = GetFirstTypeParameter(ms.ReturnType).RenderTypename();
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        else if (name.StartsWith("ienumerable<"))
        {
            var collectionType = GetFirstTypeParameter(ms.ReturnType).RenderTypename();
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        else if (name.StartsWith("task<ienumerable"))
        {
            var collectionType = GetFirstTypeParameter(ms.ReturnType).RenderTypename();
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        else
        {
            var collectionType = ms.ReturnType.RenderTypename();
            blockType = $"TransformManyBlock<{inputTypeName}, {collectionType}>";
        }
        builder.AppendLine($"    {blockType} {blockName};");
        // generate the wrapper function
    }

    private static void GenerateBlockLinkage(StringBuilder builder, IMethodSymbol srcMethod, Dictionary<IMethodSymbol, List<IMethodSymbol>> dependencyGraph)
    {
        var srcBlockName = "_" + srcMethod.Name;
        foreach (var m in dependencyGraph[srcMethod])
        {
            var targetBlockName = "_" + m.Name;
            builder.AppendLine($"        {srcBlockName}.LinkTo({targetBlockName}, new DataflowLinkOptions {{ PropagateCompletion = true }});");
        }
    }

    private static IMethodSymbol[] GenerateCtor(GenerationContext ctx, StringBuilder builder)
    {
        // start the ctor
        builder.AppendLine($"    public {ctx.Name}() : base(DataflowOptions.Default)");
        builder.AppendLine("    {");
        // create the blocks
        var methods = GetStepMethods(ctx.ActorClass.Symbol).ToArray();
        foreach (var method in methods)
        {
            GenerateBlockCreation(builder, method);
        }

        // create the linkage
        foreach (var methodName in ctx.DependencyGraph.Keys)
        {
            GenerateBlockLinkage(builder, methodName, ctx.DependencyGraph);
        }

        // end the ctor
        builder.AppendLine("    }");
        return methods;
    }

    private static void GenerateHeaders(StringBuilder builder, SyntaxAndSymbol input)
    {
        // header stuff
        builder.AppendHeader(input.Syntax, input.Symbol);
        builder.AppendLine("using System.Threading.Tasks.Dataflow;");
        builder.AppendLine("using Gridsum.DataflowEx;");
        builder.AppendLine();
    }

    private static ITypeSymbol? GetFirstTypeParameter(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nts && nts.TypeArguments.Length > 0)
        {
            return nts.TypeArguments[0];
        }

        return default;
    }

    private static IEnumerable<IMethodSymbol> GetStepMethods(INamedTypeSymbol typeSymbol)
    {
        return from m in typeSymbol.GetMembers()
               let ms = m as IMethodSymbol
               where ms is not null
               where ms.GetBlockAttr() is not null
               where ms.Name != ".ctor"
               select ms;
    }

    private Dictionary<IMethodSymbol, List<IMethodSymbol>> BuildDependencyGraph(SyntaxAndSymbol ss)
    {
        var methods = GetStepMethods(ss.Symbol).ToArray();
        //var methods = (from fromStep in ss.Symbol.GetMembers()
        //               let ms = fromStep as IMethodSymbol
        //               where ms is not null
        //               where ms.GetAttributes().Any(a => a.AttributeClass.Name.EndsWith("StepAttribute"))
        //               where ms.Name != ".ctor"
        //               select ms).ToArray();

        var deps = new Dictionary<IMethodSymbol, List<IMethodSymbol>>();
        foreach (var fromStep in methods.Where(x => x.GetBlockAttr().AttributeClass.Name != nameof(LastStepAttribute)))
        {
            deps[fromStep] = new();
            foreach (var a in fromStep.GetNextStepAttrs())
            {
                var nextArg = a.GetArg<string>(0);
                var toStep = methods.FirstOrDefault(n => n.Name == nextArg);
                deps[fromStep].Add(toStep);
            }
        }
        return deps;
    }

    private void GenerateClass(StringBuilder builder, SyntaxAndSymbol input, SourceProductionContext context)
    {
        var actorCtx = new GenerationContext(input, GetStartMethods(input), GetEndMethods(input), BuildDependencyGraph(input));
        var inputTypes = string.Join(", ", actorCtx.InputTypeNames.ToArray());

        // validation: if there are multiple input types provided, they must all be distinct to
        //             allow supplying inputs to the right input port of the actor
        if (actorCtx.HasMultipleInputTypes && !actorCtx.HasDisjointInputTypes)
        {
            var dd = new DiagnosticDescriptor(
                    "ASG0001",
                    "Actor with multiple input types must be disjoint",
                    "Actor {0} accepts inputs of type '{1}'. All types must be distinct.",
                    "types",
                    DiagnosticSeverity.Error,
                    true);
            Diagnostic diagnostic = Diagnostic.Create(dd, Location.None, actorCtx.Name, inputTypes);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        var baseClass = $"Dataflow";
        if (actorCtx.HasSingleInputType && actorCtx.HasSingleOutputType)
        {
            baseClass = $"Dataflow<{actorCtx.InputTypeNames.First()}, {actorCtx.OutputTypeNames.First()}>";
        }
        else if (actorCtx.HasSingleInputType)
        {
            baseClass = $"Dataflow<{actorCtx.InputTypeNames.First()}>";
        }

        // start the class
        builder.AppendLine($"public partial class {actorCtx.Name} : {baseClass}, IActor<{inputTypes}>");
        builder.AppendLine("{");
        builder.AppendLine();
        IMethodSymbol[] methods = GenerateCtor(actorCtx, builder);
        // foreach srcMethod
        foreach (var ms in methods)
        {
            GenerateBlockDeclaration(builder, ms);
            builder.AppendLine();
        } // foreach member

        GenerateIOBlockAccessors(builder, actorCtx);
        builder.AppendLine();
        GeneratePostMethods(builder, actorCtx);
        GenerateResultReceivers(builder, actorCtx);
        builder.AppendLine();
        // end the class
        builder.AppendLine("}");
    }

    private void GenerateResultReceivers(StringBuilder builder, GenerationContext ctx)
    {
        foreach (var om in ctx.OutputMethods) // non void end methods
        {
            var outputTypeName = om.ReturnType.RenderTypename(true);
            var blockName = $"_{om.Name}";
            var receiverMethodName = $"Receive{om.Name}Async".Replace("AsyncAsync", "Async");

            builder.AppendLine($$"""
                public async Task<{{outputTypeName}}> {{receiverMethodName}}(CancellationToken cancellationToken)
                {
                    var result = await {{blockName}}.ReceiveAsync(cancellationToken);
                    return result;
                }
            """);
        }
    }

    private void GenerateIOBlockAccessors(StringBuilder builder, GenerationContext ctx)
    {
        if (ctx.HasSingleInputType)
        {
            builder.AppendLine($"    public override ITargetBlock<{ctx.InputTypeNames.First()}> InputBlock {{ get => _{ctx.StartMethods.First().Name}; }}");
        }
        else
        {
            foreach (var t in ctx.InputTypeNames)
            {
                var startMethod = ctx.StartMethods.First(i => i.Name.Equals(t));
                builder.AppendLine($"    public ITargetBlock<{t}> {startMethod.Name}InputBlock {{ get => _{startMethod.Name}; }}");
            }
        }
        if (ctx.OutputMethods.Any())
        {
            if (ctx.HasSingleOutputType)
            {
                var endMethod = ctx.EndMethods.First();
                builder.AppendLine($"    public override ISourceBlock<{ctx.OutputTypeNames.First()}> OutputBlock {{ get => _{endMethod.Name}; }}");
            }
            else
            {
                foreach (var em in ctx.EndMethods)
                {
                    var rt = em.ReturnType.RenderTypename();
                    builder.AppendLine($"    public ITargetBlock<{rt}> {em.Name}OutputBlock {{ get => _{em.Name}; }}");
                }
            }

        }
    }

    private void GeneratePostMethods(StringBuilder builder, GenerationContext ctx)
    {
        foreach (var inputType in ctx.InputTypeNames)
        {
            builder.AppendLine($$"""
                    public bool Call({{inputType}} input)
                    => InputBlock.Post(input);
                """);
            builder.AppendLine();
            builder.AppendLine($$"""
                    public async Task<bool> Cast({{inputType}} input)
                    => await InputBlock.SendAsync(input);
                """);
        }
    }

    private IEnumerable<IMethodSymbol> GetEndMethods(SyntaxAndSymbol input)
        => GetMethodsWithAttr(input, nameof(LastStepAttribute));

    private IEnumerable<IMethodSymbol> GetMethodsWithAttr(SyntaxAndSymbol input, string attrName)
    {
        return (from m in input.Symbol.GetMembers()
                let ms = m as IMethodSymbol
                where ms is not null
                where ms.GetAttributes().Any(a => a.AttributeClass.Name.Equals(attrName, StringComparison.InvariantCultureIgnoreCase))
                where ms.Name != ".ctor"
                select ms);
    }

    private IEnumerable<IMethodSymbol> GetStartMethods(SyntaxAndSymbol input)
        => GetMethodsWithAttr(input, nameof(FirstStepAttribute));

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
            StringBuilder sb = new StringBuilder();
            GenerateHeaders(sb, input);
            GenerateClass(sb, input, context);
            context.AddSource($"{typeSymbol.Name}.generated.cs", sb.ToString());
#if DEBUG
            Console.WriteLine(sb.ToString());
#endif
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while generating source: " + e.Message); ;
        }
    }
}