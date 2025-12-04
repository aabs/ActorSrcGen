# Quick Start: Generator Reliability Hardening

**Feature**: Hardening ActorSrcGen Source Generator for Reliability and Testability  
**Branch**: `001-generator-reliability-hardening`  
**Prerequisites**: .NET 8 SDK, Visual Studio 2022 or VS Code with C# extension

## Quick Commands

```powershell
# Clone and navigate
cd D:\dev\aabs\ActorSrcGen
git checkout 001-generator-reliability-hardening

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
reportgenerator -reports:tests/**/coverage.opencover.xml -targetdir:coverage-report

# View coverage
start coverage-report/index.html
```

## Development Workflow (TDD)

### 1. Write Failing Test First (RED)

```csharp
// tests/ActorSrcGen.Tests/ActorVisitorTests.cs
[Fact]
public void VisitActor_WithNoInputMethods_ReturnsASG0002Diagnostic()
{
    // Arrange
    var symbol = TestHelpers.CreateSymbol("""
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
}
```

Run: `dotnet test --filter "FullyQualifiedName~EmptyActor"` → **FAIL** ✗

### 2. Implement Minimum Code (GREEN)

```csharp
// ActorSrcGen/Model/ActorVisitor.cs
public VisitorResult VisitActor(SyntaxAndSymbol symbol)
{
    var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    
    if (!HasAnyStepMethods(symbol.Symbol))
    {
        diagnostics.Add(Diagnostic.Create(
            Diagnostics.MissingInputTypes,
            symbol.Syntax.GetLocation(),
            symbol.Symbol.Name
        ));
        return new VisitorResult(ImmutableArray<ActorNode>.Empty, diagnostics.ToImmutable());
    }
    
    // ... rest of implementation
}
```

Run: `dotnet test --filter "FullyQualifiedName~EmptyActor"` → **PASS** ✓

### 3. Refactor (REFACTOR)

Improve code quality while keeping tests green:
- Extract validation methods
- Reduce complexity
- Add helper functions

Run: `dotnet test` → **ALL PASS** ✓

## Project Structure

```
ActorSrcGen/
├── Generators/
│   ├── Generator.cs              # IIncrementalGenerator implementation
│   └── ActorGenerator.cs         # Code emission logic
├── Model/
│   ├── ActorVisitor.cs           # ⚠️ REFACTOR: Remove mutable state
│   ├── BlockGraph.cs             # ⚠️ REFACTOR: Convert to records
│   └── VisitorResult.cs          # ✨ NEW: Return type for visitor
├── Helpers/
│   ├── RoslynExtensions.cs       # Roslyn API extensions
│   ├── TypeHelpers.cs            # Type rendering
│   └── SyntaxAndSymbol.cs        # ⚠️ REFACTOR: Convert to record
├── Diagnostics/
│   └── DiagnosticDescriptors.cs  # ✨ NEW: Centralized diagnostics
└── Templates/
    └── Actor.tt                  # T4 template for code generation

tests/ActorSrcGen.Tests/
├── GeneratorTests.cs              # ✨ NEW: Generator pipeline tests
├── ActorVisitorTests.cs           # ✨ NEW: Visitor logic tests
├── DiagnosticTests.cs             # ✨ NEW: Diagnostic reporting tests
├── DeterminismTests.cs            # ✨ NEW: Byte-for-byte stability
├── CancellationTests.cs           # ✨ NEW: Cancellation handling
├── ThreadSafetyTests.cs           # ✨ NEW: Parallel execution
├── SnapshotTests/                 # ✨ NEW: Generated code snapshots
│   ├── SingleInputOutput.verified.cs
│   └── MultipleInputs.verified.cs
├── TestHelpers/
│   ├── CompilationHelper.cs       # ✨ NEW: Test compilation setup
│   └── SnapshotHelper.cs          # ✨ NEW: Verify support
└── GeneratorSmokeTests.cs         # ✅ EXISTS: Keep as regression test
```

## Key Refactorings

### Refactoring 1: Convert BlockNode to Record

**Before** (Class with mutable properties):
```csharp
public class BlockNode {
    public string HandlerBody { get; set; }
    public List<int> NextBlocks { get; set; } = new();
}
```

**After** (Immutable record):
```csharp
public sealed record BlockNode(
    string HandlerBody,
    int Id,
    IMethodSymbol Method,
    NodeType NodeType,
    ImmutableArray<int> NextBlocks,
    ...
);
```

### Refactoring 2: Visitor Returns Result

**Before** (Void with side effects):
```csharp
public class ActorVisitor {
    public List<ActorNode> Actors => _actorStack.ToList();
    public void VisitActor(SyntaxAndSymbol symbol) {
        // Mutates _actorStack
    }
}
```

**After** (Pure function):
```csharp
public class ActorVisitor {
    public VisitorResult VisitActor(SyntaxAndSymbol symbol) {
        // Returns new result, no mutation
        return new VisitorResult(actors, diagnostics);
    }
}
```

### Refactoring 3: Centralized Diagnostics

**Before** (Inline creation):
```csharp
var descriptor = new DiagnosticDescriptor(
    "ASG0002", "Error generating source", ...
);
```

**After** (Centralized):
```csharp
using static ActorSrcGen.Diagnostics.Diagnostics;

var diagnostic = Diagnostic.Create(
    MissingInputTypes,
    location,
    actorName
);
```

## Testing Guide

### Running Specific Test Categories

```powershell
# Unit tests only (fast)
dotnet test --filter "Category=Unit"

# Integration tests
dotnet test --filter "Category=Integration"

# Snapshot tests
dotnet test --filter "Category=Snapshot"

# Critical path tests (must be 100%)
dotnet test --filter "Category=Critical"
```

### Verifying Coverage

```powershell
# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Check coverage threshold
dotnet test /p:CollectCoverage=true /p:Threshold=85 /p:ThresholdType=line
```

**Expected Results**:
- Overall: ≥85% line coverage
- Generator.cs: 100%
- ActorVisitor.cs: 100%
- ActorGenerator.cs: 100%
- DiagnosticDescriptors.cs: 100%

### Snapshot Testing

```powershell
# Accept new snapshots
dotnet test -- Verify.DiffTool=none

# Review snapshot changes
dotnet test -- Verify.DiffTool=VisualStudio
```

## Common Issues & Solutions

### Issue 1: Test Fails with "Generator not found"

**Symptom**: Test cannot load Generator class

**Solution**: Ensure test project references generator project as analyzer:
```xml
<ProjectReference Include="..\..\ActorSrcGen\ActorSrcGen.csproj" 
                  ReferenceOutputAssembly="false" 
                  OutputItemType="Analyzer" />
```

### Issue 2: Snapshot Test Fails with Line Ending Differences

**Symptom**: Snapshot diff shows `\r\n` vs `\n`

**Solution**: Normalize line endings before verification:
```csharp
var normalized = generatedCode.Replace("\r\n", "\n");
await Verify(normalized).UseFileName("MyTest");
```

### Issue 3: Coverage Report Missing Files

**Symptom**: Some files show 0% coverage despite having tests

**Solution**: Exclude generated files from coverage:
```xml
<ItemGroup>
  <Compile Update="Templates\Actor.cs">
    <ExcludeFromCodeCoverage>true</ExcludeFromCodeCoverage>
  </Compile>
</ItemGroup>
```

## Validation Checklist

Before submitting PR, verify:

- [ ] All tests pass: `dotnet test`
- [ ] Coverage ≥85%: `dotnet test /p:Threshold=85`
- [ ] Critical paths 100%: Check report for Generator, ActorVisitor, ActorGenerator
- [ ] No compiler warnings: `dotnet build /warnaserror`
- [ ] Snapshots accepted: Review `*.verified.cs` files
- [ ] Determinism verified: Run `DeterminismTests` 10+ times
- [ ] Constitution principles followed (see research.md violations list)

## Next Steps After Hardening

1. **Update README.md** with new testing requirements
2. **Update CONTRIBUTING.md** with TDD workflow
3. **Configure CI/CD** with coverage gates
4. **Add EditorConfig** with code style rules (CC ≤ 5)
5. **Document diagnostic IDs** in user-facing documentation

## Helpful Resources

- **Constitution**: `.specify/memory/constitution.md`
- **Research**: `specs/001-generator-reliability-hardening/research.md`
- **Data Model**: `specs/001-generator-reliability-hardening/data-model.md`
- **Copilot Instructions**: `.github/copilot/copilot-instructions.md`
- **Roslyn Source Generators**: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview
- **xUnit Documentation**: https://xunit.net/docs/getting-started/netcore/cmdline
- **Verify Documentation**: https://github.com/VerifyTests/Verify

## Quick Reference: Constitutional Principles

| Principle | Status | Key Action |
|-----------|--------|----------|
| I. TDD | ❌ Violated | Write tests FIRST (Red-Green-Refactor) |
| II. Coverage | ❌ Violated | Achieve 85% overall, 100% critical |
| III. Immutability | ❌ Violated | Convert to records + ImmutableArray |
| IV. Diagnostics | ❌ Violated | Centralize DiagnosticDescriptors |
| V. Complexity | ⚠️ Partial | Reduce CC to ≤ 5 (target) or ≤ 8 (max) |
| VI. Testability | ❌ Violated | Refactor visitor to return VisitorResult |

**Goal**: Transform all ❌ to ✅ through systematic refactoring with tests.
