# Implementation Plan: Generator Reliability Hardening

**Branch**: `001-generator-reliability-hardening` | **Date**: 2025-12-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-generator-reliability-hardening/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Harden ActorSrcGen source generator for production reliability and testability by (1) converting mutable domain models to immutable records with ImmutableArray<T>, (2) refactoring ActorVisitor to return VisitorResult (pure function), (3) centralizing DiagnosticDescriptors, (4) adding deterministic sorting by fully-qualified name, (5) implementing CancellationToken support, and (6) expanding test coverage from 0-20% to ≥85% overall with 100% coverage for critical paths (Generator, ActorVisitor, ActorGenerator). Technical approach: TDD-first implementation using xUnit + Verify snapshot testing, Coverlet for coverage validation, records with System.Collections.Immutable for thread-safe data structures.

## Technical Context

**Language/Version**: C# 12.0 (tests/playground on .NET 8.0, generator on .NET Standard 2.0)  
**Primary Dependencies**: 
- Microsoft.CodeAnalysis.CSharp 4.6.0 (Roslyn SDK)
- System.Collections.Immutable 8.0.0
- Gridsum.DataflowEx 2.0.0 (TPL Dataflow)
- xUnit 2.6.6 (testing)
- Verify.Xunit 24.0.0 (snapshot testing)
- Coverlet.Collector 6.0.0 (code coverage)

**Storage**: N/A (in-memory compilation pipeline)  
**Testing**: xUnit with Verify for snapshot testing, Coverlet for coverage  
**Target Platform**: Visual Studio 2022 / VS Code with C# extension, Roslyn 4.6+
**Project Type**: Single project (Roslyn incremental source generator)  
**Performance Goals**: 
- <100ms cancellation response time (FR-009)
- <30s test suite execution (FR-018)
- Byte-for-byte deterministic output (FR-001)

**Constraints**: 
- .NET Standard 2.0 compatibility (generator project)
- Roslyn 4.6.0 API constraints
- Must support incremental generation
- Zero threading issues in parallel builds

**Scale/Scope**: 
- 3 primary source files (Generator.cs, ActorVisitor.cs, ActorGenerator.cs)
- ~50+ new unit tests
- 10+ integration tests
- 5+ snapshot tests
- Target: 85% overall coverage, 100% critical path coverage

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Initial Assessment (Pre-Research)

**Status**: ❌ **6 MAJOR VIOLATIONS** - Feature explicitly designed to remediate constitutional deviations

| Principle | Status | Issue | Location |
|-----------|--------|-------|----------|
| I. TDD | ❌ VIOLATED | No tests written first, implementation exists without tests | tests/ActorSrcGen.Tests/ (only 1 smoke test) |
| II. Coverage | ❌ VIOLATED | 0-20% coverage vs required 85% overall, 0% vs 100% critical | Generator.cs, ActorVisitor.cs, ActorGenerator.cs |
| III. Immutability | ❌ VIOLATED | Mutable classes with List<T>, not records with ImmutableArray | BlockGraph.cs (ActorNode, BlockNode), ActorVisitor.cs (_actorStack, _blockStack) |
| IV. Diagnostics | ❌ VIOLATED | Inline DiagnosticDescriptor creation, no centralized Diagnostics class | Generator.cs:90, ActorGenerator.cs (inline descriptors) |
| V. Complexity | ⚠️ PARTIAL | Some methods likely exceed CC≤5 target | ActorVisitor.VisitActor (needs analysis) |
| VI. Testability | ❌ VIOLATED | Void methods with side effects, no CancellationToken support | ActorVisitor.VisitActor (void, mutates state), Generator.Generate (no cancellation) |

**Justification**: This feature's PURPOSE is to remediate these violations. Implementation will follow TDD strictly to transform all ❌ to ✅.

### Post-Design Re-Evaluation (After Phase 1)

**Status**: ✅ **DESIGN COMPLIANT** - All violations addressed in design phase

| Principle | New Status | Remediation in Design | Evidence |
|-----------|------------|----------------------|----------|
| I. TDD | ✅ COMPLIANT | Quickstart.md mandates Red-Green-Refactor workflow | [quickstart.md](quickstart.md) "Write Failing Test First" |
| II. Coverage | ✅ COMPLIANT | 50+ tests planned, Coverlet configured for 85%/100% gates | [research.md](research.md) "Testing Strategy", [data-model.md](data-model.md) "Testing Implications" |
| III. Immutability | ✅ COMPLIANT | All domain models converted to records with ImmutableArray | [data-model.md](data-model.md) ActorNode, BlockNode, SyntaxAndSymbol, VisitorResult |
| IV. Diagnostics | ✅ COMPLIANT | DiagnosticDescriptors static class with ASG0001-0003 | [data-model.md](data-model.md) "DiagnosticDescriptors Entity" |
| V. Complexity | ✅ COMPLIANT | Helper methods extracted, validation properties computed | [data-model.md](data-model.md) ActorNode.HasAnyInputTypes, ActorNode.HasDisjointInputTypes |
| VI. Testability | ✅ COMPLIANT | Visitor returns VisitorResult (pure function), CancellationToken added | [data-model.md](data-model.md) "VisitorResult Entity", [research.md](research.md) Decision 2 |

**Approval**: Design phase complete. Implementation may proceed with TDD workflow.

## Project Structure

### Documentation (this feature)

```text
specs/001-generator-reliability-hardening/
├── plan.md              # ✅ This file (/speckit.plan command output)
├── research.md          # ✅ Phase 0 output (constitutional violations analysis)
├── data-model.md        # ✅ Phase 1 output (immutable domain model design)
├── quickstart.md        # ✅ Phase 1 output (TDD workflow guide)
├── spec.md              # ✅ Feature specification (input)
├── checklists/
│   └── requirements.md  # ✅ Specification quality validation
└── tasks.md             # ⏳ Phase 2 output (/speckit.tasks command - NOT YET CREATED)
```

### Source Code (repository root)

```text
ActorSrcGen/                          # Generator project (.NET Standard 2.0)
├── Generators/
│   ├── Generator.cs                  # ⚠️ REFACTOR: Add sorting, cancellation, remove GenContext property
│   ├── GenerationContext.cs          # ⚠️ REVIEW: Ensure thread-safe usage
│   └── ActorGenerator.cs             # ⚠️ REFACTOR: Use SourceText.From with UTF-8
├── Model/
│   ├── ActorVisitor.cs               # ⚠️ REFACTOR: Return VisitorResult, remove mutable state
│   └── BlockGraph.cs                 # ⚠️ REFACTOR: Convert classes to records
├── Helpers/
│   ├── RoslynExtensions.cs           # ✅ KEEP: Utility extensions
│   ├── TypeHelpers.cs                # ✅ KEEP: Type rendering
│   ├── SyntaxAndSymbol.cs            # ⚠️ REFACTOR: Convert to record
│   └── DomainRoslynExtensions.cs     # ✅ KEEP: Domain-specific extensions
├── Diagnostics/                      # ✨ NEW FOLDER
│   └── DiagnosticDescriptors.cs      # ✨ NEW: Centralized diagnostic definitions
└── Templates/
    ├── Actor.tt                      # ✅ KEEP: T4 template
    └── Actor.cs                      # ✅ KEEP: Generated template class

ActorSrcGen.Abstractions/             # ✅ UNCHANGED: Public API attributes
├── ActorAttribute.cs
├── StepAttribute.cs
├── FirstStepAttribute.cs
├── LastStepAttribute.cs
├── ReceiverAttribute.cs
└── IngestAttribute.cs

tests/ActorSrcGen.Tests/              # Test suite (massive expansion planned)
├── Helpers/                          # ✨ NEW FOLDER
│   ├── CompilationHelper.cs          # ✨ NEW: Test compilation setup
│   └── SnapshotHelper.cs             # ✨ NEW: Verify support utilities
├── Unit/                             # ✨ NEW FOLDER
│   ├── ActorVisitorTests.cs          # ✨ NEW: ~20 tests for visitor logic
│   ├── ActorNodeTests.cs             # ✨ NEW: ~10 tests for record validation
│   ├── BlockNodeTests.cs             # ✨ NEW: ~5 tests for block record
│   └── DiagnosticTests.cs            # ✨ NEW: ~10 tests for diagnostic creation
├── Integration/                      # ✨ NEW FOLDER
│   ├── GeneratorTests.cs             # ✨ NEW: ~15 tests for end-to-end pipeline
│   ├── DeterminismTests.cs           # ✨ NEW: ~5 tests for byte-for-byte stability
│   ├── CancellationTests.cs          # ✨ NEW: ~5 tests for cancellation handling
│   └── ThreadSafetyTests.cs          # ✨ NEW: ~5 tests for parallel execution
├── Snapshots/                        # ✨ NEW FOLDER
│   ├── GeneratedCode/                # ✨ NEW: Snapshot test cases
│   │   ├── SingleInputOutputTest.cs  # ✨ NEW: Basic actor snapshot
│   │   ├── MultipleInputsTest.cs     # ✨ NEW: Multi-input actor
│   │   ├── FirstStepTest.cs          # ✨ NEW: [FirstStep] attribute
│   │   ├── LastStepTest.cs           # ✨ NEW: [LastStep] attribute
│   │   └── IngestMethodTest.cs       # ✨ NEW: [Ingest] method
│   └── *.verified.cs                 # ✨ NEW: Verified snapshot files
├── GeneratorSmokeTests.cs            # ✅ KEEP: Existing smoke test
└── Usings.cs                         # ✅ KEEP: Global usings

ActorSrcGen.Playground/               # ✅ UNCHANGED: Manual testing playground
```

**Structure Decision**: Single project structure (Option 1) with hierarchical test organization. Tests organized by category (Unit/Integration/Snapshots) for clarity and parallel execution. New `Diagnostics/` folder added to generator project for centralized diagnostic management. Existing files marked for refactoring (⚠️) or preservation (✅), new files marked (✨).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**Status**: ✅ **NO UNJUSTIFIED COMPLEXITY**

All design decisions reduce complexity:
1. Records replace classes → fewer lines of code
2. VisitorResult return type → eliminates mutable state tracking
3. Centralized DiagnosticDescriptors → eliminates duplication
4. Helper properties (HasAnyInputTypes) → extract validation logic, reduce CC
5. ImmutableArray → eliminates defensive copying

**Potential CC>5 Methods** (to monitor during implementation):
- ActorVisitor.VisitActor: Current implementation likely CC>5 due to nested conditionals
  - **Mitigation**: Extract validation methods (ValidateInputTypes, ValidateStepMethods, etc.)
  - **Target**: CC ≤ 5 after extraction
  
If any method exceeds CC=8 during implementation, refactor immediately or document justification here.

## Implementation Phases

### Phase 0: Foundation (Research) ✅ COMPLETE
**Artifacts**: [research.md](research.md)
- Constitutional violation analysis (6 violations identified)
- 5 major implementation decisions documented
- Testing strategy defined (50+ tests planned)
- Performance considerations analyzed
- Migration path from classes to records

### Phase 1: Design ✅ COMPLETE
**Artifacts**: [data-model.md](data-model.md), [quickstart.md](quickstart.md), [.github/agents/copilot-instructions.md](../../.github/agents/copilot-instructions.md)
- Immutable domain model defined (SyntaxAndSymbol, VisitorResult, ActorNode, BlockNode, IngestMethod)
- Centralized DiagnosticDescriptors designed (ASG0001, ASG0002, ASG0003)
- Entity relationships documented
- Validation invariants specified
- TDD workflow guide created
- Agent context updated with constitutional principles

### Phase 2: Task Breakdown ⏳ PENDING
**Command**: `/speckit.tasks` (NOT YET EXECUTED)
**Expected Output**: [tasks.md](tasks.md) with:
- Detailed task breakdown following TDD workflow
- Task sequencing (Foundation → Reliability → Tests → Coverage)
- Acceptance criteria per task
- Estimated complexity per task

### Phase 3: Implementation ⏳ PENDING
**Execution**: Follow tasks.md using quickstart.md TDD workflow
**Process**:
1. Write failing test (RED)
2. Implement minimum code (GREEN)
3. Refactor while keeping tests green (REFACTOR)
4. Commit test + implementation together
5. Validate coverage thresholds

### Phase 4: Validation ⏳ PENDING
**Criteria** (from [spec.md](spec.md) Success Criteria):
- [ ] ≥85% overall code coverage (SC-001)
- [ ] 100% coverage for Generator, ActorVisitor, ActorGenerator (SC-001)
- [ ] Byte-for-byte deterministic output (SC-002)
- [ ] <100ms cancellation response time (SC-003)
- [ ] Zero threading failures in parallel builds (SC-004)
- [ ] <30s test suite execution (SC-005)
- [ ] All 20 functional requirements validated (SC-006)
- [ ] All 3 diagnostic IDs tested (SC-007)
- [ ] Snapshot tests for 5+ actor patterns (SC-008)
- [ ] CI/CD pipeline green with coverage gates (SC-009)

## Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Breaking changes to existing actors | HIGH | HIGH | Comprehensive snapshot tests to catch regressions |
| Performance regression from immutability | MEDIUM | MEDIUM | Benchmark before/after, accept <5% overhead |
| Difficulty achieving 100% critical coverage | MEDIUM | HIGH | Focus on critical paths first, defer edge cases |
| Roslyn API limitations for testing | LOW | MEDIUM | Use CSharpGeneratorDriver for in-memory compilation |
| Test suite execution time >30s | MEDIUM | MEDIUM | Parallelize tests, use [Collection] attributes sparingly |

## Next Steps

1. **Execute `/speckit.tasks`** to generate [tasks.md](tasks.md) with detailed task breakdown
2. **Review tasks.md** for completeness and sequencing
3. **Begin TDD implementation** following [quickstart.md](quickstart.md) workflow
4. **Track progress** against Success Criteria in [spec.md](spec.md)
5. **Update CI/CD** to enforce 85% coverage threshold

## References

- **Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
- **Copilot Instructions**: [.github/copilot/copilot-instructions.md](../../.github/copilot/copilot-instructions.md)
- **Agent Instructions**: [.github/agents/copilot-instructions.md](../../.github/agents/copilot-instructions.md)
- **Feature Specification**: [spec.md](spec.md)
- **Research Analysis**: [research.md](research.md)
- **Data Model Design**: [data-model.md](data-model.md)
- **Quick Start Guide**: [quickstart.md](quickstart.md)
