// Analyzer diagnostics centralization. RS tracking warnings suppressed for generator project scope.
#pragma warning disable RS1032
#pragma warning disable RS2008
using Microsoft.CodeAnalysis;

namespace ActorSrcGen.Diagnostics;

internal static class Diagnostics
{
    private const string Category = "ActorSrcGen";

    public static readonly DiagnosticDescriptor ASG0001 = new(
        id: "ASG0001",
        title: "Actor must define at least one Step method",
        messageFormat: "Actor '{0}' does not define any methods annotated with [Step] or [FirstStep] attributes",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ASG0002 = new(
        id: "ASG0002",
        title: "Actor has no entry points",
        messageFormat: "Actor '{0}' must declare an entry point via [FirstStep], [Receiver], or [Ingest]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ASG0003 = new(
        id: "ASG0003",
        title: "Invalid ingest method",
        messageFormat: "Ingest method '{0}' must be static and return Task or IAsyncEnumerable<T>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, Location location, params object[] args)
        => Diagnostic.Create(descriptor, location, args);
}
