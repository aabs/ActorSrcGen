# Tasks: Generator Reliability Hardening

**Feature**: Hardening ActorSrcGen Source Generator for Reliability and Testability  
**Specification**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Quick Start**: [quickstart.md](quickstart.md)  
**Branch**: `001-generator-reliability-hardening` | **Generated**: 2025-12-05

## Overview

This document breaks down the feature specification into executable tasks organized by user story. Each task follows the strict checklist format and can be completed independently within its story's phase. Tasks are sequenced to enable TDD-first implementation.

**Total Tasks**: 85 tasks across 6 phases and 3 user stories (P1, P2, P3)  
**Estimated Duration**: 40-60 hours (assuming 4-6 hours per major refactoring task)  
**MVP Scope**: Phase 1 (Setup) → Phase 2 (Foundation) → Phase 3 (US1: Determinism & Diagnostics)

---

## Dependencies & Execution Order

```
Phase 1: SETUP (BLOCKING all user stories)
  ↓
Phase 2: FOUNDATION (BLOCKING all user stories)
  ├── Phase 3: US1 - Determinism & Diagnostic Reporting (P1) [PARALLEL INDEPENDENT]
  ├── Phase 4: US2 - Thread Safety & Cancellation (P2)    [PARALLEL to US1]
  ├── Phase 5: US3 - Test Suite & Coverage (P3)           [PARALLEL to US1/US2]
  ↓
Phase 6: POLISH & CROSS-CUTTING (depends on all stories)
```

**Parallelization**: Once Foundation (Phase 2) completes, all three user stories can be implemented in parallel:
- **US1 (Determinism)**: Affects sorting logic, impacts Generator.cs
- **US2 (Thread Safety)**: Affects state management, impacts ActorVisitor.cs
- **US3 (Tests)**: Affects all components equally, can start immediately

**Dependencies Within Stories**:
- US1: Models → Diagnostics → Sorting → Integration tests
- US2: Models → Visitor refactor → Cancellation → Concurrency tests
- US3: Test infrastructure → Unit tests → Integration tests → Snapshots

---

## Phase 1: Setup (Project Initialization)

**Goals**: Create test infrastructure, establish tooling, configure CI/CD  
**Independent Test Criteria**: `dotnet build` succeeds, test project loads  
**Estimated Time**: 4-6 hours

### Create Test Project Structure

- [X] T001 Create folders: `tests/ActorSrcGen.Tests/{Helpers,Unit,Integration,Snapshots/GeneratedCode}`
- [X] T002 [P] Create `tests/ActorSrcGen.Tests/Usings.cs` with global usings for xUnit, Verify, System.Collections.Immutable
- [X] T003 [P] Create `tests/ActorSrcGen.Tests.csproj` with xUnit 2.6.6, Verify.Xunit 24.0.0, Coverlet.Collector 6.0.0, Microsoft.CodeAnalysis.CSharp 4.6.0
- [X] T004 [P] Add project reference to `ActorSrcGen.csproj` as analyzer: `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`
- [X] T005 [P] Add ReportGenerator target to `.csproj`: `<ReportGeneratorTool Include="ReportGenerator" Version="5.1.0" />`

### Add Test Infrastructure Classes

- [X] T006 Create `tests/ActorSrcGen.Tests/Helpers/CompilationHelper.cs` with:
  - `CreateCompilation(sourceCode: string): CSharpCompilation`
  - `CreateGeneratorDriver(compilation: CSharpCompilation): GeneratorDriver`
  - `GetGeneratedOutput(GeneratorDriver driver): Dictionary<string, string>`
- [X] T007 [P] Create `tests/ActorSrcGen.Tests/Helpers/SnapshotHelper.cs` with:
  - `NormalizeLineEndings(code: string): string` (convert \r\n to \n)
  - `FormatGeneratedCode(code: string): string` (consistent formatting)
  - `VerifyGeneratedOutput(code: string, fileName: string): Task`
- [X] T008 [P] Create `tests/ActorSrcGen.Tests/Helpers/TestActorFactory.cs` with:
  - `CreateTestActor(name: string, steps: string[]): string` (generates test actor source)
  - `CreateActorWithIngest(name: string): string` (generates actor with [Ingest])
  - `CreateActorWithMultipleInputs(name: string, inputCount: int): string`

### Configure Coverage & CI/CD

- [X] T009 Update `ActorSrcGen.Tests.csproj` to enable Coverlet:
  ```xml
  <PropertyGroup>
    <CollectCoverage>true</CollectCoverage>
    <CoverletOutputFormat>opencover</CoverletOutputFormat>
    <Threshold>85</Threshold>
    <ThresholdType>line</ThresholdType>
  </PropertyGroup>
  ```
- [X] T010 [P] Create `.github/workflows/coverage.yml` with:
  - Run `dotnet test /p:CollectCoverage=true`
  - Generate coverage report with ReportGenerator
  - Fail if coverage < 85%
  - Upload coverage to Codecov (optional)
- [X] T011 [P] Create `.editorconfig` entry enforcing CC ≤ 5:
  ```
  [ActorSrcGen/**.cs]
  dotnet_code_quality_unused_parameters = all
  dotnet_diagnostic.CA1501.severity = warning  # cyclomatic complexity > 5
  ```

### Verify Setup

- [X] T012 Run `dotnet build` → ✅ succeeds with no warnings
- [X] T013 Run `dotnet test --collect:"XPlat Code Coverage"` → ✅ test project loads, baseline coverage reports
- [X] T014 Verify snapshot folder exists: `tests/ActorSrcGen.Tests/Snapshots/`

---

## Phase 2: Foundation (Blocking Prerequisites)

**Goals**: Establish immutable data structures and centralized diagnostics  
**Independent Test Criteria**: New record types compile and can be instantiated  
**Estimated Time**: 10-12 hours

### Create Domain Model Records

- [X] T015 [P] Create `ActorSrcGen/Helpers/SyntaxAndSymbol.cs` as immutable record:
  ```csharp
  public sealed record SyntaxAndSymbol(
      ClassDeclarationSyntax Syntax,
      INamedTypeSymbol Symbol,
      SemanticModel SemanticModel
  );
  ```
- [X] T016 [P] Create `ActorSrcGen/Model/ActorNode.cs` as immutable record with:
  - Properties: Name, FullName, BlockNodes, StepMethods, HasAnyInputTypes, HasDisjointInputTypes, IngestMethods
  - All using `ImmutableArray<T>` for collections
  - Computed properties for validation (see [data-model.md](data-model.md))
- [X] T017 [P] Create `ActorSrcGen/Model/BlockNode.cs` as immutable record with:
  - Properties: Id, HandlerBody, Method, NodeType, NextBlocks
  - NodeType enum: Step, FirstStep, LastStep, Receiver
- [X] T018 [P] Create `ActorSrcGen/Model/IngestMethod.cs` as immutable record with:
  - Properties: Name, ReturnType, Symbol, SourceLocation
- [X] T019 Create `ActorSrcGen/Model/VisitorResult.cs` as immutable record:
  ```csharp
  public sealed record VisitorResult(
      ImmutableArray<ActorNode> Actors,
      ImmutableArray<Diagnostic> Diagnostics
  );
  ```

### Centralize Diagnostics

- [X] T020 Create `ActorSrcGen/Diagnostics/Diagnostics.cs` with static readonly DiagnosticDescriptor instances:
  - ASG0001: "Actor must define at least one Step method"
  - ASG0002: "Actor has no entry points (FirstStep, Receiver, or Ingest)"
  - ASG0003: "Ingest method must be static and return Task or IAsyncEnumerable<T>"
  - All with severity=Warning, default enabled
- [X] T021 [P] Create helper: `Diagnostic CreateDiagnostic(DiagnosticDescriptor, Location, params object[]): Diagnostic`

### Update Existing Classes for Immutability

- [X] T022 Update `ActorSrcGen/Model/BlockGraph.cs`:
  - Convert to use ImmutableArray<T> instead of List<T>
  - Ensure all collections are read-only
  - Add validation in constructors
- [X] T023 [P] Update `ActorSrcGen/Helpers/TypeHelpers.cs` to handle ImmutableArray<T> rendering

### Unit Tests for Foundation

- [X] T024 [P] Create `tests/ActorSrcGen.Tests/Unit/ActorNodeTests.cs`:
  - Test construction with valid data
  - Test HasAnyInputTypes computed property
  - Test HasDisjointInputTypes computed property
  - 5 tests total, all should pass
- [X] T025 [P] Create `tests/ActorSrcGen.Tests/Unit/BlockNodeTests.cs`:
  - Test construction with valid data
  - Test NextBlocks immutability
  - 3 tests total
- [X] T026 [P] Create `tests/ActorSrcGen.Tests/Unit/DiagnosticTests.cs`:
  - Test all 3 DiagnosticDescriptors are defined
  - Test ASG0001, ASG0002, ASG0003 have correct properties
  - Test diagnostic creation helpers
  - 5 tests total

### Verify Foundation

- [X] T027 Run `dotnet test --filter "Category=Unit"` → ✅ all foundation tests pass
- [X] T028 Run `dotnet build` → ✅ no breaking changes to existing code

---

## Phase 3: User Story 1 - Determinism & Diagnostic Reporting (P1)

**Goal**: Ensure byte-for-byte deterministic generation with proper diagnostic reporting  
**Independent Test Criteria**: Generated output identical across multiple runs, all ASG diagnostics reported correctly  
**Estimated Time**: 12-15 hours

### Refactor ActorVisitor for Pure Functions

- [X] T029 [US1] Create `tests/ActorSrcGen.Tests/Unit/ActorVisitorTests.cs` with failing tests:
  - `VisitActor_WithValidInput_ReturnsActorNode` → RED
  - `VisitActor_WithNoInputMethods_ReturnsASG0002Diagnostic` → RED
  - `VisitActor_WithMultipleInputs_ReturnsCorrectBlockGraph` → RED
  - 3 tests total to drive initial implementation
- [X] T030 [US1] Refactor `ActorSrcGen/Model/ActorVisitor.cs`:
  - Remove all instance fields (_actorStack, _blockStack, BlockCounter)
  - Change `VisitActor(INamedTypeSymbol): void` → `VisitActor(SyntaxAndSymbol): VisitorResult`
  - Return immutable VisitorResult with ActorNode[] and Diagnostic[]
  - Ensure pure function (no side effects)
- [X] T031 [US1] Extract validation helpers from ActorVisitor:
  - `ValidateInputTypes(ActorNode): ImmutableArray<Diagnostic>`
  - `ValidateStepMethods(ActorNode): ImmutableArray<Diagnostic>`
  - `ValidateIngestMethods(ActorNode): ImmutableArray<Diagnostic>`
  - Each returns empty if valid, diagnostic if invalid
- [ ] T032 [US1] Add unit tests for validation helpers:
  - Test ValidateInputTypes with valid/invalid inputs
  - Test ValidateStepMethods with valid/invalid methods
  - Test ValidateIngestMethods with static/instance methods
  - 8 tests total

### Implement Deterministic Sorting

- [X] T033 [US1] Update `ActorSrcGen/Generators/Generator.cs`:
  - Remove `GenContext` instance property (state)
  - Change: `foreach(var actor in symbols)` → `foreach(var actor in symbols.OrderBy(s => s.ToDisplayString(FullyQualifiedFormat)))`
  - Add cancellation token check: `cancellationToken.ThrowIfCancellationRequested()`
- [X] T034 [US1] Update `ActorSrcGen/Generators/ActorGenerator.cs`:
  - Add sorting before emission: `actors.OrderBy(a => a.FullName)`
  - Ensure all nested loops use sorted collections
- [X] T035 [US1] Create determinism test `tests/ActorSrcGen.Tests/Integration/DeterminismTests.cs`:
  - `Generate_MultipleRuns_ProduceIdenticalOutput` (run 5 times, compare byte arrays)
  - `Generate_DifferentRunOrder_SameOutput` (shuffle input order, verify output identical)
  - 2 tests total

### Implement Centralized Diagnostic Reporting

- [X] T036 [US1] Update `ActorSrcGen/Generators/Generator.cs`:
  - Replace all inline `DiagnosticDescriptor.Create()` with `Diagnostic.Create(Diagnostics.ASG0001, ...)`
  - Collect all diagnostics from VisitorResult
  - Add diagnostics via `context.ReportDiagnostic()`
- [X] T037 [US1] Create integration test `tests/ActorSrcGen.Tests/Integration/DiagnosticReportingTests.cs`:
  - `MissingInputTypes_ReportsASG0002` (verify diagnostic ID, message, location)
  - `InvalidIngestMethod_ReportsASG0003` (test static/return type validation)
  - `MultipleErrors_ReportsAllDiagnostics` (actor with multiple violations)
  - 3 tests total
- [X] T038 [US1] Create snapshot tests for diagnostic messages:
  - `tests/ActorSrcGen.Tests/Snapshots/DiagnosticMessages/ASG0001.verified.txt`
  - `tests/ActorSrcGen.Tests/Snapshots/DiagnosticMessages/ASG0002.verified.txt`
  - `tests/ActorSrcGen.Tests/Snapshots/DiagnosticMessages/ASG0003.verified.txt`

### Integration Tests for US1

- [X] T039 [US1] Create `tests/ActorSrcGen.Tests/Integration/GeneratedCodeTests.cs`:
  - `GenerateSingleInputOutput_ProducesValidCode` (basic actor snapshot)
  - `GenerateMultipleInputs_ProducesValidCode` (multi-input actor)
  - `GenerateWithFirstStep_ProducesValidCode` (FirstStep attribute)
  - `GenerateWithLastStep_ProducesValidCode` (LastStep attribute)
  - 4 tests total with snapshots
- [X] T040 [US1] Create snapshot files:
  - `tests/ActorSrcGen.Tests/Snapshots/GeneratedCode/SingleInputOutput.verified.cs`
  - `tests/ActorSrcGen.Tests/Snapshots/GeneratedCode/MultipleInputs.verified.cs`
  - `tests/ActorSrcGen.Tests/Snapshots/GeneratedCode/FirstStepPattern.verified.cs`
  - `tests/ActorSrcGen.Tests/Snapshots/GeneratedCode/LastStepPattern.verified.cs`

### Validate US1 Complete

- [ ] T041 [US1] Run `dotnet test --filter "Category=US1"` → ✅ all tests pass
- [ ] T042 [US1] Verify determinism: `dotnet test DeterminismTests -p:Sequential=true` 10 times → ✅ identical output
- [ ] T043 [US1] Verify coverage for Generator.cs, ActorGenerator.cs ≥95%

---

## Phase 4: User Story 2 - Thread Safety & Cancellation (P2)

**Goal**: Ensure generator is thread-safe and supports cancellation  
**Independent Test Criteria**: No race conditions in parallel builds, cancellation within 100ms  
**Estimated Time**: 10-12 hours

### Refactor for Thread Safety

- [ ] T044 [US2] Create failing tests in `tests/ActorSrcGen.Tests/Unit/ActorVisitorThreadSafetyTests.cs`:
  - `VisitActor_ConcurrentCalls_ProduceIndependentResults` → RED
  - 1 test to drive immutability validation
- [ ] T045 [US2] Update `ActorSrcGen/Model/ActorVisitor.cs`:
  - Ensure no shared mutable state (already done in T030, verify here)
  - Add readonly modifiers to all fields
  - Add sealed modifier to class
- [ ] T046 [US2] Update `ActorSrcGen/Generators/GenerationContext.cs`:
  - Verify thread-safe usage patterns
  - Document thread-safety guarantees
  - Add [ThreadSafe] documentation comments

### Implement Cancellation Support

- [ ] T047 [US2] Update `ActorSrcGen/Generators/Generator.cs`:
  - Add `cancellationToken.ThrowIfCancellationRequested()` in:
    - Main foreach loop (after each symbol processing)
    - ActorGenerator emission loop
    - Long-running operations
  - Test cancellation is honored within 100ms
- [ ] T048 [US2] Add unit tests for cancellation in `tests/ActorSrcGen.Tests/Unit/CancellationTests.cs`:
  - `Generate_CancellationToken_CancelsWithin100ms` (measure elapsed time)
  - `Generate_CancelledMidway_ReturnsPartialResults` (verify partial work)
  - 2 tests total
- [ ] T049 [US2] Update `SourceText.From()` calls:
  - Replace all: `SourceText.From(source)` → `SourceText.From(source, Encoding.UTF8)`
  - Add using: `using System.Text;`
  - Ensures consistent encoding

### Concurrent Safety Tests

- [ ] T050 [US2] Create `tests/ActorSrcGen.Tests/Integration/ThreadSafetyTests.cs`:
  - `Generate_ParallelCompilations_NoRaceConditions` (run 10 parallel generator invocations)
  - `Generate_SharedSymbols_IndependentResults` (verify each call is independent)
  - `VisitActor_Parallel_AllProduceValidResults` (10 parallel visits)
  - 3 tests total
- [ ] T051 [US2] Create `tests/ActorSrcGen.Tests/Integration/CancellationIntegrationTests.cs`:
  - `Generate_CancellationRequested_StopsEarly` (request cancellation, verify stopped)
  - `Generate_PartialWork_RespectsCancellation` (cancel mid-way through compilation)
  - 2 tests total

### Stress Testing

- [ ] T052 [US2] Create stress test `tests/ActorSrcGen.Tests/Integration/StressTests.cs`:
  - `Generate_LargeInputSet_HandlesGracefully` (100+ actors)
  - `Generate_DeepNesting_DoesNotStackOverflow` (deeply nested steps)
  - `Generate_ComplexGraphs_HandlesAllPatterns` (complex dataflow)
  - 3 tests total

### Validate US2 Complete

- [ ] T053 [US2] Run `dotnet test --filter "Category=US2"` → ✅ all tests pass
- [ ] T054 [US2] Run parallel tests 10+ times → ✅ no flakiness
- [ ] T055 [US2] Measure cancellation: `Stopwatch` in test → ✅ < 100ms

---

## Phase 5: User Story 3 - Test Suite & Coverage (P3)

**Goal**: Achieve ≥85% overall coverage with 100% critical path coverage  
**Independent Test Criteria**: Coverage report shows 85%+ lines, 100% for Generator/ActorVisitor/ActorGenerator  
**Estimated Time**: 12-15 hours

### Expand Unit Test Suite

- [ ] T056 [US3] Create comprehensive `tests/ActorSrcGen.Tests/Unit/RoslynExtensionTests.cs`:
  - Test all extensions in RoslynExtensions.cs
  - Test all extensions in DomainRoslynExtensions.cs
  - 10+ tests total covering all code paths
- [ ] T057 [US3] Create `tests/ActorSrcGen.Tests/Unit/TypeHelperTests.cs`:
  - Test type name rendering for all scenarios
  - Test ImmutableArray<T> rendering
  - 8+ tests total
- [ ] T058 [US3] Expand ActorVisitorTests with edge cases:
  - Actor with no methods
  - Actor with only Step methods (no FirstStep)
  - Actor with conflicting attributes
  - 5 additional tests
- [ ] T059 [US3] Create `tests/ActorSrcGen.Tests/Unit/BlockGraphConstructionTests.cs`:
  - Test BlockGraph from various actor patterns
  - Test block linking logic
  - Test cycle detection (if applicable)
  - 8+ tests total

### Expand Integration Test Suite

- [ ] T060 [US3] Create comprehensive `tests/ActorSrcGen.Tests/Integration/ActorPatternTests.cs`:
  - Single Step (simplest pattern)
  - FirstStep → Step → LastStep (pipeline)
  - FirstStep + multiple Steps + Receiver
  - Ingest → Step → LastStep
  - Multiple inputs with different entry points
  - Each pattern gets 2-3 test cases, 15+ tests total
- [ ] T061 [US3] Create `tests/ActorSrcGen.Tests/Integration/AttributeValidationTests.cs`:
  - Valid FirstStep usage
  - Invalid FirstStep usage (multiple, on non-public method, etc.)
  - Valid LastStep usage
  - Valid Step usage
  - Valid Receiver usage
  - 10+ tests total
- [ ] T062 [US3] Create `tests/ActorSrcGen.Tests/Integration/IngestMethodTests.cs`:
  - Static ingest returning Task<T>
  - Static ingest returning IAsyncEnumerable<T>
  - Invalid ingest (non-static)
  - Invalid ingest (wrong return type)
  - 8+ tests total

### Expand Snapshot Tests

- [ ] T063 [US3] Create snapshot tests for all major patterns:
  - `SimpleActor.verified.cs` (single step)
  - `PipelineActor.verified.cs` (FirstStep → Step → LastStep)
  - `MultiInputActor.verified.cs` (multiple entry points)
  - `IngestActor.verified.cs` (ingest pattern)
  - `ReceiverActor.verified.cs` (external receiver)
  - `ComplexActor.verified.cs` (all features combined)
  - 6+ snapshot files
- [ ] T064 [US3] Create error snapshot tests:
  - `MissingStepMethods.verified.txt` (ASG0001)
  - `NoInputTypes.verified.txt` (ASG0002)
  - `InvalidIngest.verified.txt` (ASG0003)
  - 3+ snapshot files

### Coverage Analysis & Remediation

- [ ] T065 [US3] Run coverage report: `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover`
- [ ] T066 [US3] Analyze coverage for each critical file:
  - Generator.cs → target 100%, add tests for uncovered branches
  - ActorVisitor.cs → target 100%, add tests for uncovered branches
  - ActorGenerator.cs → target 100%, add tests for uncovered branches
  - BlockGraph.cs → target ≥95%, add tests for edge cases
- [ ] T067 [US3] Add branch coverage tests:
  - Test all if/else branches in visitor logic
  - Test all loop variations
  - Test all exception paths
  - 15+ additional tests

### Performance & Regression Tests

- [ ] T068 [US3] Create `tests/ActorSrcGen.Tests/Integration/PerformanceTests.cs`:
  - `Generate_ExecutesUnder30Seconds` (benchmark full suite)
  - `SingleGeneration_ExecutesUnder100ms` (single actor benchmark)
  - `LargeInput_ExecutesUnder5Seconds` (50+ actors)
  - 3 tests total with performance assertions
- [ ] T069 [US3] Ensure existing smoke test still passes:
  - `GeneratorSmokeTests.cs` → all tests pass
  - Verify no regressions from refactoring

### Validate US3 Complete

- [ ] T070 [US3] Run all tests: `dotnet test` → ✅ all pass, < 30s
- [ ] T071 [US3] Generate coverage report → ✅ ≥85% overall
- [ ] T072 [US3] Verify critical paths: `dotnet test --filter "Category=Critical"` → ✅ 100%

---

## Phase 6: Polish & Cross-Cutting Concerns

**Goal**: Documentation, CI/CD validation, final quality checks  
**Estimated Time**: 4-6 hours

### Documentation Updates

- [ ] T073 Update `ReadMe.md`:
  - Add "Testing" section referencing [quickstart.md](quickstart.md)
  - Add coverage badge: `![Coverage](coverage-report/badge.svg)`
  - Document diagnostic IDs (ASG0001-0003)
- [ ] T074 [P] Create `docs/DIAGNOSTICS.md`:
  - Full reference for each diagnostic ID
  - Common causes and fixes
  - Examples of violations
- [ ] T075 [P] Update `CONTRIBUTING.md`:
  - Add TDD workflow section
  - Reference [quickstart.md](quickstart.md) for testing guide
  - Add coverage threshold requirement (85%)

### Final Validation

- [ ] T076 Clean build: `dotnet clean; dotnet build /warnaserror` → ✅ no warnings/errors
- [ ] T077 Full test suite: `dotnet test --configuration Release` → ✅ all pass
- [ ] T078 Coverage validation: `dotnet test /p:Threshold=85 /p:ThresholdType=line` → ✅ meets threshold
- [ ] T079 Snapshot validation: All `*.verified.cs` files reviewed → ✅ content correct

### CI/CD & Automation

- [ ] T080 [P] Verify `.github/workflows/coverage.yml` runs successfully
- [ ] T081 [P] Configure branch protection to require:
  - All tests passing
  - Coverage ≥85%
  - No compiler warnings
- [ ] T082 [P] Create `.github/workflows/benchmark.yml` for performance regression detection

### Final Checklist

- [ ] T083 Run all success criteria from [spec.md](spec.md):
  - [ ] SC-001: ≥85% coverage, 100% critical paths
  - [ ] SC-002: Byte-for-byte determinism (DeterminismTests)
  - [ ] SC-003: <100ms cancellation (CancellationTests)
  - [ ] SC-004: Zero concurrency failures (ThreadSafetyTests)
  - [ ] SC-005: <30s test execution (PerformanceTests)
  - [ ] SC-006: All 20 FRs validated (task references)
  - [ ] SC-007: All 3 diagnostic IDs tested (DiagnosticTests)
  - [ ] SC-008: 6+ snapshot patterns verified
  - [ ] SC-009: CI/CD green with coverage gates
  - [ ] SC-010: Constitutional principles validated
- [ ] T084 Create PR with:
  - Link to this tasks.md
  - Summary of refactorings
  - Coverage report
  - Performance benchmarks
- [ ] T085 Code review with focus on:
  - TDD discipline (test before implementation)
  - Constitutional compliance (all 6 principles)
  - Coverage thresholds met
  - Performance within bounds
  - No regressions in generated output

---

## Task Quick Reference by Category

### Setup & Infrastructure (T001-T014)
- Project structure: T001-T005
- Test helpers: T006-T008
- CI/CD setup: T009-T011
- Verification: T012-T014

### Foundation - Models (T015-T026)
- Domain records: T015-T019
- Centralized diagnostics: T020-T021
- Immutability updates: T022-T023
- Foundation tests: T024-T026
- Verification: T027-T028

### User Story 1: Determinism (T029-T043)
- Visitor refactoring: T029-T032
- Sorting implementation: T033-T035
- Diagnostic reporting: T036-T038
- Integration tests: T039-T040
- Validation: T041-T043

### User Story 2: Thread Safety (T044-T055)
- Thread safety updates: T044-T046
- Cancellation support: T047-T049
- Concurrent tests: T050-T051
- Stress tests: T052
- Validation: T053-T055

### User Story 3: Coverage (T056-T072)
- Unit tests: T056-T059
- Integration tests: T060-T062
- Snapshot tests: T063-T064
- Coverage analysis: T065-T067
- Performance tests: T068-T069
- Validation: T070-T072

### Polish & Cross-Cutting (T073-T085)
- Documentation: T073-T075
- Final validation: T076-T082
- Success criteria: T083
- PR & review: T084-T085

---

## Parallelization Examples

### Scenario 1: 2 Developers (Recommended)

**Developer A (Foundation & US1)**:
- Phases 1-2: Setup + Foundation (6-8 hours)
- Phase 3: US1 Determinism (12-15 hours)
- Total: 18-23 hours

**Developer B (US2 & US3)**:
- After Phase 2: Implement in parallel
- Phase 4: US2 Thread Safety (10-12 hours)
- Phase 5: US3 Coverage (12-15 hours)
- Total: 22-27 hours (starts after Phase 2 completes)

**Merge Point**: Phase 6 Polish (both together, 4-6 hours)

### Scenario 2: 3 Developers (Optimal)

**Developer A**: Foundation (T015-T028) + US1 Determinism (T029-T043)  
**Developer B**: US2 Thread Safety (T044-T055)  
**Developer C**: US3 Coverage (T056-T072)

All three start Phase 3+ after Phase 2 completes in parallel, reducing total time to ~30-35 hours.

### Scenario 3: Solo Development

Follow sequential order: Phase 1 → 2 → 3 → 4 → 5 → 6  
Estimated 40-60 hours over 2-3 weeks at 20 hrs/week.

---

## Success Metrics

**Coverage**: 
- ✅ PASS: ≥85% overall line coverage
- ✅ PASS: 100% coverage for Generator.cs, ActorVisitor.cs, ActorGenerator.cs
- ❌ FAIL: <85% overall or <100% critical paths → add more tests

**Functionality**:
- ✅ PASS: All 20 functional requirements tested (see [spec.md](spec.md))
- ✅ PASS: All 3 diagnostic IDs working correctly
- ❌ FAIL: Any FR not validated → add test for that FR

**Performance**:
- ✅ PASS: Full test suite < 30 seconds
- ✅ PASS: Single generation < 100ms
- ✅ PASS: Cancellation response < 100ms
- ❌ FAIL: Performance regressions → optimize hot paths

**Quality**:
- ✅ PASS: All tests pass with no flakiness
- ✅ PASS: Cyclomatic complexity ≤ 5 (target) or ≤ 8 (max)
- ✅ PASS: Zero compiler warnings
- ❌ FAIL: Any of above → fix before merging

**Determinism**:
- ✅ PASS: DeterminismTests run 5+ times with identical output
- ✅ PASS: Snapshot tests all verify
- ❌ FAIL: Output differs between runs → debug sorting

**Thread Safety**:
- ✅ PASS: ThreadSafetyTests run with no exceptions
- ✅ PASS: StressTests handle edge cases
- ❌ FAIL: Race conditions or crashes → add synchronization

---

## Notes & Caveats

- **TDD Discipline**: Each task description says "RED" or "FAIL" → implement minimum code to pass
- **Snapshot Approval**: First run of snapshot tests will create `.verified.cs` files → review and approve
- **Coverage Gaps**: If coverage < 85% after all tests, add targeted tests for uncovered branches
- **Breaking Changes**: Generated code may change during refactoring → update snapshots after validation
- **CI/CD Timing**: Coverage.yml may need tuning for your environment (parallelization, timeouts)
- **Complexity Targets**: If any method exceeds CC=8, extract helper methods or document justification

---

## Appendix: Constitutional Principle Validation Per Task

Each task contributes to constitutional compliance:

| Principle | Key Tasks | Validation |
|-----------|-----------|-----------|
| I. TDD | T029, T044, T056 (all marked with RED) | Create failing test first before implementing |
| II. Coverage | T065-T067, T070-T072 | Achieve 85%+ overall, 100% critical paths |
| III. Immutability | T015-T019, T022-T023 | All records, ImmutableArray<T>, sealed |
| IV. Diagnostics | T020-T021, T036-T038 | Centralized DiagnosticDescriptors, ASG codes |
| V. Complexity | T011 (EditorConfig), T067 (branch coverage) | CC ≤ 5 target, extract helpers as needed |
| VI. Testability | T030, T047, T068 | Pure functions, cancellation support, performance |

---

**Next Step**: Begin Phase 1 (T001-T014) following [quickstart.md](quickstart.md) TDD workflow.

