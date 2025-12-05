using System.Collections.Immutable;
using ActorSrcGen.Helpers;
using Microsoft.CodeAnalysis;
using System;

namespace ActorSrcGen.Model;

public enum NodeType
{
    Action,
    Batch,
    BatchedJoin,
    Buffer,
    Transform,
    TransformMany,
    Broadcast,
    Join,
    WriteOnce
}

public sealed record ActorNode
{
    public ActorNode(ImmutableArray<BlockNode> stepNodes, ImmutableArray<IngestMethod> ingesters, SyntaxAndSymbol symbol)
    {
        StepNodes = Normalize(stepNodes);
        Ingesters = Normalize(ingesters);
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
    }

    public ImmutableArray<BlockNode> StepNodes { get; init; }
    public ImmutableArray<IngestMethod> Ingesters { get; init; }
    public SyntaxAndSymbol Symbol { get; init; }

    public ImmutableArray<BlockNode> EntryNodes => StepNodes.Where(s => s.IsEntryStep).ToImmutableArray();
    public ImmutableArray<BlockNode> ExitNodes => StepNodes.Where(s => s.IsExitStep).ToImmutableArray();
    public INamedTypeSymbol TypeSymbol => Symbol.Symbol;

    public bool HasSingleInputType => InputTypes.Length == 1;
    public bool HasMultipleInputTypes => InputTypes.Length > 1;
    public bool HasAnyInputTypes => InputTypes.Any();
    public bool HasAnyOutputTypes => OutputTypes.Any();
    public bool HasDisjointInputTypes => InputTypeNames.Distinct(StringComparer.Ordinal).Count() == InputTypeNames.Length;

    public bool HasSingleOutputType => OutputTypes.Length == 1;
    public bool HasMultipleOutputTypes => OutputTypes.Length > 1;
    public ImmutableArray<IMethodSymbol> OutputMethods => ExitNodes.Select(n => n.Method).Where(s => !s.ReturnsVoid).ToImmutableArray();
    public string Name => TypeSymbol.Name;
    public ImmutableArray<string> InputTypeNames => EntryNodes.Select(n => n.InputTypeName).ToImmutableArray();
    public ImmutableArray<ITypeSymbol> InputTypes => EntryNodes.Select(n => n.InputType).OfType<ITypeSymbol>().ToImmutableArray();
    public ImmutableArray<ITypeSymbol> OutputTypes => ExitNodes.Select(n => n.OutputType).OfType<ITypeSymbol>().Where(t => !string.Equals(t.Name, "void", StringComparison.OrdinalIgnoreCase)).ToImmutableArray();

    public ImmutableArray<string> OutputTypeNames => ExitNodes
        .SelectMany(fm =>
        {
            var returnType = fm.Method.ReturnType;
            if (string.Equals(returnType.Name, "Task", StringComparison.Ordinal) && returnType is INamedTypeSymbol nts)
            {
                if (nts.TypeArguments.Length > 0)
                {
                    return new[] { nts.TypeArguments[0].RenderTypename() };
                }
                return new[] { returnType.RenderTypename() };
            }

            return new[] { fm.Method.ReturnType.RenderTypename() };
        })
        .ToImmutableArray();

    private static ImmutableArray<T> Normalize<T>(ImmutableArray<T> value)
        => value.IsDefault ? ImmutableArray<T>.Empty : value;
}

public sealed record BlockNode
{
    public BlockNode(
        string HandlerBody,
        int Id,
        IMethodSymbol Method,
        NodeType NodeType,
        ImmutableArray<int> NextBlocks,
        bool IsEntryStep,
        bool IsExitStep,
        bool IsAsync,
        bool IsReturnTypeCollection,
        int MaxDegreeOfParallelism = 4,
        int MaxBufferSize = 10)
    {
        this.HandlerBody = HandlerBody ?? string.Empty;
        this.Id = Id;
        this.Method = Method ?? throw new ArgumentNullException(nameof(Method));
        this.NodeType = NodeType;
        this.NextBlocks = Normalize(NextBlocks);
        this.IsEntryStep = IsEntryStep;
        this.IsExitStep = IsExitStep;
        this.IsAsync = IsAsync;
        this.IsReturnTypeCollection = IsReturnTypeCollection;
        this.MaxDegreeOfParallelism = MaxDegreeOfParallelism;
        this.MaxBufferSize = MaxBufferSize;
    }

    public string HandlerBody { get; init; }
    public int Id { get; init; }
    public IMethodSymbol Method { get; init; }
    public NodeType NodeType { get; init; }
    public ImmutableArray<int> NextBlocks { get; init; }
    public bool IsEntryStep { get; init; }
    public bool IsExitStep { get; init; }
    public bool IsAsync { get; init; }
    public bool IsReturnTypeCollection { get; init; }
    public int MaxDegreeOfParallelism { get; init; }
    public int MaxBufferSize { get; init; }

    public ITypeSymbol? InputType => Method.Parameters.FirstOrDefault()?.Type;
    public string InputTypeName => InputType?.RenderTypename() ?? string.Empty;
    public ITypeSymbol? OutputType => Method.ReturnType;
    public string OutputTypeName => OutputType?.RenderTypename() ?? string.Empty;

    private static ImmutableArray<int> Normalize(ImmutableArray<int> value)
        => value.IsDefault ? ImmutableArray<int>.Empty : value;
}

public sealed record IngestMethod
{
    public IngestMethod(IMethodSymbol method)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
    }

    public IMethodSymbol Method { get; init; }
    public ImmutableArray<ITypeSymbol> InputTypes => Method.Parameters.Select(s => s.Type).ToImmutableArray();
    public ITypeSymbol OutputType => Method.ReturnType;
    public int Priority
    {
        get
        {
            var attr = Method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "IngestAttribute");
            return (int)(attr?.ConstructorArguments.FirstOrDefault().Value ?? int.MaxValue);
        }
    }
}