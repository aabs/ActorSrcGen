using System.Text;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;

namespace ActorSrcGen.Generators;

public class ActorGenerator(SourceProductionContext context)
{
    public StringBuilder Builder { get; } = new();

    public void GenerateActor(ActorNode actor)
    {
        var ctx = new ActorGenerationContext(actor, Builder, context);
        var input = actor.Symbol;
        var builder = ctx.Builder;

        #region Gen Headers

        builder.AppendHeader(input.Syntax, input.Symbol);

        builder.AppendLine("""
                           using System.Threading.Tasks.Dataflow;
                           using Gridsum.DataflowEx;
                           """);

        #endregion Gen Headers

        #region Validate Actor Syntax/Semantics

        var inputTypes = string.Join(", ", actor.InputTypes.Select(t => t.RenderTypename(true)).ToArray());
        var hasValidationErrors = false;

        // validation: check for empty input types
        if (!actor.HasAnyInputTypes)
        {
            var dd = new DiagnosticDescriptor(
                    "ASG0002",
                    "Actor must have at least one input type",
                    "Actor {0} does not have any input types defined. At least one entry method is required.",
                    "types",
                    DiagnosticSeverity.Error,
                    true);
            Diagnostic diagnostic = Diagnostic.Create(dd, Location.None, actor.Name);
            context.ReportDiagnostic(diagnostic);
            hasValidationErrors = true;
        }

        // validation: if there are multiple input types provided, they must all be distinct to
        // allow supplying inputs to the right input port of the actor
        if (actor is { HasMultipleInputTypes: true, HasDisjointInputTypes: false })
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
            hasValidationErrors = true;
        }

        // Return early if there were any validation errors
        if (hasValidationErrors)
        {
            return;
        }

        #endregion Validate Actor Syntax/Semantics

        #region Gen Class Decl

        var baseClass = "Dataflow";
        var inputTypeName = actor.InputTypes.FirstOrDefault()?.RenderTypename(true);
        var outputTypeName = actor.OutputTypes.FirstOrDefault()?.RenderTypename(true);

        if (actor is { HasSingleInputType: true, HasAnyOutputTypes: true } && inputTypeName != null && outputTypeName != null)
        {
            baseClass = $"Dataflow<{inputTypeName}, {outputTypeName}>";
        }
        else if (actor.HasSingleInputType && inputTypeName != null)
        {
            baseClass = $"Dataflow<{inputTypeName}>";
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
        builder.AppendLine("    }");

        #endregion Gen ctor

        #region Gen Block Decls

        foreach (var step in actor.StepNodes)
        {
            GenerateBlockDeclaration(step, ctx);
        }

        #endregion Gen Block Decls

        #region Gen Receivers

        foreach (var step in actor.EntryNodes)
        {
            if (step.Method.GetAttributes().Any(a => a.AttributeClass is { Name: nameof(ReceiverAttribute) }))
            {
                GenerateReceiverMethod(step, ctx);
            }
        }

        #endregion Gen Receivers

        GenerateIoBlockAccessors(ctx);
        GeneratePostMethods(ctx);
        GenerateResultAcceptors(ctx);
        builder.AppendLine("}");
    }

    private void GenerateReceiverMethod(BlockNode step, ActorGenerationContext ctx)
    {
        var builder = ctx.Builder;
        var methodName = $"Receive{step.Method.Name}";
        var stepInputTypeName = step.InputTypeName;
        builder.AppendLine($"    protected partial Task<{stepInputTypeName}> {methodName}(CancellationToken cancellationToken);");

        var continuousMethodName = $"ListenFor{methodName}";
        var postMethodName = "Call";
        if (ctx.HasMultipleInputTypes)
        {
            postMethodName = $"Call{step.Method.Name}";
        }

        builder.AppendLine($$"""
                                 public async Task {{continuousMethodName}}(CancellationToken cancellationToken)
                                 {
                                     while (!cancellationToken.IsCancellationRequested)
                                     {
                                         {{stepInputTypeName}} incomingValue = await {{methodName}}(cancellationToken);
                                         {{postMethodName}}(incomingValue);
                                     }
                                 }
                             """);
    }

    private static string ChooseBlockType(BlockNode step)
    {
        var sb = new StringBuilder();
        sb.Append(GetBlockBaseType(step));

        var methodFirstParamTypeName = step.Method.Parameters.First().Type.RenderTypename(true);
        if (step.NodeType == NodeType.Action)
        {
            sb.AppendFormat("<{0}>", methodFirstParamTypeName);
        }
        else
        {
            var methodReturnTypeName = step.Method.ReturnType.RenderTypename(true);
            if (step.NodeType == NodeType.Broadcast)
            {
                sb.AppendFormat("<{0}>", methodReturnTypeName);
            }
            else
            {
                sb.AppendFormat("<{0},{1}>", methodFirstParamTypeName,
                    methodReturnTypeName);
            }
        }

        return sb.ToString();
    }

    private static void ChooseMethodBody(BlockNode step)
    {
        // The logic for the TLEH: 1) If the type is "void", just trap exceptions and do nothing. 2)
        // If the type is "IEnumerable<T>", just create a receiver collection, and receive into
        // that. 3) If the type is "Task<T>", create a new signature for "Task<IEnumerable<T>>" and
        // return empty collection on error. 4) if the type is "T", create a new signature for
        // "Task<IEnumerable<T>>" and return empty collection on error.

        var ms = step.Method;
        var stepInputType = step.Method.Parameters.First().Type.RenderTypename(true);
        var stepResultType = step.Method.ReturnType.RenderTypename(true);
        var isAsync = ms.IsAsync || step.Method.ReturnType.RenderTypename().StartsWith("Task<", StringComparison.InvariantCultureIgnoreCase);
        var asyncer = isAsync ? "async" : "";
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
                    {{asyncer}} ({{stepInputType}} x) => {
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
                       {{asyncer}} ({{stepInputType}} x) => {
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
                    {{asyncer}} ({{stepInputType}} x) => {
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
                    {{asyncer}} ({{stepInputType}} x) => {
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
        ctx.Builder.AppendLine($"""

            {blockType} {blockName};
        """);
    }

    private static void GenerateBlockLinkage(ActorGenerationContext ctx)
    {
        var builder = ctx.Builder;
        var actor = ctx.Actor;

        foreach (var step in ctx.Actor.StepNodes)
        {
            var blockName = ChooseBlockName(step);
            var outNodes = actor.StepNodes.Where(sn => step.NextBlocks.Contains(sn.Id));
            foreach (var outNode in outNodes)
            {
                string targetBlockName = ChooseBlockName(outNode);
                builder.AppendLine($"        {blockName}.LinkTo({targetBlockName}, new DataflowLinkOptions {{ PropagateCompletion = true }});");
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

    private void GenerateBlockInstantiation(ActorGenerationContext ctx, BlockNode step)
    {
        ChooseMethodBody(step);
        var builder = ctx.Builder;
        const int capacity = 5;
        const int maxParallelism = 8;

        string blockName = ChooseBlockName(step);
        string blockTypeName = ChooseBlockType(step);
        builder.AppendLine($$"""
                    {{blockName}} = new {{blockTypeName}}({{step.HandlerBody}},
                        new ExecutionDataflowBlockOptions() {
                            BoundedCapacity = {{capacity}},
                            MaxDegreeOfParallelism = {{maxParallelism}}
                    });
                    RegisterChild({{blockName}});
            """);
    }

    private void GenerateIoBlockAccessors(ActorGenerationContext ctx)
    {
        if (ctx.HasSingleInputType)
        {
            var firstInputType = ctx.InputTypeNames.FirstOrDefault();
            var firstEntryNode = ctx.Actor.EntryNodes.FirstOrDefault();
            if (firstInputType != null && firstEntryNode != null)
            {
                ctx.Builder.AppendLine($$"""
                    public override ITargetBlock<{{firstInputType}}> InputBlock { get => _{{firstEntryNode.Method.Name}}; }
                """);
            }
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
                var step = ctx.Actor.ExitNodes.FirstOrDefault(x => !x.Method.ReturnsVoid);
                if (step != null)
                {
                    var rt = step.Method.ReturnType.RenderTypename(true);
                    var stepName = ChooseBlockName(step);
                    ctx.Builder.AppendLine($"    public override ISourceBlock<{rt}> OutputBlock {{ get => {stepName}; }}");
                }
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
            var inputType = ctx.InputTypeNames.FirstOrDefault();
            if (inputType != null)
            {
                ctx.Builder.AppendLine($$"""
                        public bool Call({{inputType}} input)
                            => InputBlock.Post(input);

                        public async Task<bool> Cast({{inputType}} input)
                            => await InputBlock.SendAsync(input);
                    """);
            }
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

    private void GenerateResultAcceptors(ActorGenerationContext ctx)
    {
        foreach (var step in ctx.Actor.ExitNodes.Where(x => !x.Method.ReturnsVoid)) // non void end methods
        {
            var om = step.Method;
            var outputTypeName = om.ReturnType.RenderTypename(true);
            var blockName = ChooseBlockName(step);
            var receiverMethodName = $"Accept{om.Name}Async".Replace("AsyncAsync", "Async");
            if (ctx.Actor.HasSingleOutputType)
                receiverMethodName = "AcceptAsync";
            ctx.Builder.AppendLine($$"""
                public async Task<{{outputTypeName}}> {{receiverMethodName}}(CancellationToken cancellationToken)
                {
                    try
                    {
                        var result = await {{blockName}}.ReceiveAsync(cancellationToken);
                        return result;
                    }
                    catch (OperationCanceledException operationCanceledException)
                    {
                        return await Task.FromCanceled<{{outputTypeName}}>(cancellationToken);
                    }
                }
            """);
        }
    }
}