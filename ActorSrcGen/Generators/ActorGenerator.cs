using System;
using System.Collections.Generic;
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

        if (!actor.HasAnyInputTypes || (actor.HasMultipleInputTypes && !actor.HasDisjointInputTypes))
        {
            // Diagnostics should already be produced by ActorVisitor; skip emission here to avoid duplication.
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

        foreach (var step in ctx.Actor.StepNodes.OrderBy(s => s.Id))
        {
            GenerateBlockInstantiation(ctx, step);
        }
        GenerateBlockLinkage(ctx);
        builder.AppendLine("    }");

        #endregion Gen ctor

        #region Gen Block Decls

        foreach (var step in actor.StepNodes.OrderBy(s => s.Id))
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
        GenerateIngestMethods(ctx);
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
            var methodReturnTypeName = step.NodeType == NodeType.TransformMany
                ? step.Method.ReturnType.RenderTypename(true, true)
                : step.Method.ReturnType.RenderTypename(true);
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

    private static string ChooseMethodBody(BlockNode step)
    {
        var method = step.Method;
        var parameterType = method.Parameters.FirstOrDefault()?.Type.RenderTypename(true) ?? "object";
        var asyncKeyword = method.IsAsync || method.ReturnType.Name == "Task" || method.ReturnType.Name.StartsWith("Task<", StringComparison.Ordinal) ? "async " : string.Empty;
        var awaitKeyword = asyncKeyword.Length > 0 ? "await " : string.Empty;

        return step.NodeType switch
        {
            NodeType.Action => $"({parameterType} x) => {method.Name}(x)",
            NodeType.Transform => $"{asyncKeyword}({parameterType} x) => {awaitKeyword}{method.Name}(x)",
            NodeType.TransformMany => $"{asyncKeyword}({parameterType} x) => {awaitKeyword}{method.Name}(x)",
            NodeType.Broadcast => $"({method.ReturnType.RenderTypename(true)} x) => x",
            _ => $"{asyncKeyword}({parameterType} x) => {awaitKeyword}{method.Name}(x)",
        };
    }

    private static string ChooseBlockName(BlockNode step)
    {
        return $"_{step.Method.Name}" + (step.NodeType == NodeType.Broadcast ? "BC" : "");
    }

    private static string ChooseBroadcastBlockName(BlockNode step)
    {
        var name = ChooseBlockName(step);
        return name.EndsWith("BC", StringComparison.Ordinal) ? name : name + "BC";
    }

    private static void GenerateBlockDeclaration(BlockNode step, ActorGenerationContext ctx)
    {
        var blockName = ChooseBlockName(step);
        var blockType = ChooseBlockType(step);
        ctx.Builder.AppendLine($"""

            {blockType} {blockName};
        """);

        if (step.NextBlocks.Length > 1)
        {
            var broadcastBlockName = ChooseBroadcastBlockName(step);
            var outputType = step.Method.ReturnType.RenderTypename(true);
            ctx.Builder.AppendLine($"""

            BroadcastBlock<{outputType}> {broadcastBlockName};
        """);
        }
    }

    private static void GenerateBlockLinkage(ActorGenerationContext ctx)
    {
        var builder = ctx.Builder;
        var actor = ctx.Actor;

        foreach (var step in ctx.Actor.StepNodes.OrderBy(s => s.Id))
        {
            var blockName = step.NextBlocks.Length > 1 ? ChooseBroadcastBlockName(step) : ChooseBlockName(step);
            var outNodes = actor.StepNodes.Where(sn => step.NextBlocks.Contains(sn.Id));
            foreach (var outNode in outNodes.OrderBy(n => n.Id))
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
        var handlerBody = ChooseMethodBody(step);
        builder.AppendLine($$"""
                    {{blockName}} = new {{blockTypeName}}({{handlerBody}},
                        new ExecutionDataflowBlockOptions() {
                            BoundedCapacity = {{capacity}},
                            MaxDegreeOfParallelism = {{maxParallelism}}
                    });
                    RegisterChild({{blockName}});
            """);

        if (step.NextBlocks.Length > 1)
        {
            var broadcastBlockName = ChooseBroadcastBlockName(step);
            var outputType = step.Method.ReturnType.RenderTypename(true);
            builder.AppendLine($$"""
                {{broadcastBlockName}} = new BroadcastBlock<{{outputType}}>(x => x);
                RegisterChild({{broadcastBlockName}});
                {{blockName}}.LinkTo({{broadcastBlockName}}, new DataflowLinkOptions { PropagateCompletion = true });
            """);
        }
    }

    private void GenerateIoBlockAccessors(ActorGenerationContext ctx)
    {
        if (ctx.HasSingleInputType)
        {
            var firstInputType = ctx.InputTypeNames.FirstOrDefault();
            var firstEntryNode = ctx.Actor.EntryNodes.OrderBy(n => n.Id).FirstOrDefault();
            if (firstInputType != null && firstEntryNode != null)
            {
                ctx.Builder.AppendLine($$"""
                    public override ITargetBlock<{{firstInputType}}> InputBlock { get => _{{firstEntryNode.Method.Name}}; }
                """);
            }
        }
        else
        {
            foreach (var en in ctx.Actor.EntryNodes.OrderBy(n => n.Id))
            {
                ctx.Builder.AppendLine($$"""
                    public ITargetBlock<{{en.InputTypeName}}> {{en.Method.Name}}InputBlock { get => _{{en.Method.Name}}; }
                """);
            }
        }
        if (ctx.OutputMethods.Any())
        {
            if (ctx.HasSingleOutputType)
            {
                var step = ctx.Actor.ExitNodes.Where(x => !x.Method.ReturnsVoid).OrderBy(n => n.Id).FirstOrDefault();
                if (step != null)
                {
                    var rt = step.Method.ReturnType.RenderTypename(true);
                    var stepName = ChooseBlockName(step);
                    ctx.Builder.AppendLine($"    public override ISourceBlock<{rt}> OutputBlock {{ get => {stepName}; }}");
                }
            }
            else
            {
                foreach (var step in ctx.Actor.ExitNodes.OrderBy(n => n.Id))
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
            foreach (var step in ctx.Actor.EntryNodes.OrderBy(n => n.Id))
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
        foreach (var step in ctx.Actor.ExitNodes.Where(x => !x.Method.ReturnsVoid).OrderBy(n => n.Id)) // non void end methods
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

    private void GenerateIngestMethods(ActorGenerationContext ctx)
    {
        if (!ctx.Actor.Ingesters.Any())
        {
            return;
        }

        var ingesters = ctx.Actor.Ingesters
            .OrderBy(i => i.Priority)
            .ThenBy(i => i.Method.Name, StringComparer.Ordinal);

        ctx.Builder.AppendLine("    public async Task Ingest(CancellationToken cancellationToken)");
        ctx.Builder.AppendLine("    {");
        ctx.Builder.AppendLine("        var ingestTasks = new List<Task>();");

        foreach (var ingest in ingesters)
        {
            var outputType = ingest.Method.ReturnType.RenderTypename(stripTask: true, stripCollection: true);
            var entry = ctx.Actor.EntryNodes.FirstOrDefault(e => string.Equals(e.InputTypeName, outputType, StringComparison.Ordinal));
            var postMethod = ctx.HasMultipleInputTypes && entry is not null
                ? $"Call{entry.Method.Name}"
                : "Call";

            var returnsAsyncEnumerable = string.Equals(ingest.Method.ReturnType.Name, "IAsyncEnumerable", StringComparison.Ordinal);

            if (returnsAsyncEnumerable)
            {
                ctx.Builder.AppendLine("        ingestTasks.Add(Task.Run(async () =>");
                ctx.Builder.AppendLine("        {");
                ctx.Builder.AppendLine($"            await foreach (var result in {ingest.Method.Name}(cancellationToken))");
                ctx.Builder.AppendLine("            {");
                ctx.Builder.AppendLine("                if (result is not null)");
                ctx.Builder.AppendLine("                {");
                ctx.Builder.AppendLine($"                    {postMethod}(result);");
                ctx.Builder.AppendLine("                }");
                ctx.Builder.AppendLine("            }");
                ctx.Builder.AppendLine("        }, cancellationToken));");
            }
            else
            {
                ctx.Builder.AppendLine("        ingestTasks.Add(Task.Run(async () =>");
                ctx.Builder.AppendLine("        {");
                ctx.Builder.AppendLine("            while (!cancellationToken.IsCancellationRequested)");
                ctx.Builder.AppendLine("            {");
                ctx.Builder.AppendLine($"                var result = await {ingest.Method.Name}(cancellationToken);");
                ctx.Builder.AppendLine("                if (result is not null)");
                ctx.Builder.AppendLine("                {");
                ctx.Builder.AppendLine($"                    {postMethod}(result);");
                ctx.Builder.AppendLine("                }");
                ctx.Builder.AppendLine("            }");
                ctx.Builder.AppendLine("        }, cancellationToken));");
            }
        }

        ctx.Builder.AppendLine("        await Task.WhenAll(ingestTasks);");
        ctx.Builder.AppendLine("    }");
    }
}