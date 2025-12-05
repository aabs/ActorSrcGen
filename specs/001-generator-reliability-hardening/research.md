# Research: Constitutional Violations & Hardening Requirements

**Feature**: Hardening ActorSrcGen Source Generator for Reliability and Testability  
**Date**: 2025-12-05  
**Purpose**: Identify constitutional violations and technical gaps requiring remediation

## Executive Summary

The ActorSrcGen codebase has **significant constitutional violations** across all six core principles. Critical issues include:

- **Zero test coverage** for critical generator logic (violates Principle II: 100% critical path requirement)
- **Mutable state violations** with instance fields and non-deterministic ordering (violates Principle III)
- **Inline diagnostic creation** instead of centralized descriptors (violates Principle IV)
- **Class-based domain models** instead of records (violates Principle III)
- **No cancellation checks** in generation pipelines (violates spec FR-009)
- **Thread-unsafe visitor** with mutable instance state (violates spec FR-014)

## Constitutional Violations Analysis

### Principle I: Test-First Development (NON-NEGOTIABLE) ❌ VIOLATED

**Current State**:
- Only 1 test exists: `Generates_no_crash_for_empty_actor` (smoke test only)
- Zero tests for ActorVisitor, ActorGenerator, diagnostic reporting, type rendering
- No TDD workflow evidence
- No acceptance test coverage

**Required State**:
- Tests MUST be written first (Red-Green-Refactor)
- All acceptance scenarios from spec MUST have tests
- Test names MUST clearly describe expected behavior

**Gap**: Complete absence of TDD practice and comprehensive test suite

---

### Principle II: Code Coverage & Reliability ❌ VIOLATED

**Current State**:
- **Generator.cs**: ~20% coverage (only smoke test)
- **ActorGenerator.cs**: 0% coverage
- **ActorVisitor.cs**: 0% coverage
- **RoslynExtensions.cs**: 0% coverage
- **TypeHelpers.cs**: 0% coverage
- **BlockGraph.cs**: 0% coverage

**Required State (Constitution)**:
- Overall project: 85% minimum
- Critical paths 100% coverage:
  - ✗ Generator.Initialize() and OnGenerate()
  - ✗ ActorGenerator public methods
  - ✗ ActorVisitor.VisitActor() and VisitMethod()
  - ✗ All validation logic
  - ✗ RoslynExtensions (attribute matching, symbol queries)
  - ✗ TypeHelpers.RenderTypename()
  - ✗ BlockNode creation methods
  - ✗ All Diagnostic creation

**Gap**: ~0-20% vs required 85% overall, 0% vs required 100% critical

---

### Principle III: Reliability Through Immutability & Pure Functions ❌ VIOLATED

**Current Violations**:

1. **Mutable Classes Instead of Records**:
   ```csharp
   // ❌ VIOLATION: Classes with mutable properties
   public class ActorNode {
       public List<BlockNode> StepNodes { get; set; } = [];
       public List<IngestMethod> Ingesters { get; set; } = [];
   }
   
   public class BlockNode {
       public string HandlerBody { get; set; }
       public List<int> NextBlocks { get; set; } = new();
   }
   
   public class SyntaxAndSymbol {
       public TypeDeclarationSyntax Syntax { get; }
       public INamedTypeSymbol Symbol { get; }
   }
   ```

2. **Mutable Collections in Public APIs**:
   ```csharp
   // ❌ VIOLATION: Mutable List<T> exposed
   public List<ActorNode> Actors => _actorStack.ToList();
   public List<BlockNode> EntryNodes => StepNodes.Where(s => s.IsEntryStep).ToList();
   ```

3. **Instance State in Visitor** (Generator.cs line 21):
   ```csharp
   // ❌ VIOLATION: Mutable instance field
   protected IncrementalGeneratorInitializationContext GenContext { get; set; }
   ```

4. **Mutable State in ActorVisitor**:
   ```csharp
   // ❌ VIOLATION: Mutable instance state
   public int BlockCounter { get; set; } = 0;
   private Stack<ActorNode> _actorStack = new();
   private Stack<BlockNode> _blockStack = new();
   ```

**Required State**:
- Use `record` types for all DTOs and domain models
- Use `ImmutableArray<T>`, `ImmutableList<T>`, `ImmutableDictionary<K,V>`
- Declare `readonly` fields unless mutation unavoidable
- Pure functions with no side effects

**Gap**: Pervasive mutable state throughout domain model and visitor

---

### Principle IV: Diagnostic Consistency & Error Handling ❌ VIOLATED

**Current Violations**:

1. **Inline Diagnostic Creation** (Generator.cs lines 85-91):
   ```csharp
   // ❌ VIOLATION: Inline descriptor creation
   var descriptor = new DiagnosticDescriptor(
       "ASG0002",
       "Error generating source",
       "Error while generating source for '{0}': {1}",
       "SourceGenerator",
       DiagnosticSeverity.Error,
       true);
   ```

2. **Inconsistent Diagnostic IDs**:
   - ASG0001 mentioned in spec but not in code
   - ASG0002 used ad-hoc without centralization

3. **No Validation Diagnostic Collection**:
   - ActorGenerator validates but doesn't collect all errors first
   - Fail-fast behavior (Constitution requires: "Collect ALL validation errors before reporting")

4. **Missing SourceText.From with Encoding**:
   ```csharp
   // ❌ VIOLATION: No UTF-8 encoding specified
   context.AddSource($"{actor.Name}.generated.cs", source);
   ```

**Required State**:
- Centralized static readonly DiagnosticDescriptor instances
- Collect all validation errors before reporting
- Use SourceText.From(text, Encoding.UTF8)
- Include symbol locations or fallback to Location.None

**Gap**: No centralized diagnostics, inconsistent error handling

---

### Principle V: Idiomatic C# & Low Cyclomatic Complexity ⚠️ PARTIAL VIOLATION

**Current Issues**:

1. **Moderate Complexity in VisitActor** (ActorVisitor.cs lines 53-103):
   - CC estimate: ~8-10 (nested ifs, foreachs, conditionals)
   - Could be decomposed into smaller methods

2. **Moderate Complexity in VisitMethod** (ActorVisitor.cs lines 105-156):
   - CC estimate: ~6-7 (nested conditionals)
   - Acceptable but could improve

3. **Good Patterns Observed**:
   - LINQ queries for filtering
   - Pattern matching used in some places
   - Expression-bodied members in BlockGraph.cs

**Required State**:
- Target CC ≤ 5 (max 8 with justification)
- Use nullable reference types
- Modern C# idioms

**Gap**: Some methods exceed target complexity, needs refactoring

---

### Principle VI: Testability & Async Code Discipline ❌ VIOLATED

**Current Violations**:

1. **Untestable Visitor Design**:
   - ActorVisitor uses mutable state (stacks, counters)
   - VisitActor is void with side effects
   - No way to test in isolation without full Roslyn pipeline

2. **No Cancellation Support**:
   ```csharp
   // ❌ VIOLATION: No cancellation checks
   void Generate(SourceProductionContext spc, ...) {
       foreach (SyntaxAndSymbol item in items) {
           OnGenerate(spc, compilation, item);
       }
   }
   ```

3. **Async Pattern Issues**:
   - N/A for generator (by design), but no guidance on patterns

**Required State**:
- Dependencies injected
- Visitor returns VisitorResult (actors + diagnostics)
- Cancellation checks in loops (FR-009)
- Pure functions testable without Roslyn

**Gap**: Visitor is stateful and side-effectful, no cancellation

---

## Specification Requirements Analysis

### Non-Determinism Issues

**Problem**: Generator output order not guaranteed

1. **No Sorting in Pipeline**:
   ```csharp
   // ❌ VIOLATION: FR-007, FR-008 - No sorting
   foreach (SyntaxAndSymbol item in items) {
       OnGenerate(spc, compilation, item);
   }
   ```

2. **Dictionary Iteration** (ActorVisitor lines 79-103):
   - Dictionary iteration order undefined
   - DependencyGraph iteration non-deterministic

**Required** (FR-007, FR-008):
- Sort symbols by fully-qualified name
- Sort actors within symbol by name

---

### Thread-Safety Issues

**Problem**: Shared mutable state

1. **Instance Field in Generator**:
   ```csharp
   // ❌ VIOLATION: FR-014 - Mutable instance field
   protected IncrementalGeneratorInitializationContext GenContext { get; set; }
   ```

2. **Visitor Instance State**:
   ```csharp
   // ❌ VIOLATION: FR-014 - Shared mutable state
   public int BlockCounter { get; set; } = 0;
   private Stack<ActorNode> _actorStack = new();
   ```

**Required** (FR-014):
- No instance fields capturing context
- No mutable static state

---

### Missing SourceText Encoding

**Problem**: No UTF-8 encoding specified

```csharp
// ❌ VIOLATION: FR-006
context.AddSource($"{actor.Name}.generated.cs", source);

// ✅ REQUIRED:
context.AddSource($"{actor.Name}.generated.cs", 
    SourceText.From(source, Encoding.UTF8));
```

---

### Test Coverage Gaps

**Missing Tests** (FR-017 through FR-020):

1. Empty actor (diagnostic expected) - ✗
2. Multiple identical input types (ASG0001) - ✗
3. Single input, single output - ✗
4. Async last step - ✗
5. Deterministic emission - ✗
6. Cancellation honored - ✗
7. Exception handling - ✗
8. Attribute false positives - ✗
9. Diagnostic locations - ✗
10. Unicode and line endings - ✗

**Current Tests**: 1 smoke test only (10% of required acceptance tests)

---

## Remediation Plan

### Decision 1: Convert Domain Models to Records

**Rationale**: Constitution Principle III mandates records for DTOs and domain models

**Implementation**:
```csharp
// ActorNode: class → record with ImmutableArray
public record ActorNode(
    ImmutableArray<BlockNode> StepNodes,
    ImmutableArray<IngestMethod> Ingesters,
    SyntaxAndSymbol Symbol
) {
    // Computed properties remain
    public ImmutableArray<BlockNode> EntryNodes => 
        StepNodes.Where(s => s.IsEntryStep).ToImmutableArray();
}

// BlockNode: class → record
public record BlockNode(
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
) {
    // Computed properties
    public ITypeSymbol? InputType => Method.Parameters.FirstOrDefault()?.Type;
    public string InputTypeName => InputType?.RenderTypename() ?? "";
}

// SyntaxAndSymbol: class → record (already immutable, just convert)
public record SyntaxAndSymbol(
    TypeDeclarationSyntax Syntax,
    INamedTypeSymbol Symbol
);

// IngestMethod: class → record
public record IngestMethod(IMethodSymbol Method) {
    public IEnumerable<ITypeSymbol> InputTypes => Method.Parameters.Select(s => s.Type);
    public ITypeSymbol OutputType => Method.ReturnType;
    public int Priority => (int)Method.GetAttributes()
        .First(a => a.AttributeClass.Name == "IngestAttribute")
        .ConstructorArguments.First().Value;
}
```

**Alternatives Considered**:
- Keep classes: Rejected (violates Constitution)
- Use init-only properties: Rejected (records preferred for immutability clarity)

---

### Decision 2: Refactor Visitor to Return VisitorResult

**Rationale**: FR-015 requires visitor return result object; enables testability

**Implementation**:
```csharp
public record VisitorResult(
    ImmutableArray<ActorNode> Actors,
    ImmutableArray<Diagnostic> Diagnostics
);

public class ActorVisitor {
    // Remove mutable state:
    // - Remove BlockCounter field
    // - Remove _actorStack field
    // - Remove _blockStack field
    
    public VisitorResult VisitActor(SyntaxAndSymbol symbol) {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var blocks = ImmutableArray.CreateBuilder<BlockNode>();
        
        // Pass blockCounter as parameter through recursive calls
        int blockCounter = 0;
        
        var methods = GetStepMethods(symbol.Symbol);
        foreach (var mi in methods) {
            var (block, newCounter) = VisitMethod(mi, blockCounter);
            blocks.Add(block);
            blockCounter = newCounter;
        }
        
        var actor = new ActorNode(
            StepNodes: blocks.ToImmutable(),
            Ingesters: GetIngestMethods(symbol.Symbol)
                .Select(m => new IngestMethod(m)).ToImmutableArray(),
            Symbol: symbol
        );
        
        // Validate and collect diagnostics
        diagnostics.AddRange(ValidateActor(actor, symbol));
        
        return new VisitorResult(
            ImmutableArray.Create(actor),
            diagnostics.ToImmutable()
        );
    }
    
    private (BlockNode block, int newCounter) VisitMethod(
        IMethodSymbol method, 
        int blockCounter
    ) {
        // Pure function - no side effects
        // Returns new block and updated counter
    }
    
    private ImmutableArray<Diagnostic> ValidateActor(
        ActorNode actor, 
        SyntaxAndSymbol symbol
    ) {
        // Collect ALL validation errors
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        
        if (!actor.HasAnyInputTypes) {
            diagnostics.Add(CreateDiagnostic(
                Diagnostics.MissingInputTypes,
                symbol.Syntax.GetLocation(),
                actor.Name
            ));
        }
        
        if (actor.HasMultipleInputTypes && !actor.HasDisjointInputTypes) {
            diagnostics.Add(CreateDiagnostic(
                Diagnostics.NonDisjointInputTypes,
                symbol.Syntax.GetLocation(),
                actor.Name,
                string.Join(", ", actor.InputTypeNames)
            ));
        }
        
        return diagnostics.ToImmutable();
    }
}
```

**Alternatives Considered**:
- Keep void methods: Rejected (untestable, violates FR-015)
- Use out parameters: Rejected (Constitution prefers return values)

---

### Decision 3: Centralize DiagnosticDescriptors

**Rationale**: Constitution Principle IV mandates centralized diagnostics; FR-012 requires static readonly instances

**Implementation**:
```csharp
// New file: ActorSrcGen/Diagnostics/DiagnosticDescriptors.cs
namespace ActorSrcGen.Diagnostics;

internal static class Diagnostics {
    private const string Category = "ActorSrcGen";
    
    public static readonly DiagnosticDescriptor NonDisjointInputTypes = new(
        id: "ASG0001",
        title: "Actor with multiple input types must have disjoint types",
        messageFormat: "Actor '{0}' accepts inputs of type '{1}'. All types must be distinct.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When an actor has multiple entry points, each input type must be unique to allow proper routing."
    );
    
    public static readonly DiagnosticDescriptor MissingInputTypes = new(
        id: "ASG0002",
        title: "Actor must have at least one input type",
        messageFormat: "Actor '{0}' does not have any input types defined. At least one entry method is required.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    
    public static readonly DiagnosticDescriptor GenerationError = new(
        id: "ASG0003",
        title: "Error generating source",
        messageFormat: "Error while generating source for '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

// Usage in Generator.cs:
private void OnGenerate(...) {
    try {
        // generation logic
    }
    catch (OperationCanceledException) {
        // FR-010: Swallow cancellation
        return;
    }
    catch (Exception e) {
        var diagnostic = Diagnostic.Create(
            Diagnostics.GenerationError,
            input.Syntax.GetLocation(),
            input.Symbol.Name,
            e.ToString()
        );
        context.ReportDiagnostic(diagnostic);
    }
}
```

**Alternatives Considered**:
- Keep inline creation: Rejected (violates Constitution and FR-012)
- Use constants: Rejected (requires static readonly DiagnosticDescriptor)

---

### Decision 4: Add Deterministic Sorting

**Rationale**: FR-007, FR-008 require sorting for byte-for-byte determinism

**Implementation**:
```csharp
// Generator.cs Generate method:
void Generate(
    SourceProductionContext spc,
    (Compilation compilation, ImmutableArray<SyntaxAndSymbol> items) source
) {
    var (compilation, items) = source;
    
    // FR-007: Sort symbols by fully-qualified name
    var sortedItems = items
        .OrderBy(item => item.Symbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat))
        .ToImmutableArray();
    
    foreach (SyntaxAndSymbol item in sortedItems) {
        // FR-009: Cancellation check
        spc.CancellationToken.ThrowIfCancellationRequested();
        
        OnGenerate(spc, compilation, item);
    }
}

// OnGenerate method:
private void OnGenerate(...) {
    try {
        var visitorResult = new ActorVisitor().VisitActor(input);
        
        // Report collected diagnostics
        foreach (var diag in visitorResult.Diagnostics) {
            context.ReportDiagnostic(diag);
        }
        
        // FR-008: Sort actors by name
        var sortedActors = visitorResult.Actors
            .OrderBy(a => a.Name)
            .ToImmutableArray();
        
        foreach (var actor in sortedActors) {
            var source = new Actor(actor).TransformText();
            
            // FR-006: Use SourceText.From with UTF-8
            context.AddSource(
                $"{actor.Name}.generated.cs",
                SourceText.From(source, Encoding.UTF8)
            );
        }
    }
    catch (OperationCanceledException) {
        // FR-010: Swallow and return
        return;
    }
    catch (Exception e) {
        // FR-011: Report as diagnostic
        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.GenerationError,
            input.Syntax.GetLocation(),
            input.Symbol.Name,
            e.ToString()
        ));
    }
}
```

**Alternatives Considered**:
- Rely on natural ordering: Rejected (non-deterministic)
- Sort only actors: Rejected (FR-007 requires symbol sorting too)

---

### Decision 5: Add Comprehensive Test Suite

**Rationale**: Constitution Principle I (TDD), Principle II (85%+ coverage), FR-017 through FR-020

**Implementation Structure**:
```
tests/ActorSrcGen.Tests/
├── GeneratorTests.cs              # Initialize, OnGenerate, pipeline
├── ActorVisitorTests.cs           # VisitActor, VisitMethod, validation
├── DiagnosticTests.cs             # ASG0001, ASG0002, locations
├── DeterminismTests.cs            # Sorting, encoding, Unicode
├── CancellationTests.cs           # Cancellation handling
├── ThreadSafetyTests.cs           # Parallel execution
├── SnapshotTests/                 # Generated code snapshots
│   ├── SingleInputOutput.cs
│   ├── MultipleInputs.cs
│   └── AsyncSteps.cs
└── TestHelpers/
    ├── CompilationHelper.cs       # Reusable compilation setup
    └── SnapshotHelper.cs          # Snapshot comparison
```

**Test Pattern Example**:
```csharp
// ActorVisitorTests.cs
public class ActorVisitorTests {
    [Fact]
    public void VisitActor_WithNoInputMethods_ReturnsASG0002Diagnostic() {
        // Arrange
        var symbol = CreateSymbol("""
            [Actor]
            public partial class EmptyActor { }
        """);
        var visitor = new ActorVisitor();
        
        // Act
        var result = visitor.VisitActor(symbol);
        
        // Assert
        Assert.Empty(result.Actors);
        Assert.Single(result.Diagnostics);
        Assert.Equal("ASG0002", result.Diagnostics[0].Id);
        Assert.Contains("EmptyActor", result.Diagnostics[0].GetMessage());
    }
    
    [Fact]
    public void VisitActor_WithNonDisjointInputs_ReturnsASG0001Diagnostic() {
        // Arrange: Multiple methods accepting same type
        var symbol = CreateSymbol("""
            [Actor]
            public partial class MyActor {
                [FirstStep]
                public void Method1(string input) { }
                
                [FirstStep]
                public void Method2(string input) { }
            }
        """);
        
        // Act
        var result = visitor.VisitActor(symbol);
        
        // Assert
        Assert.Single(result.Diagnostics);
        Assert.Equal("ASG0001", result.Diagnostics[0].Id);
    }
}
```

**Alternatives Considered**:
- Manual testing: Rejected (Constitution requires automated tests)
- Integration tests only: Rejected (need unit + integration)

---

## Technology Decisions

### Test Framework: xUnit

**Decision**: Use xUnit as specified in FR-017

**Rationale**:
- Spec explicitly requires xUnit
- Standard for .NET projects
- Good Roslyn test support
- Parallel execution support for performance

---

### Snapshot Testing: Verify

**Decision**: Use Verify library for snapshot tests

**Rationale**:
- Industry standard for .NET snapshot testing
- Excellent diff visualization
- xUnit integration
- Git-friendly output format

**Usage**:
```csharp
[Fact]
public Task GeneratesCorrectCode_ForSingleInputOutput() {
    var source = """
        [Actor]
        public partial class MyActor {
            [FirstStep]
            public int Process(string input) => input.Length;
        }
    """;
    
    var result = GenerateCode(source);
    
    return Verify(result)
        .UseFileName("SingleInputOutput");
}
```

---

### Coverage Tool: Coverlet

**Decision**: Use Coverlet with ReportGenerator for coverage

**Rationale**:
- Cross-platform
- MSBuild and CLI integration
- Generates multiple formats (Cobertura, HTML)
- CI/CD friendly

**Configuration** (Directory.Build.props or test project):
```xml
<ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="ReportGenerator" Version="5.2.0" />
</ItemGroup>
```

---

## Implementation Sequence

Based on Constitution and spec requirements, implementation MUST follow this order:

### Phase 1: Foundation (Testability Refactoring)

1. **Convert domain models to records** (enables pure functions)
   - ActorNode, BlockNode, SyntaxAndSymbol, IngestMethod
   - Use ImmutableArray for collections

2. **Centralize DiagnosticDescriptors** (enables consistent error reporting)
   - Create Diagnostics static class
   - Define ASG0001, ASG0002, ASG0003

3. **Refactor ActorVisitor** (enables unit testing)
   - Remove mutable instance state
   - Return VisitorResult
   - Make VisitMethod pure function

### Phase 2: Reliability Hardening

4. **Add deterministic sorting** (FR-007, FR-008)
   - Sort symbols by FQN
   - Sort actors by name

5. **Add SourceText.From with UTF-8** (FR-006)
   - Update AddSource calls

6. **Add cancellation support** (FR-009, FR-010)
   - ThrowIfCancellationRequested in loops
   - Catch OperationCanceledException

7. **Remove mutable instance fields** (FR-014)
   - Remove GenContext property
   - Pass through method parameters if needed

### Phase 3: Test Suite (TDD)

8. **Write failing tests FIRST** for each unit
   - ActorVisitor unit tests
   - Generator unit tests
   - Diagnostic tests

9. **Implement fixes to make tests pass**

10. **Add integration tests**
    - End-to-end generation tests
    - Determinism tests
    - Thread-safety tests

11. **Add snapshot tests**
    - Expected code output validation

### Phase 4: Coverage & CI/CD

12. **Measure coverage**
    - Run Coverlet
    - Generate reports
    - Identify gaps

13. **Add coverage gate to CI**
    - Fail if < 85% overall
    - Fail if critical paths < 100%

---

## Testing Strategy Details

### Unit Test Categories

**ActorVisitor Tests** (100% coverage required):
- Empty actor → ASG0002 diagnostic
- Non-disjoint inputs → ASG0001 diagnostic
- Single input/output → correct ActorNode
- Multiple disjoint inputs → separate entry nodes
- Async methods → correct node types
- Block wiring → correct NextBlocks
- Pure function → same input = same output

**Generator Tests** (100% coverage required):
- Initialize sets up pipeline correctly
- OnGenerate with valid actor → source added
- OnGenerate with exception → ASG0003 diagnostic
- Cancellation → swallowed, no diagnostics
- Sorting → deterministic order
- UTF-8 encoding → stable hashes

**Diagnostic Tests** (100% coverage required):
- ASG0001 has correct ID, message, location
- ASG0002 has correct ID, message, location
- ASG0003 includes exception details
- Diagnostic locations point to symbols

### Integration Test Categories

**Determinism Tests**:
- Run twice → identical output (byte-for-byte)
- Shuffle input order → same generated files
- Unicode identifiers → stable encoding

**Thread-Safety Tests**:
- Parallel execution → no race conditions
- Concurrent diagnostic reporting → all present
- No shared state corruption

**Cancellation Tests**:
- Cancel during generation → clean exit
- Cancel between actors → no partial output
- No spurious diagnostics on cancellation

### Snapshot Tests

**Generated Code Validation**:
- Single input, single output
- Multiple disjoint inputs
- Async last step
- Complex dataflow pipeline
- Broadcast blocks

Each snapshot stored in `Snapshots/` directory with verification on code changes.

---

## Performance Considerations

### Sorting Impact

**Concern**: Sorting adds O(n log n) overhead

**Analysis**:
- Typical project: < 10 actors
- Sort overhead: < 1ms
- Well within < 50ms per actor constraint (Assumption in spec)

**Decision**: Acceptable tradeoff for determinism

### ImmutableArray vs List

**Concern**: ImmutableArray may impact performance

**Analysis**:
- Most collections small (< 20 items)
- ImmutableArray efficient for small collections
- Builder pattern for construction

**Decision**: Acceptable; reliability > micro-optimization

---

## Risks & Mitigation

### Risk 1: Breaking Template Compatibility

**Risk**: Converting ActorNode to record may break T4 template

**Likelihood**: Medium  
**Impact**: High (blocks generation)

**Mitigation**:
1. Analyze template dependencies first
2. Keep computed properties compatible
3. Add integration test for template rendering
4. Maintain property names and types

### Risk 2: Test Suite Slowdown

**Risk**: 85% coverage may slow down test runs

**Likelihood**: Low  
**Impact**: Medium (dev experience)

**Mitigation**:
1. Target < 30 seconds (spec SC-005)
2. Parallelize tests (xUnit default)
3. Use fast compilation helpers
4. Avoid expensive I/O in unit tests

### Risk 3: Regression from Refactoring

**Risk**: Refactoring visitor may break existing functionality

**Likelihood**: Medium  
**Impact**: High

**Mitigation**:
1. Write characterization tests FIRST
2. Refactor in small steps
3. Run tests after each change
4. Keep existing smoke test passing

---

## Open Questions

### Q1: Template Rendering Determinism

**Question**: Are T4 templates already deterministic?

**Investigation Needed**:
- Analyze Actor.tt for non-deterministic operations
- Check if template uses Dictionary iteration
- Verify StringBuilder determinism

**Decision Point**: Phase 1 research (before implementation)

### Q2: Line Ending Normalization

**Question**: Should we normalize line endings in generated code?

**Context**: FR-016 requires LF normalization

**Options**:
1. Normalize in template (affects all output)
2. Normalize in Generator.OnGenerate (centralized)
3. Let SourceText.From handle it (may not normalize)

**Recommendation**: Normalize in Generator.OnGenerate after template rendering

---

## Summary

### Critical Path to Compliance

1. **Immediate** (Blocking):
   - Convert models to records (testability foundation)
   - Refactor visitor to return results (enables unit testing)
   - Centralize diagnostics (error handling foundation)

2. **High Priority** (Week 1):
   - Write comprehensive test suite (TDD principle)
   - Add deterministic sorting (FR-007, FR-008)
   - Add cancellation support (FR-009, FR-010)

3. **Medium Priority** (Week 2):
   - Achieve 85%+ coverage
   - Add snapshot tests
   - CI/CD coverage gates

4. **Final Validation**:
   - Run acceptance tests (all 10 scenarios)
   - Verify 100+ runs without flakiness (SC-010)
   - Measure performance (< 1s for 10+ actors)

### Constitutional Compliance Checklist

After remediation, verify:

- [ ] Principle I: TDD workflow established, tests written first
- [ ] Principle II: 85% overall coverage, 100% critical paths
- [ ] Principle III: Records used, ImmutableArray everywhere, pure functions
- [ ] Principle IV: Centralized diagnostics, collect all errors
- [ ] Principle V: CC ≤ 5-8, modern C# idioms
- [ ] Principle VI: Visitor testable, cancellation supported

### Success Metrics

- Overall coverage: 0-20% → 85%+
- Critical path coverage: 0% → 100%
- Test count: 1 → 50+ (covering all acceptance scenarios)
- Constitutional violations: 6 major → 0
- Determinism: Not guaranteed → Guaranteed (byte-for-byte)
- Thread-safety: Unsafe → Safe (no shared state)
