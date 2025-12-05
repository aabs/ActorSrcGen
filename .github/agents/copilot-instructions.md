# ActorSrcGen Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-05

## Active Technologies

- **C# 12.0** (.NET 8 for tests, .NET Standard 2.0 for generator)
- **Roslyn SDK 4.6.0** (Microsoft.CodeAnalysis.CSharp)
- **xUnit** test framework with **Verify** snapshot testing
- **Coverlet** for code coverage with **ReportGenerator**
- **ImmutableCollections** (System.Collections.Immutable)
- **TPL Dataflow** (Gridsum.DataflowEx 2.0.0)

## Project Structure

```text
ActorSrcGen/                          # Source generator project (.NET Standard 2.0)
├── Generators/                       # IIncrementalGenerator implementation
├── Model/                            # Domain models (⚠️ refactoring to records)
├── Helpers/                          # Roslyn extensions
├── Diagnostics/                      # ✨ NEW: Centralized DiagnosticDescriptors
└── Templates/                        # T4 templates

ActorSrcGen.Abstractions/             # Public API attributes
tests/ActorSrcGen.Tests/              # Test suite (✨ expanding for 85% coverage)
├── GeneratorTests.cs                 # ✨ NEW: Pipeline tests
├── ActorVisitorTests.cs              # ✨ NEW: Visitor logic
├── DiagnosticTests.cs                # ✨ NEW: Diagnostic reporting
├── DeterminismTests.cs               # ✨ NEW: Byte-for-byte stability
├── CancellationTests.cs              # ✨ NEW: Cancellation handling
├── ThreadSafetyTests.cs              # ✨ NEW: Parallel execution
└── SnapshotTests/                    # ✨ NEW: Generated code snapshots
```

## Commands

```powershell
# Build
dotnet build

# Run tests
dotnet test

# Coverage (target: 85% overall, 100% critical paths)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
reportgenerator -reports:tests/**/coverage.opencover.xml -targetdir:coverage-report

# Run specific test categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Critical"

# Verify coverage thresholds (Constitution Principle II)
dotnet test /p:Threshold=85 /p:ThresholdType=line
```

## Code Style & Constitutional Principles

### TDD (Principle I - NON-NEGOTIABLE)
- Write tests FIRST (Red-Green-Refactor)
- No implementation without failing test
- Commit test + implementation together

### Coverage (Principle II)
- **Overall minimum: 85%**
- **Critical paths: 100%** (Generator.cs, ActorVisitor.cs, ActorGenerator.cs)
- Use Coverlet + ReportGenerator for validation

### Immutability (Principle III)
```csharp
// ❌ OLD: Mutable class
public class ActorNode {
    public List<int> NextBlocks { get; set; } = new();
}

// ✅ NEW: Immutable record
public sealed record ActorNode(
    string Name,
    ImmutableArray<int> NextBlocks,
    ...
);
```

### Diagnostics (Principle IV)
```csharp
// ❌ OLD: Inline creation
var descriptor = new DiagnosticDescriptor("ASG0002", ...);

// ✅ NEW: Centralized
using static ActorSrcGen.Diagnostics.Diagnostics;
var diagnostic = Diagnostic.Create(MissingInputTypes, location, actorName);
```

### Complexity (Principle V)
- **Target: CC ≤ 5**
- **Maximum: CC ≤ 8** (must justify exceptions)
- Extract helper methods to reduce complexity

### Testability (Principle VI)
```csharp
// ❌ OLD: Void with side effects
public void VisitActor(INamedTypeSymbol symbol) {
    _actorStack.Add(...); // Mutates state
}

// ✅ NEW: Pure function returning result
public VisitorResult VisitActor(SyntaxAndSymbol symbol) {
    return new VisitorResult(actors, diagnostics);
}
```

## Recent Changes

### 001-generator-reliability-hardening (Active)
**Goal**: Harden generator for reliability and testability

**Key Refactorings**:
1. Convert domain models to records (ActorNode, BlockNode, SyntaxAndSymbol)
2. Refactor ActorVisitor to return VisitorResult (pure function)
3. Centralize DiagnosticDescriptors in static Diagnostics class
4. Add deterministic sorting (OrderBy FQN)
5. Add CancellationToken support
6. Expand test suite from 1 test to 50+ tests

**Diagnostic IDs**:
- ASG0001: Actor must have at least one Step method
- ASG0002: Actor has no entry points ([FirstStep], [Receiver], or [Ingest])
- ASG0003: Ingest method must be static and return Task or IAsyncEnumerable<T>

**Success Criteria**:
- ≥85% code coverage overall
- 100% coverage for Generator, ActorVisitor, ActorGenerator
- <100ms cancellation response time
- Byte-for-byte deterministic output
- Zero concurrency failures in parallel tests

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
