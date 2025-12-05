# Feature Specification: Hardening ActorSrcGen Source Generator for Reliability and Testability

**Feature Branch**: `001-generator-reliability-hardening`  
**Created**: 2025-12-05  
**Status**: Draft  
**Input**: User description: "Hardening ActorSrcGen Source Generator for Reliability and Testability"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterministic Code Generation (Priority: P1) 🎯 MVP

A library maintainer building ActorSrcGen multiple times expects the generated source files to be identical byte-for-byte across builds when inputs haven't changed. The build system should report stable content hashes, allowing incremental builds and reproducible artifacts.

**Why this priority**: Determinism is foundational for reliable builds, caching, and debugging. Without it, developers cannot trust that issues are repeatable, builds cannot be cached effectively, and debugging becomes nearly impossible.

**Independent Test**: Run the generator twice with identical input on the same actor class. Compare generated file content byte-for-byte, including file names, encoding, and line endings. All outputs must be identical.

**Acceptance Scenarios**:

1. **Given** an actor class with [Actor] attribute and valid methods, **When** the generator runs twice without any code changes, **Then** both runs produce identical .generated.cs files with the same content hash
2. **Given** multiple actor classes processed in different order (e.g., alphabetical vs reverse), **When** the generator runs, **Then** the generated code for each actor is identical regardless of processing order
3. **Given** an actor class with Unicode identifiers (e.g., method names with non-ASCII characters), **When** the generator runs multiple times, **Then** the output encoding (UTF-8) and content remain stable

---

### User Story 2 - Clear Diagnostic Reporting (Priority: P1) 🎯 MVP

A developer creates an actor class with validation errors (e.g., multiple input types that aren't disjoint). The build fails with clear, actionable error messages showing exactly which class and methods are problematic, with accurate source locations in the IDE.

**Why this priority**: Without clear diagnostics, developers waste time debugging why generation fails. Accurate error messages with source locations are essential for developer productivity and must be part of the MVP.

**Independent Test**: Create test cases with known validation errors. Verify that diagnostic IDs (ASG####), messages, and source locations are correct and actionable.

**Acceptance Scenarios**:

1. **Given** an actor with multiple input types that are not disjoint, **When** the generator runs, **Then** diagnostic ASG0001 is emitted with a message identifying the actor name and conflicting input types, pointing to the class declaration
2. **Given** an actor with no input methods, **When** the generator runs, **Then** diagnostic ASG0002 is emitted with a message stating "Actor must have at least one input type", pointing to the class declaration
3. **Given** an exception during template rendering, **When** the generator runs, **Then** diagnostic ASG0002 is emitted with the exception message and stack trace, pointing to the actor class

---

### User Story 3 - Thread-Safe Parallel Generation (Priority: P2)

A build system compiles multiple projects in parallel, each using the ActorSrcGen generator. The generator must operate correctly without race conditions or shared state corruption, even when multiple instances run concurrently in the same process.

**Why this priority**: Roslyn may run generators in parallel or reuse instances. Thread safety ensures reliability in modern build environments and prevents intermittent build failures.

**Independent Test**: Run the generator on multiple unrelated actor classes simultaneously (simulated with parallel test execution). Verify no diagnostics are duplicated or lost, and all expected outputs are generated correctly.

**Acceptance Scenarios**:

1. **Given** multiple actor classes in separate files, **When** the generator processes them in parallel, **Then** each actor generates its own .generated.cs file without interference or missing outputs
2. **Given** shared diagnostic descriptors accessed from multiple threads, **When** diagnostics are reported concurrently, **Then** no race conditions occur and all diagnostics are reported correctly
3. **Given** the visitor pattern traversing syntax trees, **When** multiple visitors run in parallel, **Then** no shared mutable state causes incorrect results

---

### User Story 4 - Cancellation-Aware Generation (Priority: P2)

A developer cancels a build mid-execution (Ctrl+C or IDE stop button). The generator detects cancellation promptly, stops processing cleanly without reporting spurious errors, and doesn't leave the build in an inconsistent state.

**Why this priority**: Cancellation support improves developer experience and prevents wasted computation. While important for production quality, it's not blocking for basic functionality.

**Independent Test**: Simulate cancellation by passing a pre-canceled CancellationToken to the generator. Verify it exits quickly without throwing exceptions or reporting diagnostics as errors.

**Acceptance Scenarios**:

1. **Given** a CancellationToken that fires during generation, **When** the generator checks for cancellation, **Then** it throws OperationCanceledException and stops processing cleanly
2. **Given** cancellation during actor traversal, **When** the generator processes multiple actors, **Then** partial results are not emitted and no ASG0002 errors are reported
3. **Given** cancellation between actors, **When** the generator has emitted some files, **Then** processing stops without corrupting already-generated files

---

### User Story 5 - Comprehensive Automated Testing (Priority: P3)

A contributor wants to add new generator features or refactor existing code. A comprehensive test suite runs quickly, validates correctness, and catches regressions before code review.

**Why this priority**: Testing enables safe refactoring and feature development. While essential for maintainability, it can be built incrementally after core functionality is solid.

**Independent Test**: Run the full test suite (xUnit) covering all acceptance scenarios. All tests pass in under 30 seconds on a typical dev machine.

**Acceptance Scenarios**:

1. **Given** a test project with Roslyn-based generator tests, **When** a contributor runs tests locally, **Then** all tests complete in under 30 seconds with clear pass/fail indicators
2. **Given** snapshot tests for generated code, **When** a code change affects output, **Then** the test framework shows a clear diff of expected vs actual output
3. **Given** negative test cases (invalid inputs, edge cases), **When** tests run, **Then** they verify correct error handling without crashes

---

### Edge Cases

- What happens when an actor class has circular step dependencies (NextStep pointing back to earlier steps)?
- How does the generator handle actor classes in nested namespaces or with nested type declarations?
- What happens when attribute syntax is malformed (e.g., missing closing brackets)?
- How does the generator behave when compilation has pre-existing semantic errors unrelated to actors?
- What happens when multiple actors have the same name in different namespaces?
- How does the generator handle extremely large actor classes (100+ methods)?
- What happens when line endings are mixed (CRLF/LF) within the same file?
- How does the generator respond to concurrent modification of source files during generation?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Generator MUST detect classes decorated with [Actor] attribute using incremental pipeline with syntax predicate and semantic transform
- **FR-002**: Generator MUST validate that actors with multiple input types have disjoint type signatures
- **FR-003**: Generator MUST emit diagnostic ASG0001 for non-disjoint multiple input types, including actor name and conflicting types in the message
- **FR-004**: Generator MUST emit diagnostic ASG0002 for actors missing input types or when generation fails, including symbol name and error context
- **FR-005**: Generator MUST emit diagnostic ASG0003 for invalid ingest methods (non-static or incompatible return type), including method name and remediation
- **FR-006**: Generator MUST generate source files named `{ActorName}.generated.cs` with stable, deterministic names
- **FR-007**: Generator MUST use `SourceText.From(text, Encoding.UTF8)` for all generated content to ensure stable hashing
- **FR-008**: Generator MUST sort candidate symbols by fully-qualified name before generation to ensure deterministic ordering
- **FR-009**: Generator MUST sort actors within a symbol by name before emitting sources
- **FR-010**: Generator MUST call `ThrowIfCancellationRequested` in top-level pipeline and per-actor loops
- **FR-011**: Generator MUST catch `OperationCanceledException` and swallow it without reporting diagnostics
- **FR-012**: Generator MUST catch all other exceptions and report them as ASG0002 diagnostics with best-available source location
- **FR-013**: Generator MUST use static readonly `DiagnosticDescriptor` instances centralized in one location
- **FR-014**: Generator MUST include symbol source locations in diagnostics when available, falling back to `Location.None`
- **FR-015**: Generator MUST NOT use instance fields that capture context or mutable static state
- **FR-016**: Generator MUST refactor visitor to return a result object containing actors collection and collected diagnostics
- **FR-017**: Generator MUST normalize line endings in generated code to LF (`\n`) for cross-platform consistency
- **FR-018**: Test project MUST run on net8.0 and use xUnit for test framework
- **FR-019**: Tests MUST use Roslyn's `CSharpCompilation` and `CSharpGeneratorDriver` to validate generator behavior
- **FR-020**: Tests MUST include snapshot tests comparing generated file contents with expected outputs
- **FR-021**: Tests MUST verify all 10 acceptance test cases listed in user requirements

### Key Entities

- **SyntaxAndSymbol**: Compact record pairing `TypeDeclarationSyntax` with `INamedTypeSymbol` for incremental pipeline (FR-001)
- **ActorNode**: Domain model representing an actor class with its steps, input types, output types, and relationships (FR-016)
- **BlockNode**: Domain model representing a dataflow block (step) within an actor with its type, method, and handler body (FR-016)
- **VisitorResult**: Result object returned by `ActorVisitor` containing `ImmutableArray<ActorNode>` actors and `ImmutableArray<Diagnostic>` diagnostics (FR-016)
- **DiagnosticDescriptor**: Centralized static readonly descriptors for ASG0001, ASG0002, ASG0003, and future diagnostic codes (FR-013)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Generator produces identical output (byte-for-byte) when run twice on the same input without code changes
- **SC-002**: Generated file content hashes remain stable across multiple builds on different machines
- **SC-003**: All validation errors produce diagnostics with correct IDs (ASG####), clear messages, and accurate source locations
- **SC-004**: Generator completes cancellation within 100ms of CancellationToken firing during any phase of generation
- **SC-005**: Test suite completes in under 30 seconds with 100% of tests passing
- **SC-006**: Test coverage reaches 100% for critical paths: Generator.Initialize(), Generator.OnGenerate(), ActorGenerator public methods, ActorVisitor, diagnostic creation
- **SC-007**: Test coverage reaches minimum 85% overall for generator project
- **SC-008**: Generator handles 10+ actors in a single compilation without performance degradation (< 1 second total)
- **SC-009**: Zero race conditions or thread-safety issues detected when running generators in parallel
- **SC-010**: All 10 acceptance test cases pass consistently across 100+ test runs without flakiness

### Assumptions

- Roslyn's incremental generator infrastructure correctly caches and invalidates pipeline stages
- The existing TPL Dataflow template rendering is deterministic if provided sorted inputs
- xUnit test framework is acceptable for generator testing (standard for .NET)
- Build time impact of determinism checks (sorting, normalization) is negligible (<50ms per actor)
- Existing ActorVisitor can be refactored to return results without breaking template compatibility

### Constraints

- Generator must remain compatible with netstandard2.0 Roslyn APIs (no net8.0-only generator code)
- Tests can use net8.0 features and latest Roslyn testing APIs
- Generated code public API must not change (backward compatibility required)
- No new runtime dependencies added to ActorSrcGen.Abstractions package
- Template rendering logic (T4) should only be modified if necessary for determinism

### Dependencies

- Existing ActorSrcGen generator project structure and attribute definitions
- Roslyn Microsoft.CodeAnalysis.CSharp 4.6.0 (or compatible version)
- xUnit test framework for net8.0
- ActorSrcGen.Abstractions for test attribute stubs

### Out of Scope

- Rewriting T4 templates unless required for determinism
- Adding new actor features or changing generated code behavior
- Performance optimization beyond preventing O(n²) operations
- Migration of existing generated code in user projects
- IDE tooling, IntelliSense, or code completion features
