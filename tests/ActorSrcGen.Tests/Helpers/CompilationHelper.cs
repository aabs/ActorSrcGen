using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ActorSrcGen.Tests.Helpers;

public static class CompilationHelper
{
    public static CSharpCompilation CreateCompilation(string sourceCode)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(sourceCode, Encoding.UTF8), parseOptions);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ActorSrcGen.ActorAttribute).Assembly.Location)
        };

        return CSharpCompilation.Create(
            assemblyName: "ActorSrcGen.Tests.Compilation",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static GeneratorDriver CreateGeneratorDriver(CSharpCompilation compilation)
    {
        var generator = new Generator();
        var parseOptions = compilation.SyntaxTrees.First().Options as CSharpParseOptions;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ISourceGenerator[] { generator.AsSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver;
    }

    public static Dictionary<string, string> GetGeneratedOutput(GeneratorDriver driver)
    {
        var runResult = driver.GetRunResult();
        var output = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var result in runResult.Results)
        {
            foreach (var source in result.GeneratedSources)
            {
                output[source.HintName] = source.SourceText.ToString();
            }
        }

        return output;
    }
}
