using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class GeneratorTests
{
    [Fact]
    public void OnGenerate_WhenVisitorThrows_ReportsDiagnostic()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial class Faulty
{
    [FirstStep, NextStep("Other")]
    public string Start(string input) => input;
}
""";

        var compilation = CompilationHelper.CreateCompilation(source);
        var (context, diagnosticBag) = CreateContext(compilation);
        var tree = compilation.SyntaxTrees.Single();
        var semanticModel = compilation.GetSemanticModel(tree);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var symbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classSyntax)!;

        var generator = new ActorSrcGen.Generator();

        var onGenerate = typeof(ActorSrcGen.Generator)
            .GetMethod("OnGenerate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onGenerate);

        var invalidInput = new SyntaxAndSymbol(classSyntax, symbol!, null!);
        var invocation = Record.Exception(() =>
            onGenerate!.Invoke(generator, new object?[] { context, compilation, invalidInput }));

        Assert.Null(invocation);
        var diagnostics = ExtractDiagnostics(diagnosticBag);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ASG0002", diagnostic.Id);
    }

    [Fact]
    public void Initialize_FiltersNonClassActors()
    {
        const string source = """
using ActorSrcGen;

[Actor]
public partial struct NotAClass
{
}
""";

        var compilation = CompilationHelper.CreateCompilation(source);
        var driver = CompilationHelper.CreateGeneratorDriver(compilation);
        var runResult = driver.GetRunResult();

        Assert.All(runResult.Results, result => Assert.Empty(result.GeneratedSources));
    }

    [Fact]
    public void ToGenerationInput_ReturnsNull_WhenSymbolUnavailable()
    {
        var actorDeclaration = SyntaxFactory.ParseCompilationUnit("""
using ActorSrcGen;

[Actor]
public partial class MissingSemanticModel { }
""")
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();

        var helperAssembly = typeof(CSharpSyntaxTree).Assembly;
        var helperType = helperAssembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxHelper")!;
        var helperInstance = helperType.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var lazySemanticModel = Activator.CreateInstance(typeof(Lazy<>).MakeGenericType(typeof(SemanticModel)), (Func<SemanticModel>)(() => null!))!;

        var gscType = typeof(GeneratorSyntaxContext);
        var ctor = gscType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var context = ctor.Invoke(new[] { actorDeclaration, lazySemanticModel, helperInstance });

        var toGenerationInput = typeof(ActorSrcGen.Generator)
            .GetMethod("<Initialize>g__ToGenerationInput|2_3", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(toGenerationInput);

        var result = toGenerationInput!.Invoke(null, new[] { context });

        Assert.Null(result);
    }

    private static (SourceProductionContext Context, object DiagnosticBag) CreateContext(Compilation compilation)
    {
        var assembly = typeof(SourceProductionContext).Assembly;
        var additionalType = assembly.GetType("Microsoft.CodeAnalysis.AdditionalSourcesCollection")!;
        var diagnosticBagType = assembly.GetType("Microsoft.CodeAnalysis.DiagnosticBag")
            ?? assembly.GetTypes().First(t => t.Name == "DiagnosticBag");

        var additionalSources = Activator.CreateInstance(
            additionalType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { ".cs" },
            culture: null);
        var getInstance = diagnosticBagType.GetMethod(
            "GetInstance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var diagnosticBag = getInstance.Invoke(null, null)!;
        Assert.NotNull(diagnosticBag);

        var ctor = typeof(SourceProductionContext)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();

        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType == additionalType)
            {
                args[i] = additionalSources;
                continue;
            }

            if (parameterType == diagnosticBagType)
            {
                args[i] = diagnosticBag;
                continue;
            }

            if (parameterType == typeof(Compilation))
            {
                args[i] = compilation;
                continue;
            }

            if (parameterType == typeof(CancellationToken))
            {
                args[i] = CancellationToken.None;
                continue;
            }

            throw new InvalidOperationException($"Unknown SourceProductionContext parameter: {parameterType}");
        }

        var context = (SourceProductionContext)ctor.Invoke(args);

        return (context, diagnosticBag);
    }

    private static IReadOnlyList<Diagnostic> ExtractDiagnostics(object diagnosticBag)
    {
        var bagType = diagnosticBag.GetType();
        var toReadOnly = bagType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name == "ToReadOnly" && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 0)
            .MakeGenericMethod(typeof(Diagnostic));
        var diagnostics = (ImmutableArray<Diagnostic>)toReadOnly.Invoke(diagnosticBag, Array.Empty<object>())!;
        return diagnostics.ToArray();
    }

}
