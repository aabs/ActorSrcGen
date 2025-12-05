using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ActorSrcGen.Model;

public sealed record VisitorResult(
    ImmutableArray<ActorNode> Actors,
    ImmutableArray<Diagnostic> Diagnostics
);
