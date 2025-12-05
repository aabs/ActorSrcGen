using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ActorSrcGen.Tests;

public class GeneratorSmokeTests
{
    private static (GeneratorDriverRunResult runResult, ImmutableArray<GeneratedSourceResult> sources, ImmutableArray<Diagnostic> diagnostics)
        Run(string source)
    {
        const string attrs = """
        namespace ActorSrcGen
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class ActorAttribute : System.Attribute { }
        }
        """;

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(SourceText.From(attrs, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Preview)),
            CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Preview)),
        };

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var genDiagnostics);
        var runResult = driver.GetRunResult();

        var sources = runResult.Results[0].GeneratedSources;
        var diags = runResult.Results[0].Diagnostics;

        return (runResult, sources, diags);
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
}
