using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;
using System.Text;

namespace ActorSrcGen.Generators;

public class ActorGenerator
{
    private readonly SourceProductionContext context;

    public ActorGenerator(SourceProductionContext context)
    {
        this.context = context;
        this.Builder = new StringBuilder();
    }

    public StringBuilder Builder { get; }

    public void GenerateActor(ActorNode actor)
    {
        var ctx = new ActorGenerationContext(actor, Builder, context);
        var input = actor.Symbol;
        var builder = ctx.Builder;

        #region Gen Headers

        builder.AppendHeader(input.Syntax, input.Symbol);

        builder.AppendLine($$"""
            using System.Threading.Tasks.Dataflow;
            using Gridsum.DataflowEx;
            """);

        #endregion Gen Headers

        #region Validate Actor Syntax/Semantics

        var inputTypes = string.Join(", ", actor.InputTypeNames.ToArray());

        // validation: if there are multiple input types provided, they must all be distinct to
        // allow supplying inputs to the right input port of the actor
        if (actor.HasMultipleInputTypes && !actor.HasDisjointInputTypes)
        {
            var dd = new DiagnosticDescriptor(
                    "ASG0001",
                    "Actor with multiple input types must be disjoint",
                    "Actor {0} accepts inputs of type '{1}'. All types must be distinct.",
                    "types",
                    DiagnosticSeverity.Error,
                    true);
            Diagnostic diagnostic = Diagnostic.Create(dd, Location.None, actor.Name, inputTypes);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        #endregion Validate Actor Syntax/Semantics

        #region Gen Class Decl

        var baseClass = $"Dataflow";
        if (actor.HasSingleInputType && actor.HasSingleOutputType)
        {
            baseClass = $"Dataflow<{actor.InputTypeNames.First()}, {actor.OutputMethods.First().ReturnType.RenderTypename(true)}>";
        }
        else if (actor.HasSingleInputType)
        {
            baseClass = $"Dataflow<{actor.InputTypeNames.First()}>";
        }

        builder.AppendLine($$"""
        public partial class {{actor.Name}} : {{baseClass}}, IActor<{{inputTypes}}>
        {
        """);

        #endregion Gen Class Decl

        #region Gen ctor

        builder.AppendLine($$"""
            public {{ctx.Name}}() : base(DataflowOptions.Default)
            {
        """);

        foreach (var step in ctx.Actor.StepNodes)
        {
            GenerateBlockInstantiation(ctx, step);
        }
        GenerateBlockLinkage(ctx);
        builder.AppendLine($$"""
            }
        """);

        #endregion Gen ctor

        #region Gen Block Decls

        foreach (var step in actor.StepNodes)
        {
            GenerateBlockDeclaration(step, ctx);
        }

        #endregion Gen Block Decls

        GenerateIOBlockAccessors(ctx);
        GeneratePostMethods(ctx);
        GenerateResultReceivers(ctx);
        builder.AppendLine("}");
    }
    private static string ChooseBlockType(BlockNode step)
    {
        var sb = new StringBuilder();
        sb.Append(GetBlockBaseType(step));

        if (step.NodeType == NodeType.Action)
        {
            sb.AppendFormat("<{0}>", step.Method.Parameters.First().Type.RenderTypename(true));
        }
        else
        if (step.NodeType == NodeType.Broadcast)
        {
            sb.AppendFormat("<{0}>", step.Method.ReturnType.RenderTypename(true));
        }
        else
        {
            sb.AppendFormat("<{0},{1}>", step.Method.Parameters.First().Type.RenderTypename(true),
                step.Method.ReturnType.RenderTypename(true));
        }

        return sb.ToString();
    }
    private static void ChooseMethodBody(ActorGenerationContext ctx, BlockNode step)
    {
        // The logic for the TLEH: 1) If the type is "void", just trap exceptions and do nothing. 2)
        // If the type is "IEnumerable<T>", just create a receiver collection, and receive into
        // that. 3) If the type is "Task<T>", create a new signature for "Task<IEnumerable<T>>" and
        // return empty collection on error. 4) if the type is "T", create a new signature for
        // "Task<IEnumerable<T>>" and return empty collection on error.

        var builder = ctx.Builder;
        var ms = step.Method;
        var actor = ctx.Actor;

        var stepInputType = step.Method.Parameters.First().Type.RenderTypename(true);
        var stepResultType = step.Method.ReturnType.RenderTypename(true);

        var isAsync = ms.IsAsync || step.Method.ReturnType.RenderTypename(false).StartsWith("Task<", StringComparison.InvariantCultureIgnoreCase);
        var prefix = isAsync ? "async" : "";
        var awaiter = isAsync ? "await" : "";

        switch (step.NodeType)
        {
            case NodeType.Action:
                step.HandlerBody = $$"""
                    ({{stepInputType}} x) => {
                        try
                        {
                            {{step.Method.Name}}(x);
                        }catch{}
                    }
            """;
                break;

            case NodeType.Batch:
                step.HandlerBody = $$"""
                    ({{stepInputType}} x) => {
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
                break;

            case NodeType.BatchedJoin:
                step.HandlerBody = $$"""
                    ({{stepInputType}} x) => {
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
                break;

            case NodeType.Buffer:
                step.HandlerBody = $$"""
                    ({{stepInputType}} x) => {
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
                break;

            case NodeType.Transform:
                step.HandlerBody = $$"""
                    {{prefix}} ({{stepInputType}} x) => {
                        var result = new List<{{stepResultType}}>();
                        try
                        {
                            var newValue = {{awaiter}} {{ms.Name}}(x);
                            result.Add(newValue);
                        }catch{}
                        return result;
                    }
             """;
                break;

            case NodeType.TransformMany:
                step.HandlerBody = $$"""
                       {{prefix}} ({{stepInputType}} x) => {
                           var result = new List<{{stepResultType}}>();
                           try
                           {
                               var newValue = {{awaiter}} {{ms.Name}}(x);
                               result.Add(newValue);
                           }catch{}
                           return result;
                       }
                """;
                break;

            case NodeType.Broadcast:
                stepInputType = step.Method.ReturnType.RenderTypename(true);
                step.HandlerBody = $$"""
                    ({{stepInputType}} x) => x
                """;
                break;

            case NodeType.Join:
                step.HandlerBody = $$"""
                    {{prefix}} ({{stepInputType}} x) => {
                        var result = new List<{{stepResultType}}>();
                        try
                        {
                            var newValue = {{awaiter}} {{ms.Name}}(x);
                            result.Add(newValue);
                        }catch{}
                        return result;
                    }
             """;
                break;

            case NodeType.WriteOnce:
                step.HandlerBody = $$"""
                    {{prefix}} ({{stepInputType}} x) => {
                        var result = new List<{{stepResultType}}>();
                        try
                        {
                            var newValue = {{awaiter}} {{ms.Name}}(x);
                            result.Add(newValue);
                        }catch{}
                        return result;
                    }
             """;
                break;

            default:
                step.HandlerBody = $$"""
                       async ({{stepInputType}} x) => {
                           var result = new List<{{stepResultType}}>();
                           try
                           {
                               result.Add({{ms.Name}}(x));
                           }catch{}
                           return result;
                       }
                """;
                break;
        }
    }
    private static string ChooseBlockName(BlockNode step)
    {
        return $"_{step.Method.Name}" + (step.NodeType == NodeType.Broadcast ? "BC" : "");
    }
    private static void GenerateBlockDeclaration(BlockNode step, ActorGenerationContext ctx)
    {
        var blockName = ChooseBlockName(step);
        var blockType = ChooseBlockType(step);
        ctx.Builder.AppendLine($$"""

            {{blockType}} {{blockName}};
        """);
    }

    private static void GenerateBlockLinkage(ActorGenerationContext ctx)
    {
        var input = ctx.Actor.Symbol;
        var builder = ctx.Builder;
        var actor = ctx.Actor;

        foreach (var step in ctx.Actor.StepNodes)
        {
            string _blockName = ChooseBlockName(step);

            var outNodes = actor.StepNodes.Where(sn => step.NextBlocks.Contains(sn.Id));
            foreach (var outNode in outNodes)
            {
                string targetBlockName = ChooseBlockName(outNode);
                builder.AppendLine($"        {_blockName}.LinkTo({targetBlockName}, new DataflowLinkOptions {{ PropagateCompletion = true }});");
            }
        }
    }

    private static string GetBlockBaseType(BlockNode step)
    {
        return step.NodeType switch
        {
            NodeType.Action => "ActionBlock",
            NodeType.Batch => "TransformBlock",
            NodeType.BatchedJoin => "BatchedJoinBlock",
            NodeType.Buffer => "BufferBlock",
            NodeType.Transform => "TransformBlock",
            NodeType.TransformMany => "TransformManyBlock",
            NodeType.Broadcast => "BroadcastBlock",
            NodeType.Join => "JoinBlock",
            NodeType.WriteOnce => "WriteOnceBlock",
            _ => "TransformBlock",
        };
    }

    private static ITypeSymbol? GetFirstTypeParameter(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nts && nts.TypeArguments.Length > 0)
        {
            return nts.TypeArguments[0];
        }

        return default;
    }

    private void GenerateBlockInstantiation(ActorGenerationContext ctx, BlockNode step)
    {
        ChooseMethodBody(ctx, step);
        var builder = ctx.Builder;
        var ms = step.Method;
        var actor = ctx.Actor;
        const int capacity = 5;
        const int maxParallelism = 8;

        string _blockName = ChooseBlockName(step);
        string blockTypeName = ChooseBlockType(step);
        builder.AppendLine($$"""
                    {{_blockName}} = new {{blockTypeName}}({{step.HandlerBody}},
                        new ExecutionDataflowBlockOptions() {
                            BoundedCapacity = {{capacity}},
                            MaxDegreeOfParallelism = {{maxParallelism}}
                    });
                    RegisterChild({{_blockName}});
            """);
    }

    private void GenerateIOBlockAccessors(ActorGenerationContext ctx)
    {
        if (ctx.HasSingleInputType)
        {
            ctx.Builder.AppendLine($$"""
                public override ITargetBlock<{{ctx.InputTypeNames.First()}}> InputBlock { get => _{{ctx.Actor.EntryNodes.First().Method.Name}}; }
            """);
        }
        else
        {
            foreach (var en in ctx.Actor.EntryNodes)
            {
                ctx.Builder.AppendLine($$"""
                    public ITargetBlock<{{en.InputTypeName}}> {{en.Method.Name}}InputBlock { get => _{en.Method.Name}; }
                """);
            }
        }
        if (ctx.OutputMethods.Any())
        {
            if (ctx.HasSingleOutputType)
            {
                var step = ctx.Actor.ExitNodes.First(x => !x.Method.ReturnsVoid);
                var rt = step.Method.ReturnType.RenderTypename(true);
                var stepName = ChooseBlockName(step);
                ctx.Builder.AppendLine($"    public override ISourceBlock<{rt}> OutputBlock {{ get => {stepName}; }}");
            }
            else
            {
                foreach (var step in ctx.Actor.ExitNodes)
                {
                    var rt = step.Method.ReturnType.RenderTypename(true);
                    ctx.Builder.AppendLine($$"""
                       public ISourceBlock<{{rt}}> {{step.Method.Name}}OutputBlock { get => _{{step.Method.Name}}; }
                    """);
                }
            }
        }
    }

    private void GeneratePostMethods(ActorGenerationContext ctx)
    {
        if (ctx.HasSingleInputType)
        {
            var inputType = ctx.InputTypeNames.First();
            ctx.Builder.AppendLine($$"""
                    public bool Call({{inputType}} input)
                        => InputBlock.Post(input);

                    public async Task<bool> Cast({{inputType}} input)
                        => await InputBlock.SendAsync(input);
                """);
        }
        else if (ctx.HasMultipleInputTypes)
        {
            foreach (var step in ctx.Actor.EntryNodes)
            {
                var inputType = step.InputTypeName;
                ctx.Builder.AppendLine($$"""
                        public bool Call{{step.Method.Name}}({{inputType}} input)
                            => {{step.Method.Name}}InputBlock.Post(input);

                        public async Task<bool> Cast{{step.Method.Name}}({{inputType}} input)
                            => await {{step.Method.Name}}InputBlock.SendAsync(input);
                    """);
            }
        }
    }

    private void GenerateResultReceivers(ActorGenerationContext ctx)
    {
        foreach (var step in ctx.Actor.ExitNodes.Where(x => !x.Method.ReturnsVoid)) // non void end methods
        {
            var om = step.Method;
            var outputTypeName = om.ReturnType.RenderTypename(true);
            var blockName = ChooseBlockName(step);
            var receiverMethodName = $"Receive{om.Name}Async".Replace("AsyncAsync", "Async");
            if(ctx.Actor.HasSingleOutputType)
                receiverMethodName = $"ReceiveAsync";
            ctx.Builder.AppendLine($$"""
                public async Task<{{outputTypeName}}> {{receiverMethodName}}(CancellationToken cancellationToken)
                {
                    var result = await {{blockName}}.ReceiveAsync(cancellationToken);
                    return result;
                }
            """);
        }
    }
}