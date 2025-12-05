<!-- 
=============================================================================
SYNC IMPACT REPORT: Constitution v1.0.0 (Initial Ratification)
=============================================================================

VERSION CHANGE: N/A (Initial version: 1.0.0)
RATIFICATION DATE: 2025-12-05

NEW PRINCIPLES ADDED:
  ✅ I. Test-First Development (NON-NEGOTIABLE) - TDD mandatory with Red-Green-Refactor cycle
  ✅ II. Code Coverage & Reliability - 85% overall, 100% critical paths
  ✅ III. Reliability Through Immutability & Pure Functions - Functional programming principles
  ✅ IV. Diagnostic Consistency & Error Handling - Structured diagnostic reporting (ASG####)
  ✅ V. Idiomatic C# & Low Cyclomatic Complexity - Modern C#, CC ≤ 5 target
  ✅ VI. Testability & Async Code Discipline - Dependency injection, proper async patterns

NEW SECTIONS ADDED:
  ✅ Code Standards - Naming conventions, organization, documentation
  ✅ Critical Code Sections - Explicit 100% coverage requirements
  ✅ Development Workflow - Code review and validation gates
  ✅ Governance - Authority, amendment process, compliance verification

TEMPLATES COMPATIBILITY CHECK:
  ✅ plan-template.md - References "Constitution Check" gate; template is aligned
  ✅ spec-template.md - Supports TDD pattern; user stories map to test-first requirements
  ✅ tasks-template.md - Supports P1/P2/P3 prioritization and test-first approach
  ⚠️ MANUAL FOLLOW-UP: Review existing test coverage for critical paths; may need:
     - Coverage baseline measurement
     - GitHub Actions workflow for coverage reporting
     - Coverage exclusion documentation

RUNTIME GUIDANCE FILES:
  ✅ .github/copilot/copilot-instructions.md - Created with TDD and quality standards aligned
  ⚠️ README.md - May need updates referencing new constitution requirements
  ⚠️ Contributions.md - Should reference constitution for contributor guidelines

RECOMMENDED FOLLOW-UP ACTIONS:
  1. Run code coverage analysis on existing test suite to establish baseline
  2. Configure CI/CD to fail on coverage < 85% (with exception process)
  3. Audit critical paths (listed in Principle II) for test coverage gaps
  4. Update CONTRIBUTING.md to reference constitution sections
  5. Create issue template requiring coverage verification for PRs
  6. Document ASG#### diagnostic ID ranges for different error categories
  7. Establish cyclomatic complexity measurement (e.g., CodeMetrics, NDepend)

=============================================================================
END SYNC IMPACT REPORT
-->

# ActorSrcGen Constitution

## Core Principles

### I. Test-First Development (NON-NEGOTIABLE)

All code MUST follow Test-Driven Development (TDD) using the Red-Green-Refactor cycle strictly enforced:

1. **Red**: Write failing tests first that specify desired behavior before any implementation
2. **Green**: Write minimal code to make tests pass
3. **Refactor**: Improve code quality, performance, and maintainability without changing behavior

Tests are executable specifications. Test names MUST clearly describe the expected behavior. Unit tests MUST be isolated and deterministic.

### II. Code Coverage & Reliability

Minimum code coverage requirements MUST be enforced:

- **Overall project**: 85% minimum line and branch coverage
- **Critical paths**: 100% coverage required for:
  - Source generator logic (Generator.cs, ActorGenerator.cs)
  - Attribute and symbol validation
  - Error handling and diagnostic reporting
  - Type name rendering and Roslyn extensions
  - Core domain model transformations (ActorVisitor.cs, BlockGraph.cs)

Coverage MUST be verified in CI/CD pipelines. Uncovered code requires explicit justification and architectural review.

### III. Reliability Through Immutability & Pure Functions

Code MUST prioritize reliability through functional programming principles:

**Immutable Data Structures**:
- Use `record` types for all data transfer objects and domain models
- Use `ImmutableArray<T>`, `ImmutableList<T>`, `ImmutableDictionary<K,V>` from System.Collections.Immutable
- Avoid mutable collections in public APIs
- Declare `readonly` fields and properties unless mutation is unavoidable

**Pure Functions**:
- Functions MUST be free of side effects and non-deterministic behavior
- Pure functions enable testability, parallelization, and memoization
- Functions with side effects (I/O, logging, diagnostics) MUST be clearly marked and separated
- Return values MUST fully describe function outcomes; avoid out parameters

### IV. Diagnostic Consistency & Error Handling

All errors and exceptional conditions MUST be reported through diagnostic infrastructure:

**Diagnostic IDs**: Use format `ASG####` (e.g., ASG0001, ASG0002)

**Validation Pattern**:
- Validate all input semantics before code generation
- Collect ALL validation errors before reporting (do not fail-fast)
- Report errors via `SourceProductionContext.ReportDiagnostic()`
- Provide clear, actionable diagnostic messages with context

**Exception Handling**:
- Generators MUST wrap generation logic in try-catch blocks
- Generator exceptions MUST be reported as diagnostics (never thrown to build system)
- Validation exceptions MUST NOT occur; use diagnostics instead
- All exceptions in source generation MUST include full exception context in diagnostic message

### V. Idiomatic C# & Low Cyclomatic Complexity

Code MUST be idiomatic, modern C# with low complexity:

**Language Features**:
- Use C# 12+ features in net8.0 projects (records, primary constructors, collection expressions, etc.)
- Use nullable reference types (`#nullable enable`) throughout
- Use `nameof()` for string literals referring to symbols
- Use expression-bodied members where clarity is maintained
- Avoid C# 1.0-era patterns; use modern idiomatic approaches

**Cyclomatic Complexity**:
- Target cyclomatic complexity ≤ 5 for all methods (maximum: 8 with justification)
- Extract complex conditionals into named boolean methods or switch expressions
- Use pattern matching to replace nested if/switch chains
- Long methods MUST be broken into focused helper methods

**Example Low-Complexity Pattern**:
```csharp
// ❌ High complexity
bool ValidateActor(ActorNode actor)
{
    if (!actor.HasInputTypes) return false;
    if (actor.InputTypes.Count > 1)
    {
        if (!actor.AllInputTypesDisjoint) return false;
    }
    if (!actor.HasValidStepSequence) return false;
    // ... many more conditions
    return true;
}

// ✅ Low complexity with named conditions
bool ValidateActor(ActorNode actor) =>
    HasRequiredInputTypes(actor) &&
    HasDisjointInputTypes(actor) &&
    HasValidStepSequence(actor);

private bool HasRequiredInputTypes(ActorNode actor) =>
    actor.HasInputTypes;

private bool HasDisjointInputTypes(ActorNode actor) =>
    !actor.HasMultipleInputTypes || actor.AllInputTypesDisjoint;
```

### VI. Testability & Async Code Discipline

Code MUST be designed for testability with proper async patterns:

**Testability Requirements**:
- All public behavior MUST be testable without mocking
- Dependencies MUST be injected (constructor or method parameters)
- Code generation logic MUST be unit testable with compiled references
- Use SemanticModel and SyntaxProvider for deterministic analysis

**Async Best Practices**:
- Use `async/await` for all I/O operations (file read, HTTP requests, etc.)
- Async MUST NOT be used for CPU-bound operations; use `Task.Run()` only for existing blocking APIs
- Generator initialization (`Initialize()`) MUST NOT be async (by API constraint)
- Use `ConfigureAwait(false)` in library code to avoid context capture
- Properly propagate `CancellationToken` through async call chains

## Code Standards

### Naming Conventions

**Classes, Records, Methods**: PascalCase
- Attribute classes end in `Attribute`: `ActorAttribute`, `FirstStepAttribute`
- Generator classes end in `Generator`: `ActorGenerator`
- Exception classes end in `Exception`

**Properties, Variables**: camelCase for locals, PascalCase for properties
- Private fields prefixed with underscore: `_actorStack`, `_diagnostics`
- Constant names: UPPER_SNAKE_CASE or PascalCase (context-dependent)

**Diagnostic IDs**: `ASG` prefix followed by 4-digit number (e.g., `ASG0001`)

### Code Organization

- **One public type per file** (except extension methods)
- **Namespace per logical domain**: `ActorSrcGen.Generators`, `ActorSrcGen.Helpers`, `ActorSrcGen.Model`
- **Order within file**: using statements → namespace → type definition → nested types → fields → properties → constructors → methods
- **Related extension methods** may be grouped in shared files (e.g., `RoslynExtensions.cs`)

### Documentation

- **Public APIs**: XML documentation (`///`) required for all public types and members
- **Complex logic**: Code comments explaining the "why", not the "what"
- **Roslyn API usage**: Document non-obvious API patterns and gotchas
- **Test names**: Clearly describe the scenario and expected outcome (e.g., `GeneratesCorrectBlockDeclaration_WhenInputHasMultipleOutputTypes`)

## Critical Code Sections

The following sections MUST maintain 100% test coverage:

1. **Generator.Initialize()** and **Generator.OnGenerate()** - Orchestration logic
2. **ActorGenerator** - All public methods for code emission
3. **ActorVisitor.VisitActor()** and **VisitMethod()** - AST traversal
4. **All validation logic** - Input validation in ActorGenerator
5. **RoslynExtensions** - Symbol matching and attribute queries
6. **TypeHelpers.RenderTypename()** - Type name rendering (critical for correctness)
7. **BlockNode creation methods** - Handler body generation and node type selection
8. **All Diagnostic creation** - Error reporting infrastructure

## Development Workflow

### Code Review Requirements

Every PR MUST have:

1. **Coverage increase or maintenance**: Coverage MUST NOT decrease (except approved exceptions with written justification)
2. **Test verification**: All new code requires passing tests; refactoring requires passing existing tests
3. **Complexity review**: Methods > 20 lines or CC > 5 require explicit review
4. **Diagnostic accuracy**: Diagnostic messages MUST be clear, actionable, and include context

### Validation Gates

Before merging to main:

- [ ] All tests pass (unit, integration, smoke tests)
- [ ] Code coverage meets/exceeds minimums
- [ ] No compiler warnings
- [ ] Documentation updated for public API changes
- [ ] Diagnostics review: all ASG#### IDs validated, messages clear

## Governance

**Constitution Authority**: This constitution supersedes all other development practices and guidelines. When conflicts arise, this document is the authority.

**Amendments**: Changes to this constitution require:
1. Written proposal documenting the change rationale
2. Team consensus and approval
3. Version bump following semantic versioning (MAJOR.MINOR.PATCH)
4. Migration plan for existing non-compliant code (if applicable)

**Compliance Verification**:
- CI/CD pipelines MUST verify coverage and test compliance automatically
- Code reviews MUST reference constitution sections when requesting changes
- Architecture reviews MUST assess adherence to principles and patterns

**Deferred Items**: None currently pending.

**Version**: 1.0.0 | **Ratified**: 2025-12-05 | **Last Amended**: 2025-12-05
