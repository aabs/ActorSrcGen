using System;
using System.Collections.Immutable;
using ActorSrcGen.Diagnostics;
using ActorSrcGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Model;

/// <summary>
/// Stateless actor visitor. Safe for concurrent use across threads.
/// </summary>
public sealed class ActorVisitor
{
    public VisitorResult VisitActor(SyntaxAndSymbol input, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new VisitorResult(ImmutableArray<ActorNode>.Empty, ImmutableArray<Diagnostic>.Empty);
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var stepMethods = GetStepMethods(input.Symbol).OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();

        var ingesters = GetIngestMethods(input.Symbol)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .Select(mi => new IngestMethod(mi))
            .ToImmutableArray();

        if (stepMethods.Length == 0)
        {
            diagnostics.Add(ActorSrcGen.Diagnostics.Diagnostics.CreateDiagnostic(ActorSrcGen.Diagnostics.Diagnostics.ASG0001, input.Syntax.GetLocation(), input.Symbol.Name));

            if (ingesters.IsDefaultOrEmpty)
            {
                return new VisitorResult(ImmutableArray<ActorNode>.Empty, diagnostics.ToImmutable());
            }
        }

        var blocks = BuildBlocks(stepMethods);
        var wiredBlocks = WireBlocks(blocks, input.SemanticModel);

        var actor = new ActorNode(wiredBlocks, ingesters, input);

        diagnostics.AddRange(ValidateInputTypes(actor, input));
        diagnostics.AddRange(ValidateStepMethods(actor));
        diagnostics.AddRange(ValidateIngestMethods(ingesters));

        if (!actor.HasAnyInputTypes)
        {
            return new VisitorResult(ImmutableArray<ActorNode>.Empty, diagnostics.ToImmutable());
        }

        return new VisitorResult(ImmutableArray.Create(actor), diagnostics.ToImmutable());
    }

    private static ImmutableArray<BlockNode> BuildBlocks(IEnumerable<IMethodSymbol> methods)
    {
        var blocks = ImmutableArray.CreateBuilder<BlockNode>();
        var blockId = 1;
        foreach (var method in methods)
        {
            var nodeType = ResolveNodeType(method);
            var handlerBody = BuildHandlerBody(method);

            blocks.Add(new BlockNode(
                HandlerBody: handlerBody,
                Id: blockId++,
                Method: method,
                NodeType: nodeType,
                NextBlocks: ImmutableArray<int>.Empty,
                IsEntryStep: method.IsStartStep(),
                IsExitStep: method.IsEndStep(),
                IsAsync: method.IsAsynchronous(),
                IsReturnTypeCollection: method.ReturnTypeIsCollection(),
                MaxDegreeOfParallelism: method.GetMaxDegreeOfParallelism(),
                MaxBufferSize: method.GetMaxBufferSize()));
        }

        return blocks.ToImmutable();
    }

    private static string BuildHandlerBody(IMethodSymbol method)
    {
        var parameterName = method.Parameters.FirstOrDefault()?.Name ?? "x";

        if (string.Equals(method.ReturnType.Name, "Void", StringComparison.OrdinalIgnoreCase))
        {
            return $"{parameterName} => {{ }}";
        }

        return $"{parameterName} => default";
    }

    private static ImmutableArray<BlockNode> WireBlocks(ImmutableArray<BlockNode> blocks, SemanticModel semanticModel)
    {
        if (blocks.IsDefaultOrEmpty)
        {
            return ImmutableArray<BlockNode>.Empty;
        }

        var byName = blocks.ToDictionary(b => b.Method.Name, b => b, StringComparer.Ordinal);
        var updated = blocks.ToDictionary(b => b.Id, b => b);

        foreach (var block in blocks)
        {
            var nextIds = block.Method.GetNextStepAttrs()
                .Select(attr => ExtractNextStepName(attr, semanticModel))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => byName.TryGetValue(name!, out var next) ? next.Id : (int?)null)
                .OfType<int>()
                .Distinct()
                .OrderBy(id => id)
                .ToImmutableArray();

            updated[block.Id] = updated[block.Id] with { NextBlocks = nextIds };
        }

        return updated.Values.OrderBy(b => b.Id).ToImmutableArray();
    }

    private static string? ExtractNextStepName(AttributeData attribute, SemanticModel semanticModel)
    {
        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string name)
        {
            return name;
        }

        if (attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax)
        {
            var argument = syntax.ArgumentList?.Arguments.FirstOrDefault();
            var expression = argument?.Expression;
            if (expression is not null)
            {
                var constant = semanticModel.GetConstantValue(expression);
                if (constant.HasValue && constant.Value is string constantName)
                {
                    return constantName;
                }

                if (expression is LiteralExpressionSyntax literal)
                {
                    return literal.Token.ValueText;
                }
            }
        }

        return null;
    }

    private static ImmutableArray<Diagnostic> ValidateInputTypes(ActorNode actor, SyntaxAndSymbol input)
    {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();

        if (!actor.HasAnyInputTypes)
        {
            builder.Add(ActorSrcGen.Diagnostics.Diagnostics.CreateDiagnostic(ActorSrcGen.Diagnostics.Diagnostics.ASG0002, input.Syntax.GetLocation(), actor.Name));
        }

        if (actor.HasMultipleInputTypes && !actor.HasDisjointInputTypes)
        {
            var types = string.Join(", ", actor.InputTypeNames);
            builder.Add(ActorSrcGen.Diagnostics.Diagnostics.CreateDiagnostic(ActorSrcGen.Diagnostics.Diagnostics.ASG0001, input.Syntax.GetLocation(), actor.Name, types));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateIngestMethods(ImmutableArray<IngestMethod> ingesters)
    {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var ingest in ingesters)
        {
            var method = ingest.Method;
            var returnsTask = string.Equals(method.ReturnType.Name, "Task", StringComparison.Ordinal) || method.ReturnType.Name.Contains("AsyncEnumerable", StringComparison.Ordinal);
            if (!method.IsStatic || !returnsTask)
            {
                builder.Add(ActorSrcGen.Diagnostics.Diagnostics.CreateDiagnostic(ActorSrcGen.Diagnostics.Diagnostics.ASG0003, method.Locations.FirstOrDefault() ?? Location.None, method.Name));
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateStepMethods(ActorNode actor)
    {
        // Placeholder for future step-level validation; currently no additional diagnostics.
        return ImmutableArray<Diagnostic>.Empty;
    }

    private static IEnumerable<IMethodSymbol> GetStepMethods(INamedTypeSymbol typeSymbol)
        => typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ms => ms.GetBlockAttr() is not null && ms.Name != ".ctor");

    private static IEnumerable<IMethodSymbol> GetIngestMethods(INamedTypeSymbol typeSymbol)
        => typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ms => ms.GetIngestAttr() is not null && ms.Name != ".ctor");

    private static NodeType ResolveNodeType(IMethodSymbol method)
    {
        if (string.Equals(method.ReturnType.Name, "Void", StringComparison.Ordinal))
        {
            return NodeType.Action;
        }

        if (method.ReturnTypeIsCollection())
        {
            return NodeType.TransformMany;
        }

        return NodeType.Transform;
    }
}