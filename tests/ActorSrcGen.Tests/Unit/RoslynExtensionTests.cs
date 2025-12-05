using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ActorSrcGen;
using ActorSrcGen.Helpers;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class RoslynExtensionTests
{
    private static (Compilation Compilation, SyntaxTree Tree, SemanticModel Model) BuildCompilation(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        return (compilation, tree, model);
    }

    [Fact]
    public void MatchAttribute_MatchesShortAndFullNames()
    {
        const string source = """
using System;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CustomAttribute : Attribute {}

[Custom]
public partial class Sample {}
""";

        var (_, tree, _) = BuildCompilation(source);
        var type = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First(t => t.Identifier.Text == "Sample");

        Assert.True(type.MatchAttribute("CustomAttribute", CancellationToken.None));
        Assert.True(type.MatchAttribute("Custom", CancellationToken.None));
    }

    [Fact]
    public void MatchAttribute_CancellationRequested_ReturnsFalse()
    {
        const string source = """
using System;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DemoAttribute : Attribute {}

[Demo]
public partial class Cancelled {}
""";

        var (_, tree, _) = BuildCompilation(source);
        var type = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(type.MatchAttribute("Demo", cts.Token));
    }

    [Fact]
    public void GetUsing_ReturnsTopLevelUsings()
    {
        const string source = """
using System;
using System.Collections.Generic;

namespace Sample;

public partial class Foo {}
""";

        var (compilation, _, _) = BuildCompilation(source);
        var root = (CompilationUnitSyntax)compilation.SyntaxTrees.Single().GetRoot();
        var usings = root.GetUsing().ToArray();

        Assert.Contains(usings, u => u.Contains("using System;", StringComparison.Ordinal));
        Assert.Contains(usings, u => u.Contains("using System.Collections.Generic;", StringComparison.Ordinal));
    }

    [Fact]
    public void GetUsingWithinNamespace_ReturnsInnerUsings()
    {
        const string source = """
using System;

namespace Sample;

using System.Threading;

public partial class Inner {}
""";

        var (_, tree, _) = BuildCompilation(source);
        var type = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();

        var innerUsings = type.GetUsingWithinNamespace();

        Assert.Single(innerUsings);
        Assert.Equal("using System.Threading;", innerUsings[0].Trim());
    }

    [Fact]
    public void TryGetValue_ExtractsConstructorAndNamedArguments()
    {
        const string source = """
using System;

[AttributeUsage(AttributeTargets.All)]
public sealed class SampleAttribute : Attribute
{
    public SampleAttribute(string name) => Name = name;
    public string Name { get; }
    public int Id { get; set; }
}

[Sample("ctor", Id = 3)]
public partial class Target
{
    [Sample("method", Id = 5)]
    public void Run() {}
}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "Target");
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;
        var method = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Run");

        Assert.True(classSymbol.TryGetValue("SampleAttribute", "name", out string name));
        Assert.Equal("ctor", name);
        Assert.True(method.TryGetValue("SampleAttribute", "Id", out int id));
        Assert.Equal(5, id);
    }

    [Fact]
    public void GetNestedBaseTypesAndSelf_ReturnsHierarchyWithoutObject()
    {
        const string source = """
public class Base {}
public class Mid : Base {}
public class Leaf : Mid {}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var leafSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "Leaf");
        var leafSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(leafSyntax)!;

        var names = leafSymbol.GetNestedBaseTypesAndSelf().Select(t => t.Name).ToArray();

        Assert.Equal(new[] { "Leaf", "Mid", "Base" }, names);
    }

    [Fact]
    public void GetArg_ReturnsConstructorAndNamedFallback()
    {
        const string source = """
using System;
using ActorSrcGen;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NamedAttr : Attribute
{
    public int Count { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class CtorAttr : Attribute
{
    public CtorAttr(int value) => Value = value;
    public int Value { get; }
}

public partial class Container
{
    [CtorAttr(5)]
    [NamedAttr(Count = 7)]
    public int DoWork() => 1;
}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "Container");
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;
        var method = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "DoWork");

        var ctorAttr = method.GetAttributes().First(a => a.AttributeClass!.Name == "CtorAttr");
        Assert.Equal(5, ctorAttr.GetArg<int>(0));

        var namedAttr = method.GetAttributes().First(a => a.AttributeClass!.Name == "NamedAttr");
        Assert.Equal(7, namedAttr.GetArg<int>(0));
    }

    [Fact]
    public void GetNextStepAttrs_FindsAllAttributes()
    {
        const string source = """
using ActorSrcGen;

public partial class Chain
{
    [NextStep("B")]
    [NextStep("C")]
    public int A(int x) => x;
}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;
        var method = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "A");

        var attrs = method.GetNextStepAttrs().ToArray();

        Assert.Equal(2, attrs.Length);
    }

    [Fact]
    public void BlockAttributeHelpers_DetectStepMarkers()
    {
        const string source = """
using ActorSrcGen;

public partial class Pipeline
{
    [FirstStep("input")]
    public int Start(string input) => input.Length;

    [LastStep]
    public int End(int value) => value;

    [Ingest]
    public static Task<int> IngestAsync() => Task.FromResult(1);
}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;

        var start = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var end = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "End");
        var ingest = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "IngestAsync");

        Assert.NotNull(start.GetBlockAttr());
        Assert.True(start.IsStartStep());
        Assert.NotNull(end.GetBlockAttr());
        Assert.True(end.IsEndStep());
        Assert.NotNull(ingest.GetIngestAttr());
    }

    [Fact]
    public void AppendHeader_WritesPragmasAndUsings()
    {
        const string source = """
using System;
using System.Collections.Generic;

namespace Demo;

using System.Threading.Tasks;

public partial class HeaderTarget {}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var typeSyntax = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        var typeSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(typeSyntax)!;

        var builder = new StringBuilder();
        builder.AppendHeader(typeSyntax, typeSymbol);
        var header = builder.ToString();

        Assert.Contains("// Generated on", header, StringComparison.Ordinal);
        Assert.Contains("#pragma warning disable CS8625", header, StringComparison.Ordinal);
        Assert.Contains("using System;", header, StringComparison.Ordinal);
        Assert.Contains("using System.Collections.Generic;", header, StringComparison.Ordinal);
        Assert.Contains("namespace Demo;", header, StringComparison.Ordinal);
        Assert.Contains("using System.Threading.Tasks;", header, StringComparison.Ordinal);
    }
}