using System.Collections.Generic;
using System.Collections.Immutable;
using ActorSrcGen;
using ActorSrcGen.Helpers;
using ActorSrcGen.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Tests.Unit;

public class TypeHelperTests
{
    private static (CSharpCompilation Compilation, SyntaxTree Tree, SemanticModel Model) BuildCompilation(string source)
    {
        var compilation = CompilationHelper.CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        return (compilation, tree, model);
    }

    [Fact]
    public void RenderTypename_NullSymbol_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TypeHelpers.RenderTypename(ts: null));
    }

    [Fact]
    public void RenderTypename_StripsTaskWhenRequested()
    {
        const string source = """
using System.Threading.Tasks;

public partial class Demo
{
    public Task<int> Run() => Task.FromResult(1);
}
""";

        var (_, tree, model) = BuildCompilation(source);
        var method = GetMethodSymbol(tree, model, "Run");

        Assert.Equal("int", method.ReturnType.RenderTypename(stripTask: true));
    }

    [Fact]
    public void RenderTypename_StripsCollectionWhenRequested()
    {
        const string source = """
using System.Collections.Generic;

public partial class Demo
{
    public List<string> Run() => new();
}
""";

        var (_, tree, model) = BuildCompilation(source);
        var method = GetMethodSymbol(tree, model, "Run");

        Assert.Equal("string", method.ReturnType.RenderTypename(stripCollection: true));
    }

    [Fact]
    public void RenderTypename_StripsTaskAndCollection()
    {
        const string source = """
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Demo
{
    public Task<List<int>> Run() => Task.FromResult(new List<int>());
}
""";

        var (_, tree, model) = BuildCompilation(source);
        var method = GetMethodSymbol(tree, model, "Run");

        Assert.Equal("int", method.ReturnType.RenderTypename(stripTask: true, stripCollection: true));
    }

    [Fact]
    public void RenderTypename_FromGenericSyntax_ResolvesSymbol()
    {
        const string source = """
using System.Threading.Tasks;

public partial class Demo
{
    public Task<string> Run() => Task.FromResult("x");
}
""";

        var (compilation, tree, _) = BuildCompilation(source);
        var genericName = tree.GetRoot().DescendantNodes().OfType<GenericNameSyntax>().First();

        Assert.Equal("string", genericName.RenderTypename(compilation, stripTask: true));
    }

    [Fact]
    public void IsCollection_RecognizesImmutableCollections()
    {
        const string source = """
using System.Collections.Immutable;

public partial class Demo
{
    public ImmutableArray<int> Items => ImmutableArray<int>.Empty;
}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var property = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().First();
        var symbol = (IPropertySymbol)model.GetDeclaredSymbol(property)!;

        Assert.True(symbol.Type.IsCollection());
    }

    [Fact]
    public void HasMultipleOnwardSteps_DetectsMultipleTargets()
    {
        const string source = """
using ActorSrcGen;

public partial class Graph
{
    [FirstStep("input")]
    public int Start(int input) => input;

    [Step]
    public int NextA(int value) => value + 1;

    [Step]
    public int NextB(int value) => value + 2;
}
""";

        var (compilation, tree, model) = BuildCompilation(source);
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;

        var start = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Start");
        var nextA = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "NextA");
        var nextB = classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "NextB");

        var dependencyGraph = new Dictionary<IMethodSymbol, IReadOnlyList<IMethodSymbol>>(SymbolEqualityComparer.Default)
        {
            [start] = new List<IMethodSymbol> { nextA, nextB }
        };

        var ctx = new GenerationContext(new SyntaxAndSymbol(classSyntax, classSymbol, model), new[] { start }, new[] { nextA }, dependencyGraph);

        Assert.True(start.HasMultipleOnwardSteps(ctx));
    }

    [Fact]
    public void GetFirstTypeParameter_ReturnsFirstGenericArgument()
    {
        const string source = """
using System.Collections.Generic;

public partial class Demo
{
    public Dictionary<string, int> Build() => new();
}
""";

        var (_, tree, model) = BuildCompilation(source);
        var method = GetMethodSymbol(tree, model, "Build");

        var first = method.ReturnType.GetFirstTypeParameter();

        Assert.Equal("String", first?.Name);
    }

    [Fact]
    public void AsTypeArgumentList_ConstructsList()
    {
        var compilation = CompilationHelper.CreateCompilation("class Demo {}");
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);

        var typeArgs = ImmutableArray.Create<ITypeSymbol>(intType, stringType);
        var list = TypeHelpers.AsTypeArgumentList(typeArgs);

        Assert.Equal("<int,string>", list.ToString());
    }

    [Fact]
    public void ReturnTypeIsCollection_HandlesTaskWrappedCollections()
    {
        const string source = """
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Demo
{
    public List<int> Direct() => new();
    public Task<List<int>> Async() => Task.FromResult(new List<int>());
}
""";

        var (_, tree, model) = BuildCompilation(source);
        var direct = GetMethodSymbol(tree, model, "Direct");
        var async = GetMethodSymbol(tree, model, "Async");

        Assert.True(direct.ReturnTypeIsCollection());
        Assert.True(async.ReturnTypeIsCollection());
    }

    [Fact]
    public void MethodAsyncHelpers_ReadAttributesAndReturnTypes()
    {
        const string source = """
using System.Collections.Generic;
using System.Threading.Tasks;
using ActorSrcGen;

public partial class Demo
{
    [Step(2, maxBufferSize: 8)]
    public List<int> Process(int value) => new();

    [FirstStep("input", maxDegreeOfParallelism: 3, maxBufferSize: 5)]
    public Task<int> Begin(string input) => Task.FromResult(input.Length);
}
""";

        var (_, tree, model) = BuildCompilation(source);
        var process = GetMethodSymbol(tree, model, "Process");
        var begin = GetMethodSymbol(tree, model, "Begin");

        Assert.False(process.IsAsynchronous());
        Assert.Equal(1, process.GetMaxDegreeOfParallelism());
        Assert.Equal(1, process.GetMaxBufferSize());
        Assert.Equal("int", process.GetReturnTypeCollectionType());
        Assert.Equal("int", process.GetInputTypeName());

        Assert.True(begin.IsAsynchronous());
        Assert.Equal(1, begin.GetMaxDegreeOfParallelism());
        Assert.Equal(1, begin.GetMaxBufferSize());
        Assert.Equal("int", begin.GetReturnTypeCollectionType());
        Assert.Equal("string", begin.GetInputTypeName());
    }

    private static IMethodSymbol GetMethodSymbol(SyntaxTree tree, SemanticModel model, string name)
    {
        var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classSyntax)!;
        return classSymbol.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == name);
    }
}