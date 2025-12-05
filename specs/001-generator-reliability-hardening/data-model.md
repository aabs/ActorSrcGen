# Data Model: Hardening ActorSrcGen Domain Entities

**Feature**: Hardening ActorSrcGen Source Generator for Reliability and Testability  
**Date**: 2025-12-05  
**Purpose**: Define immutable domain model with records and ImmutableArray

## Overview

The ActorSrcGen domain model represents the abstract syntax tree of actor classes and their dataflow blocks. All entities MUST be immutable records using `ImmutableArray<T>` for collections (Constitution Principle III).

## Core Entities

### SyntaxAndSymbol

**Purpose**: Pairs Roslyn syntax node with semantic symbol for incremental pipeline

**Type**: Immutable record  
**Lifecycle**: Created during transform phase, passed to generation phase  
**Mutability**: Fully immutable

```csharp
/// <summary>
/// Represents a type declaration syntax paired with its semantic symbol.
/// Used in incremental generator pipeline to pass both syntax and semantic information.
/// </summary>
public sealed record SyntaxAndSymbol(
    TypeDeclarationSyntax Syntax,
    INamedTypeSymbol Symbol
);
```

**Properties**:
- `Syntax`: The class or record declaration syntax from source code
- `Symbol`: The resolved semantic symbol with type information

**Validation**: None required (created by Roslyn infrastructure)

**Relationships**:
- Referenced by: ActorNode, Generator pipeline
- References: None (terminal node)

---

### VisitorResult

**Purpose**: Encapsulates results of actor visitation including discovered actors and diagnostics

**Type**: Immutable record  
**Lifecycle**: Returned by ActorVisitor.VisitActor()  
**Mutability**: Fully immutable

```csharp
/// <summary>
/// Result of visiting an actor class, containing discovered actors and any diagnostics.
/// Enables pure functional visitor design for testability.
/// </summary>
public sealed record VisitorResult(
    ImmutableArray<ActorNode> Actors,
    ImmutableArray<Diagnostic> Diagnostics
);
```

**Properties**:
- `Actors`: Collection of discovered actor nodes (typically 1, but supports multiple)
- `Diagnostics`: Collected validation errors and warnings

**Validation**: 
- Actors and Diagnostics may both be empty (valid scenario)
- Diagnostics should be sorted by location for consistent reporting

**Relationships**:
- Returned by: ActorVisitor
- Consumed by: Generator.OnGenerate()
- Contains: ActorNode instances, Roslyn Diagnostic instances

---

### ActorNode

**Purpose**: Represents a complete actor class with all its dataflow steps and metadata

**Type**: Immutable record  
**Lifecycle**: Created by ActorVisitor, passed to T4 template  
**Mutability**: Fully immutable

```csharp
/// <summary>
/// Represents an actor class with its dataflow steps, input/output types, and relationships.
/// Contains computed properties for type analysis and validation.
/// </summary>
public sealed record ActorNode(
    ImmutableArray<BlockNode> StepNodes,
    ImmutableArray<IngestMethod> Ingesters,
    SyntaxAndSymbol Symbol
)
{
    // Computed Properties (derived from StepNodes)
    
    /// <summary>Entry nodes (marked with [FirstStep] or first in pipeline)</summary>
    public ImmutableArray<BlockNode> EntryNodes => 
        StepNodes.Where(s => s.IsEntryStep).ToImmutableArray();
    
    /// <summary>Exit nodes (marked with [LastStep] or last in pipeline)</summary>
    public ImmutableArray<BlockNode> ExitNodes => 
        StepNodes.Where(s => s.IsExitStep).ToImmutableArray();
    
    /// <summary>Output methods (exit nodes that return values)</summary>
    public ImmutableArray<IMethodSymbol> OutputMethods => 
        ExitNodes
            .Select(n => n.Method)
            .Where(m => !m.ReturnsVoid)
            .ToImmutableArray();
    
    /// <summary>Actor class name</summary>
    public string Name => Symbol.Symbol.Name;
    
    // Type Analysis Properties
    
    /// <summary>Input type names for all entry nodes</summary>
    public ImmutableArray<string> InputTypeNames =>
        EntryNodes.Select(n => n.InputTypeName).ToImmutableArray();
    
    /// <summary>Input type symbols for all entry nodes</summary>
    public ImmutableArray<ITypeSymbol> InputTypes =>
        EntryNodes
            .Select(n => n.InputType)
            .Where(t => t is not null)
            .ToImmutableArray()!;
    
    /// <summary>Output type symbols for all exit nodes (excluding void)</summary>
    public ImmutableArray<ITypeSymbol> OutputTypes =>
        ExitNodes
            .Select(n => n.OutputType)
            .Where(t => t is not null && !t.Name.Equals("void", StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray()!;
    
    /// <summary>Output type names with async unwrapping</summary>
    public ImmutableArray<string> OutputTypeNames =>
        ExitNodes
            .SelectMany(node => {
                var returnType = node.Method.ReturnType;
                // Unwrap Task<T> for async methods
                if (returnType.Name == "Task" && returnType is INamedTypeSymbol nts && nts.TypeArguments.Length > 0)
                    return new[] { nts.TypeArguments[0].RenderTypename() };
                return new[] { returnType.RenderTypename() };
            })
            .ToImmutableArray();
    
    // Validation Properties
    
    /// <summary>True if actor has exactly one distinct input type</summary>
    public bool HasSingleInputType => InputTypes.Distinct(SymbolEqualityComparer.Default).Count() == 1;
    
    /// <summary>True if actor has multiple distinct input types</summary>
    public bool HasMultipleInputTypes => InputTypes.Distinct(SymbolEqualityComparer.Default).Count() > 1;
    
    /// <summary>True if actor has at least one input type (required for valid actor)</summary>
    public bool HasAnyInputTypes => InputTypes.Any();
    
    /// <summary>True if actor has at least one output type</summary>
    public bool HasAnyOutputTypes => OutputTypes.Any();
    
    /// <summary>
    /// True if all input types are disjoint (required for multi-input actors).
    /// For routing, each input type must be unique.
    /// </summary>
    public bool HasDisjointInputTypes => 
        InputTypeNames.Distinct().Count() == InputTypeNames.Length;
    
    /// <summary>True if actor has exactly one output type</summary>
    public bool HasSingleOutputType => OutputTypes.Length == 1;
    
    /// <summary>True if actor has multiple output types</summary>
    public bool HasMultipleOutputTypes => OutputTypes.Length > 1;
    
    /// <summary>Semantic type symbol reference</summary>
    public INamedTypeSymbol TypeSymbol => Symbol.Symbol;
}
```

**Properties**:
- `StepNodes`: All dataflow blocks (steps) in the actor pipeline
- `Ingesters`: Methods that pull data from external sources (optional)
- `Symbol`: Reference to syntax and semantic symbol

**Computed Properties**: All derived from `StepNodes`, not stored

**Validation Rules**:
- MUST have at least one input type (HasAnyInputTypes)
- IF HasMultipleInputTypes THEN HasDisjointInputTypes (else ASG0001)
- StepNodes should have at least one EntryNode and one ExitNode

**Relationships**:
- Contained in: VisitorResult
- References: SyntaxAndSymbol
- Contains: BlockNode (StepNodes), IngestMethod (Ingesters)

---

### BlockNode

**Purpose**: Represents a single dataflow block (step) in the actor pipeline

**Type**: Immutable record  
**Lifecycle**: Created during actor visitation  
**Mutability**: Fully immutable

```csharp
/// <summary>
/// Represents a single dataflow block (TPL Dataflow block) in an actor pipeline.
/// Contains method reference, block type, handler body, and wiring information.
/// </summary>
public sealed record BlockNode(
    string HandlerBody,
    int Id,
    IMethodSymbol Method,
    NodeType NodeType,
    int NumNextSteps,
    ImmutableArray<int> NextBlocks,
    bool IsEntryStep,
    bool IsExitStep,
    bool IsAsync,
    bool IsReturnTypeCollection,
    int MaxDegreeOfParallelism = 4,
    int MaxBufferSize = 10
)
{
    /// <summary>Input type of the block (first parameter type)</summary>
    public ITypeSymbol? InputType => 
        Method.Parameters.FirstOrDefault()?.Type;
    
    /// <summary>Input type name (rendered for code generation)</summary>
    public string InputTypeName => 
        InputType?.RenderTypename() ?? "";
    
    /// <summary>Output type of the block (return type)</summary>
    public ITypeSymbol? OutputType => Method.ReturnType;
    
    /// <summary>Output type name (rendered for code generation)</summary>
    public string OutputTypeName => 
        OutputType?.RenderTypename() ?? "";
}
```

**Properties**:
- `HandlerBody`: Generated C# lambda code for block handler (e.g., `(x) => { try { Method(x); } catch {...} }`)
- `Id`: Unique identifier for block wiring (sequential, deterministic)
- `Method`: Roslyn symbol for the source method
- `NodeType`: Type of TPL Dataflow block (Action, Transform, TransformMany, Broadcast, etc.)
- `NumNextSteps`: Count of outgoing edges (used for broadcast block insertion)
- `NextBlocks`: IDs of blocks to link to (wiring graph)
- `IsEntryStep`: True if this is an entry point (accepts external input)
- `IsExitStep`: True if this is an exit point (produces final output)
- `IsAsync`: True if method is async or returns Task<T>
- `IsReturnTypeCollection`: True if return type is IEnumerable<T> or Task<IEnumerable<T>>
- `MaxDegreeOfParallelism`: Concurrency level for block (default: 4)
- `MaxBufferSize`: Buffer capacity for block (default: 10)

**Computed Properties**:
- `InputType`, `InputTypeName`: Derived from Method.Parameters
- `OutputType`, `OutputTypeName`: Derived from Method.ReturnType

**Validation Rules**:
- Id must be > 0 and unique within actor
- HandlerBody must be valid C# lambda expression
- NextBlocks IDs must reference valid blocks in the same actor
- Entry steps must have InputType
- Exit steps should have OutputType (unless void action)

**Relationships**:
- Contained in: ActorNode.StepNodes
- References: IMethodSymbol (Roslyn), other BlockNodes via NextBlocks (by ID)

**Node Type Enum**:
```csharp
/// <summary>
/// Types of TPL Dataflow blocks supported by ActorSrcGen.
/// Determines handler signature and behavior.
/// </summary>
public enum NodeType
{
    /// <summary>ActionBlock: input only, no output (void method)</summary>
    Action,
    
    /// <summary>TransformBlock: input → single output</summary>
    Transform,
    
    /// <summary>TransformManyBlock: input → multiple outputs (IEnumerable)</summary>
    TransformMany,
    
    /// <summary>BroadcastBlock: broadcasts value to multiple targets</summary>
    Broadcast,
    
    /// <summary>BufferBlock: queues messages</summary>
    Buffer,
    
    /// <summary>BatchBlock: collects N messages into batch</summary>
    Batch,
    
    /// <summary>JoinBlock: joins multiple inputs</summary>
    Join,
    
    /// <summary>BatchedJoinBlock: batched version of JoinBlock</summary>
    BatchedJoin,
    
    /// <summary>WriteOnceBlock: stores single value</summary>
    WriteOnce
}
```

---

### IngestMethod

**Purpose**: Represents a method that pulls data from external sources into the actor

**Type**: Immutable record  
**Lifecycle**: Created during actor visitation  
**Mutability**: Fully immutable

```csharp
/// <summary>
/// Represents an ingester method that pulls data from external sources.
/// Marked with [Ingest(priority)] attribute.
/// </summary>
public sealed record IngestMethod(IMethodSymbol Method)
{
    /// <summary>Input parameter types (if any)</summary>
    public ImmutableArray<ITypeSymbol> InputTypes => 
        Method.Parameters.Select(p => p.Type).ToImmutableArray();
    
    /// <summary>Return type (output from ingester)</summary>
    public ITypeSymbol OutputType => Method.ReturnType;
    
    /// <summary>
    /// Priority for ingester execution (lower = higher priority).
    /// Extracted from [Ingest(priority)] attribute.
    /// </summary>
    public int Priority
    {
        get
        {
            var attr = Method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "IngestAttribute");
            
            if (attr is null)
                return int.MaxValue; // Default: lowest priority
            
            return (int)(attr.ConstructorArguments.FirstOrDefault().Value ?? int.MaxValue);
        }
    }
}
```

**Properties**:
- `Method`: Roslyn symbol for the ingester method
- `InputTypes`: Parameter types (typically none or CancellationToken)
- `OutputType`: Type of data pulled into the actor
- `Priority`: Execution order (lower values run first)

**Validation Rules**:
- Method should be async (typically returns Task<T>)
- OutputType should match one of actor's input types
- Priority should be unique within actor (or explicitly handled)

**Relationships**:
- Contained in: ActorNode.Ingesters
- References: IMethodSymbol (Roslyn)

---

## Diagnostic Descriptors

**Purpose**: Centralized error and warning definitions

**Type**: Static readonly DiagnosticDescriptor  
**Location**: ActorSrcGen/Diagnostics/DiagnosticDescriptors.cs

```csharp
/// <summary>
/// Centralized diagnostic descriptor definitions for ActorSrcGen.
/// All descriptors are static readonly for immutability and thread-safety.
/// </summary>
internal static class Diagnostics
{
    private const string Category = "ActorSrcGen";
    
    /// <summary>
    /// ASG0001: Actor with multiple input types must have disjoint types.
    /// Severity: Error
    /// </summary>
    public static readonly DiagnosticDescriptor NonDisjointInputTypes = new(
        id: "ASG0001",
        title: "Actor with multiple input types must have disjoint types",
        messageFormat: "Actor '{0}' accepts inputs of type '{1}'. All types must be distinct.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When an actor has multiple entry points, each input type must be unique to allow proper routing to the correct input block."
    );
    
    /// <summary>
    /// ASG0002: Actor must have at least one input type.
    /// Severity: Error
    /// </summary>
    public static readonly DiagnosticDescriptor MissingInputTypes = new(
        id: "ASG0002",
        title: "Actor must have at least one input type",
        messageFormat: "Actor '{0}' does not have any input types defined. At least one method marked with [FirstStep] or [Step] is required.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An actor must have at least one entry point that accepts input to form a valid dataflow pipeline."
    );
    
    /// <summary>
    /// ASG0003: Error during source generation.
    /// Severity: Error
    /// </summary>
    public static readonly DiagnosticDescriptor GenerationError = new(
        id: "ASG0003",
        title: "Error generating source",
        messageFormat: "Error while generating source for '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An unexpected error occurred during source generation. This typically indicates an internal generator issue or invalid actor configuration."
    );
}
```

**Usage**:
```csharp
// Creating diagnostic with location
var diagnostic = Diagnostic.Create(
    Diagnostics.NonDisjointInputTypes,
    symbol.Syntax.GetLocation(),
    actorName,
    string.Join(", ", inputTypeNames)
);
context.ReportDiagnostic(diagnostic);
```

---

## Entity Relationships

```
Generator
    │
    ├─► SyntaxAndSymbol (pipeline input)
    │
    └─► ActorVisitor
            │
            └─► VisitorResult
                    │
                    ├─► ActorNode (1+)
                    │       │
                    │       ├─► SyntaxAndSymbol (reference)
                    │       ├─► BlockNode (many) ──► IMethodSymbol
                    │       └─► IngestMethod (0+) ──► IMethodSymbol
                    │
                    └─► Diagnostic (0+) ──► DiagnosticDescriptor
```

**Flow**:
1. Generator receives `SyntaxAndSymbol` from incremental pipeline
2. ActorVisitor analyzes symbol and creates `ActorNode` with `BlockNode` children
3. Visitor validates and collects `Diagnostic` instances
4. Visitor returns `VisitorResult` containing actors and diagnostics
5. Generator reports diagnostics and emits source for each actor

---

## Invariants & Constraints

### Global Invariants

1. **Immutability**: All entities are immutable after construction
2. **No null collections**: Use `ImmutableArray<T>.Empty`, never null
3. **Deterministic ordering**: Collections maintain insertion order
4. **No circular references**: Entity graph is acyclic (BlockNode.NextBlocks reference by ID, not object)

### ActorNode Invariants

1. `HasAnyInputTypes` must be true (validated with ASG0002)
2. IF `HasMultipleInputTypes` THEN `HasDisjointInputTypes` (validated with ASG0001)
3. `StepNodes` should contain at least one entry and one exit node
4. `Symbol.Symbol.Name` must be valid C# identifier

### BlockNode Invariants

1. `Id` > 0 and unique within parent ActorNode
2. `HandlerBody` must be valid C# lambda expression
3. `NextBlocks` IDs must reference valid blocks in same actor
4. IF `IsEntryStep` THEN `InputType` is not null
5. IF `NodeType == Action` THEN `OutputType.Name == "Void"`
6. IF `NodeType == Broadcast` THEN `NumNextSteps` > 1

### VisitorResult Invariants

1. `Diagnostics` contains errors ONLY if validation failed
2. `Actors` may be empty if severe errors prevent creation
3. IF `Actors.IsEmpty` THEN `Diagnostics.Any()` (must explain why no actors)

---

## Validation Strategy

### Visitor Validation (during construction)

Collect all validation errors in `ImmutableArray<Diagnostic>.Builder`:

1. **Input type validation**:
   - Check `HasAnyInputTypes` → ASG0002 if false
   - Check `HasDisjointInputTypes` if multiple inputs → ASG0001 if false

2. **Block graph validation**:
   - Check for orphaned blocks (no path from entry to exit)
   - Check for circular dependencies (graph cycles)
   - Validate NextBlocks IDs reference existing blocks

3. **Method signature validation**:
   - Entry methods have exactly one parameter
   - Exit methods return compatible types
   - Async methods properly annotated

### Generator Validation (during generation)

1. **Symbol validation**:
   - Symbol is class or record (not interface, struct)
   - Class is partial
   - Class has [Actor] attribute

2. **Template validation**:
   - ActorNode properties accessible
   - Type names render correctly
   - Generated code compiles (integration test)

---

## Migration Path (Current → Target)

### Phase 1: Add Records Alongside Classes

```csharp
// Keep existing classes temporarily
public class ActorNode { ... } // OLD

// Add new records
public record ActorNodeV2 { ... } // NEW
```

### Phase 2: Update Visitor to Return V2

```csharp
public VisitorResult VisitActor(...) {
    // Build using V2
    var node = new ActorNodeV2(...);
    return new VisitorResult(...);
}
```

### Phase 3: Update Template to Accept V2

```csharp
// Actor.tt template parameter
<#@ parameter name="ActorNode" type="ActorNodeV2" #>
```

### Phase 4: Remove Old Classes

```csharp
// Delete old ActorNode class
// Rename ActorNodeV2 → ActorNode
```

**Risk Mitigation**: Keep integration tests running throughout migration to catch breaks early.

---

## Summary

The refactored domain model prioritizes:

1. **Immutability**: Records with ImmutableArray for all collections
2. **Testability**: Pure functions, no side effects, computed properties
3. **Clarity**: Explicit validation properties (HasAnyInputTypes, HasDisjointInputTypes)
4. **Performance**: ImmutableArray is efficient for small collections (<100 items)
5. **Thread-Safety**: No shared mutable state, safe for parallel generation

All entities comply with Constitution Principle III: "Use record types for all data transfer objects and domain models."
