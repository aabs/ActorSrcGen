# GitHub Copilot Instructions for ActorSrcGen

## Priority Guidelines

When generating code for this repository:

1. **Version Compatibility**: Strictly adhere to the exact C# language versions and .NET frameworks specified in each project
2. **Architecture Consistency**: Maintain the source generator architecture and dataflow-based design patterns established in the codebase
3. **Code Quality**: Prioritize maintainability, testability, and error handling consistency with existing patterns
4. **Codebase Patterns**: Follow established naming conventions, error handling patterns, and code organization strictly
5. **Context Files**: Reference the patterns and standards defined in this file and the codebase

## Technology Versions

### .NET Versions

The ActorSrcGen solution uses multiple .NET versions per project:

- **ActorSrcGen** (Source Generator): `.NET Standard 2.0` with C# `preview` language features
  - Target Framework: `netstandard2.0`
  - Language Version: `preview`
  - Nullable: `enable`
  - IsRoslynComponent: `true` (this is a source generator)
  - Dependencies: Microsoft.CodeAnalysis.CSharp 4.6.0, System.CodeDom 8.0.0

- **ActorSrcGen.Abstractions**: `.NET Standard 2.0`
  - Target Framework: `netstandard2.0`
  - Language Version: default (netstandard2.0)
  - Dependencies: Gridsum.DataflowEx 2.0.0, System.Collections.Immutable 7.0.0

- **ActorSrcGen.Playground**: `.NET 8.0`
  - Target Framework: `net8.0`
  - Language Version: `12.0`
  - Nullable: `enable`
  - Implicit Usings: `enable`
  - Dependencies: Gridsum.DataflowEx 2.0.0, Microsoft.Extensions.Configuration.*

- **Directory.Build.props** (Global Settings):
  - Global Language Version: `12.0`
  - Global Target Framework: `net8.0`
  - Global Nullable: `enable`
  - Version: `2.3.6`

### C# Language Features

- **ActorSrcGen generator project**: Use C# preview features for maximum flexibility in generating code
- **Abstractions project**: Avoid modern C# features; ensure netstandard2.0 compatibility
- **Playground project**: Use C# 12.0 features freely (records, primary constructors, nullable reference types, etc.)
- **Never use features beyond the project's configured language version**

## Project Architecture

### Core Concepts

ActorSrcGen is a C# Incremental Source Generator that converts classes decorated with `[Actor]` into TPL Dataflow-compatible pipelines. The architecture consists of:

1. **Generator** (ActorSrcGen project): Implements `IIncrementalGenerator` and orchestrates source generation
2. **Abstractions** (ActorSrcGen.Abstractions): Attribute definitions and IActor interface
3. **Model**: Domain objects representing the actor structure (ActorNode, BlockNode, ActorVisitor)
4. **Helpers**: Roslyn extension methods and code generation utilities
5. **Templates**: Text templates (T4) for code generation

### Architectural Patterns

**Do:**
- Use incremental generation patterns with `IncrementalGeneratorInitializationContext`
- Apply the visitor pattern (ActorVisitor) to analyze source code structure
- Create separate concerns: syntax analysis → semantic analysis → code generation
- Use immutable collections (ImmutableArray) in generators
- Leverage Roslyn's syntax and semantic APIs for robust analysis
- Return `null` from transform functions to filter invalid symbols
- Report diagnostics via `SourceProductionContext` for user-facing errors

**Do Not:**
- Avoid complex mutable state in generators; use immutable collections
- Never perform expensive operations outside of generation context
- Don't generate code without first validating input semantics
- Avoid tight coupling between generation phases

## Code Organization & Naming Conventions

### File Organization

```
ActorSrcGen/
├── Generators/          # Source generation logic
│   ├── Generator.cs     # Main IIncrementalGenerator implementation
│   ├── ActorGenerator.cs # Code emission logic
│   └── GenerationContext.cs
├── Helpers/             # Roslyn extensions and utilities
│   ├── RoslynExtensions.cs  # INamedTypeSymbol, IMethodSymbol extensions
│   ├── DomainRoslynExtensions.cs # Domain-specific Roslyn helpers
│   ├── TypeHelpers.cs   # Type name rendering and inspection
│   ├── SyntaxAndSymbol.cs # Paired syntax/symbol record
│   └── actor.template.cs # T4 template output
├── Model/               # Domain model
│   ├── ActorVisitor.cs  # Visitor for analyzing actor structure
│   ├── BlockGraph.cs    # Block/node relationships
│   └── [domain objects]
└── Templates/           # T4 templates
    ├── Actor.tt         # Template source
    └── Actor.cs         # Generated output
```

### Naming Conventions

**Classes & Records:**
- Use PascalCase: `ActorNode`, `BlockNode`, `RoslynExtensions`
- Attribute classes end in `Attribute`: `ActorAttribute`, `FirstStepAttribute`
- Generator classes end in `Generator`: `ActorGenerator`, `Generator`
- Context/helper classes use domain terminology: `ActorGenerationContext`, `GenerationContext`

**Methods & Properties:**
- Use PascalCase: `VisitActor()`, `VisitMethod()`, `GenerateBlockDeclaration()`
- Extension methods: use domain-appropriate names: `MatchAttribute()`, `GetBlockAttr()`, `ToSymbol()`
- Private helper methods use verb-noun pattern: `CreateActionNode()`, `ChooseBlockType()`, `GenerateBlockLinkage()`

**Variables & Parameters:**
- Use camelCase: `actor`, `blockNode`, `inputTypeName`, `nextSteps`
- Single letter for loop variables only in short loops: `m`, `a`, `x`
- Prefix private fields with underscore: `_actorStack`, `_blockStack`
- Prefix internal constants with no prefix: `MethodTargetAttribute = "DataflowBlockAttribute"`

**Diagnostics:**
- Error IDs use pattern `ASG####` (e.g., `ASG0001`, `ASG0002`)
- Diagnostic titles are descriptive: `"Actor must have at least one input type"`

## Error Handling & Validation

### Validation Pattern

Follow the pattern established in `ActorGenerator.GenerateActor()`:

```csharp
// validation: check for condition
if (!actor.HasAnyInputTypes)
{
    var dd = new DiagnosticDescriptor(
        "ASG0002",
        "Actor must have at least one input type",
        "Actor {0} does not have any input types defined. At least one entry method is required.",
        "types",
        DiagnosticSeverity.Error,
        true);
    Diagnostic diagnostic = Diagnostic.Create(dd, Location.None, actor.Name);
    context.ReportDiagnostic(diagnostic);
    hasValidationErrors = true;
}

// Return early if there were any validation errors
if (hasValidationErrors)
{
    return;
}
```

**Key patterns:**
- Collect all validation errors before returning/stopping generation
- Create `DiagnosticDescriptor` with: ID, title, message format, category, severity, isEnabledByDefault
- Use `Diagnostic.Create()` to instantiate, providing location and message arguments
- Report diagnostics via `context.ReportDiagnostic()`
- Return early or log errors to prevent generating invalid code

### Error Handling in Generator Context

In the main Generator class:

```csharp
try
{
    ActorVisitor v = new();
    v.VisitActor(input);
    foreach (var actor in v.Actors)
    {
        var source = new Actor(actor).TransformText();
        context.AddSource($"{actor.Name}.generated.cs", source);
    }
}
catch (Exception e)
{
    var descriptor = new DiagnosticDescriptor(
        "ASG0002",
        "Error generating source",
        "Error while generating source for '{0}': {1}",
        "SourceGenerator",
        DiagnosticSeverity.Error,
        true);
    var diagnostic = Diagnostic.Create(descriptor, input.Syntax.GetLocation(), input.Symbol.Name, e.ToString());
    context.ReportDiagnostic(diagnostic);
}
```

## Roslyn Extension Patterns

### Attribute Matching

```csharp
public static bool MatchAttribute(
    this SyntaxNode node,
    string attributeName,
    CancellationToken cancellationToken)
{
    if (node is TypeDeclarationSyntax type)
        return type.MatchAttribute(attributeName, cancellationToken);
    return false;
}

private static (string, string) RefineAttributeNames(string attributeName)
{
    // Handle both "MyAttribute" and "My" forms
    // Handle namespaced attributes
}
```

**Patterns:**
- Use extension methods on Roslyn types (INamedTypeSymbol, IMethodSymbol, SyntaxNode)
- Handle both with/without "Attribute" suffix: "Actor" and "ActorAttribute" both match
- Use cancellation tokens in traversal operations
- Return tuple for paired return values: `(string, string)`

### Type Name Rendering

Always use the `RenderTypename()` extension method for type names:

```csharp
// Render with optional Task unwrapping
string inputTypeName = method.GetInputTypeName();
string outputType = method.ReturnType.RenderTypename(stripTask: true);

// Use MinimallyQualifiedFormat for consistent output
return ts.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
```

## Code Generation Patterns

### StringBuilder for Code Emission

Use `StringBuilder` with `AppendLine()` and interpolated strings:

```csharp
var builder = ctx.Builder;
builder.AppendLine($$"""
    public {{ctx.Name}}() : base(DataflowOptions.Default)
    {
    """);

// For complex blocks, use raw string literals with $$ prefix
builder.AppendLine($$"""
    {{blockName}} = new {{blockTypeName}}({{step.HandlerBody}},
        new ExecutionDataflowBlockOptions() {
            BoundedCapacity = {{capacity}},
            MaxDegreeOfParallelism = {{maxParallelism}}
    });
""");
```

**Patterns:**
- Use triple-quoted raw strings (`"""..."""`) for multi-line code
- Use `$$` prefix for interpolated raw strings
- Use `AppendLine()` for line-by-line generation
- Maintain consistent indentation (4 spaces per level)

### Handler Body Generation

Generated handler bodies follow established patterns for different block types:

```csharp
// Action block (no return)
step.HandlerBody = $$"""
    ({{stepInputType}} x) => {
        try
        {
            {{step.Method.Name}}(x);
        }catch{}
    }
""";

// Transform block (sync)
step.HandlerBody = $$"""
    ({{stepInputType}} x) => {
        try
        {
            return {{ms.Name}}(x);
        }
        catch
        {
            return default;
        }
    }
""";

// Transform block (async)
step.HandlerBody = $$"""
    {{asyncer}} ({{stepInputType}} x) => {
        var result = new List<{{stepResultType}}>();
        try
        {
            var newValue = {{awaiter}} {{ms.Name}}(x);
            result.Add(newValue);
        }catch{}
        return result;
    }
""";
```

**Key patterns:**
- All handlers are wrapped in try-catch blocks
- Sync actions suppress exceptions: `catch{}`
- Transform blocks return `default` on error
- TransformMany/TransformManyBlock return `List<T>` wrapped results
- Use `asyncer` and `awaiter` variables for conditional async/await

## Testing

### Test Structure

Follow the pattern in `GeneratorSmokeTests.cs`:

```csharp
private static (GeneratorDriverRunResult runResult, ImmutableArray<GeneratedSourceResult> sources, ImmutableArray<Diagnostic> diagnostics)
    Run(string source)
{
    // Setup: Create syntax trees for attributes and test code
    var syntaxTrees = new[]
    {
        CSharpSyntaxTree.ParseText(SourceText.From(attrs, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Preview)),
        CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Preview)),
    };

    // Setup: Add required metadata references
    var references = new[] { /* object, Enumerable, etc. */ };

    // Create compilation and driver
    var compilation = CSharpCompilation.Create(
        assemblyName: "Tests",
        syntaxTrees: syntaxTrees,
        references: references,
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    // Run generator
    var generator = new Generator();
    var driver = CSharpGeneratorDriver.Create(generator);
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var genDiagnostics);

    var runResult = driver.GetRunResult();
    return (runResult, sources, diagnostics);
}

[Fact]
public void Generates_no_crash_for_empty_actor()
{
    var input = """
    using ActorSrcGen;
    [Actor]
    public partial class MyActor { }
    """;

    var (_, sources, diagnostics) = Run(input);

    Assert.True(diagnostics.Length >= 0);
    Assert.NotNull(sources);
}
```

**Test patterns:**
- Use xUnit: `[Fact]` attributes and `Assert.*` methods
- Create a helper `Run()` method that encapsulates compilation setup
- Test that generator handles both valid and edge-case inputs
- Verify that diagnostics are reported appropriately
- Use raw string literals for test input code

## Communication Patterns

### Diagnostic Messages

Format diagnostic messages for clarity:

```csharp
// Good: includes variable values and context
"Error while generating source for '{0}': {1}"  // actor name, exception
"Actor {0} does not have any input types defined. At least one entry method is required."

// Format with Diagnostic.Create()
Diagnostic.Create(descriptor, location, arg1, arg2, ...)
```

## Common Extensions to Know

Commonly used Roslyn extensions in the codebase:

```csharp
// Type checking
public static bool IsCollection(this ITypeSymbol ts)
    => ts.Name is "List" or "IEnumerable";

// Method analysis
public static bool ReturnTypeIsCollection(this IMethodSymbol method)
public static bool IsAsynchronous(this IMethodSymbol method)
public static int GetMaxDegreeOfParallelism(this IMethodSymbol method)
public static int GetMaxBufferSize(this IMethodSymbol method)
public static string? GetInputTypeName(this IMethodSymbol method)

// Type utilities
public static ITypeSymbol? GetFirstTypeParameter(this ITypeSymbol type)
public static string GetReturnTypeCollectionType(this IMethodSymbol method)

// Attribute access
public static AttributeData GetBlockAttr(this IMethodSymbol ms)
public static AttributeData GetIngestAttr(this IMethodSymbol ms)
public static IEnumerable<AttributeData> GetNextStepAttrs(this IMethodSymbol ms)
public static bool IsStartStep(this IMethodSymbol method)
public static bool IsEndStep(this IMethodSymbol method)
```

## When Creating New Code

### Source Generators

1. **Implement `IIncrementalGenerator`** with `Initialize()` method
2. **Use `SyntaxProvider.CreateSyntaxProvider()`** with predicate and transform
3. **Apply filtering** with `.Where()` to exclude null results
4. **Combine with compilation** using `.Combine()`
5. **Register output** with `context.RegisterSourceOutput()`
6. **Wrap generation in try-catch** to report diagnostic errors

### Domain Model Classes

1. **Use records or classes** to represent AST concepts (ActorNode, BlockNode)
2. **Include collections** of child nodes when representing hierarchies
3. **Use enums** for node types (NodeType.Action, NodeType.Transform, etc.)
4. **Initialize in visitor** during syntax tree traversal

### Helper Extensions

1. **Extend Roslyn types** (INamedTypeSymbol, IMethodSymbol, etc.)
2. **Add domain-specific methods** for actor/block analysis
3. **Handle edge cases** (null types, missing attributes)
4. **Return appropriate defaults** (empty collections, null, false)

## Code Quality Standards

### Maintainability

- Write self-documenting code with clear variable and method names
- Follow the naming conventions strictly
- Keep functions focused on single responsibility
- Use comments for non-obvious logic (especially Roslyn API usage)

### Performance

- Use immutable collections in generators (ImmutableArray, ImmutableList)
- Avoid unnecessary allocations in hot paths
- Cache attribute lookups when used multiple times
- Use StringBuilder for string concatenation in code generation

### Security

- Validate all user input before processing
- Report diagnostic errors for invalid configurations
- Sanitize generated code to prevent injection
- Handle exceptions gracefully without exposing internal details

### Testability

- Separate concerns: syntax analysis, semantic analysis, code generation
- Provide extension methods for Roslyn APIs
- Create testable helper functions with pure logic
- Use dependency injection for context (ActorGenerationContext)

## Project-Specific Guidance

- **Always scan similar files** for patterns before generating new code
- **Respect `netstandard2.0` constraints** in Abstractions project
- **Use preview language features** judiciously in generator project
- **Test generated code** by ensuring it compiles and handles edge cases
- **Document attribute behavior** in attribute classes and test files
- **Match spacing and style** of existing code exactly
- **Use DataflowEx base classes** (Dataflow, Dataflow<TIn>, Dataflow<TIn, TOut>) when generating code
- **Reference Gridsum.DataflowEx** for base class functionality

## References

Key files to understand the architecture:
- [Generator.cs](../../ActorSrcGen/Generators/Generator.cs) - Main incremental generator
- [ActorGenerator.cs](../../ActorSrcGen/Generators/ActorGenerator.cs) - Code emission
- [ActorVisitor.cs](../../ActorSrcGen/Model/ActorVisitor.cs) - AST traversal
- [RoslynExtensions.cs](../../ActorSrcGen/Helpers/RoslynExtensions.cs) - Roslyn utilities
- [TypeHelpers.cs](../../ActorSrcGen/Helpers/TypeHelpers.cs) - Type name rendering
