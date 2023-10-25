#pragma warning disable HAA0301 // Closure Allocation Source
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
using System.Collections.Immutable;
using System;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;
using DataflowSrcGen.Helpers;

namespace DataflowSrcGen;

[Generator]
public partial class Generator : IIncrementalGenerator
{
    protected const string TargetAttribute = "ProxyAttribute";
    protected const string MethodTargetAttribute = "ProxyRouteAttribute";

    private static bool AttributePredicate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        return syntaxNode.MatchAttribute(TargetAttribute, cancellationToken);
    }

    #region Initialize

    /// <summary>
    /// Called to initialize the generator and register generation steps via callbacks
    /// on the <paramref name="context" />
    /// </summary>
    /// <param name="context">The <see cref="T:Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext" /> to register callbacks on</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<SyntaxAndSymbol> classDeclarations =
                context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: AttributePredicate,
                        transform: static (ctx, _) => ToGenerationInput(ctx))
                    .Where(static m => m is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxAndSymbol>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        // register a code generator for the triggers
        context.RegisterSourceOutput(compilationAndClasses, Generate);

        static SyntaxAndSymbol ToGenerationInput(GeneratorSyntaxContext context)
        {
            var declarationSyntax = (TypeDeclarationSyntax)context.Node;

            var symbol = context.SemanticModel.GetDeclaredSymbol(declarationSyntax);
            if (symbol is not INamedTypeSymbol namedSymbol) throw new NullReferenceException($"Code generated symbol of {nameof(declarationSyntax)} is missing");
            return new SyntaxAndSymbol(declarationSyntax, namedSymbol);
        }

        void Generate(
                       SourceProductionContext spc,
                       (Compilation compilation,
                       ImmutableArray<SyntaxAndSymbol> items) source)
        {
            var (compilation, items) = source;
            foreach (SyntaxAndSymbol item in items)
            {
                OnGenerate(spc, compilation, item);
            }
        }
    }

    #endregion // Initialize

    #region OnGenerate

    private void OnGenerate(
            SourceProductionContext context,
            Compilation compilation,
            SyntaxAndSymbol input)
    {
        INamedTypeSymbol typeSymbol = input.Symbol;
        TypeDeclarationSyntax syntax = input.Syntax;
        var cancellationToken = context.CancellationToken;
        if (cancellationToken.IsCancellationRequested)
            return;

        #region Error Handling

        if (!typeSymbol.TryGetValue(TargetAttribute, "template", out string clsTemplate))
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("HTTPGEN: 006", "template is missing",
                $"{typeSymbol.Name}, template is missing", "CustomErrorCategory",
                DiagnosticSeverity.Error, isEnabledByDefault: true),
                Location.None);
            context.ReportDiagnostic(diagnostic);
        }

        #endregion // Error Handling

        StringBuilder builder = new StringBuilder();
        builder.AppendHeader(syntax, typeSymbol);

        builder.AppendLine("using System.Net.Http.Json;");
        builder.AppendLine();

        string type = syntax.Keyword.Text;
        string name = typeSymbol.Name.Substring(1);

        var asm = GetType().Assembly.GetName();
        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"{asm.Name}\",\"{asm.Version}\")]");
        builder.AppendLine($"internal class {name}: {typeSymbol.Name}"); // REMOVE `I`
        builder.AppendLine("{");
        builder.AppendLine("\tprivate readonly HttpClient _httpClient;");
        builder.AppendLine();
        builder.AppendLine($"\tpublic {name}(HttpClient httpClient)");
        builder.AppendLine("\t{");
        builder.AppendLine("\t\t_httpClient = httpClient;");
        builder.AppendLine("\t}");


        foreach (var item in typeSymbol.GetMembers())
        {
            IMethodSymbol methodSymbol = item as IMethodSymbol;
            if (methodSymbol == null)
                continue;

            string mtdName = methodSymbol.Name;

            #region string mtdTemplate = ...

            string? mtdTemplate;
            methodSymbol.TryGetValue(MethodTargetAttribute, "template", out mtdTemplate);

            #endregion // string mtdTemplate = ...

            var symbolPrms = methodSymbol.Parameters;

            #region string verb = ...

            string verb = "Unknown";
            if (!methodSymbol.TryGetValue(TargetAttribute, "verb", out verb) || verb == "Unknown")
            {
                if (symbolPrms.Length == 0)
                    verb = "GET";
                else if (symbolPrms.Length == 1)
                    verb = "POST";
                else
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor("HTTPGEN: 002", "Failed to infer the verb",
                        $"{typeSymbol.Name}.{methodSymbol.Name},Failed to infer the verb ", "CustomErrorCategory",
                        DiagnosticSeverity.Error, isEnabledByDefault: true),
                        Location.None);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            }

            if ((verb == "POST" || verb == "PUT") && symbolPrms.Length != 1)
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor("HTTPGEN: 003", "POST/PUT expecting a single parameter",
                    $"{typeSymbol.Name}.{methodSymbol.Name}, POST/PUT expecting a single parameter", "CustomErrorCategory",
                    DiagnosticSeverity.Error, isEnabledByDefault: true),
                    Location.None);
                context.ReportDiagnostic(diagnostic);
                break;
            }

            if (verb == "Get" && symbolPrms.Length != 0)
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor("HTTPGEN: 004", "GET isn't expecting parameters",
                    $"{typeSymbol.Name}.{methodSymbol.Name}, GET isn't expecting parameters", "CustomErrorCategory",
                    DiagnosticSeverity.Error, isEnabledByDefault: true),
                    Location.None);
                context.ReportDiagnostic(diagnostic);
                break;
            }

            #endregion // string verb = ...

            var mtdSyntax = syntax.Members.Select(m => m as MethodDeclarationSyntax)
                                    .FirstOrDefault(m => m != null && m.Identifier.Text == mtdName) ?? throw new NullReferenceException(mtdName);
            var retType = mtdSyntax.ReturnType.ToString();
            var symbolRetTypeSymbol = methodSymbol.ReturnType as INamedTypeSymbol;
            var symbolRetType = symbolRetTypeSymbol?.TypeArguments.First().Name;

            #region Error Handling

            if (!retType.StartsWith("Task") && !retType.StartsWith("ValueTask"))
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor("HTTPGEN: 001", "Expecting return type of Task<T> or ValueTask<T>",
                    $"{typeSymbol.Name}.{methodSymbol.Name}, must return  Task<T> or ValueTask<T>'", "CustomErrorCategory",
                    DiagnosticSeverity.Error, isEnabledByDefault: true),
                    Location.None);
                context.ReportDiagnostic(diagnostic);
                break;
            }

            #endregion // Error Handling

            string template = clsTemplate;
            if (!string.IsNullOrEmpty(mtdTemplate))
                template = $"{template}/{mtdTemplate}";
            builder.AppendLine();
            if (verb == "GET")
            {
                builder.AppendLine($"\tasync {retType} {typeSymbol.Name}.{mtdName}()");
                builder.AppendLine("\t{");
                builder.AppendLine($"\t\tvar result = await _httpClient.GetFromJsonAsync<{symbolRetType}>(\"{template}\");");
                builder.AppendLine("\t\treturn result;");
                builder.AppendLine("\t}");
            }
            else
            {
                builder.AppendLine($"\tasync {retType} {typeSymbol.Name}.{mtdName}({symbolPrms[0].Type.Name} payload)");
                builder.AppendLine("\t{");
                builder.AppendLine("\t\tvar content = JsonContent.Create(payload);");
                builder.AppendLine($"\t\tvar response = await _httpClient.PostAsync(\"{template}\", content);");
                builder.AppendLine("\t\tif(!response.IsSuccessStatusCode)");
                builder.AppendLine($"\t\t\tthrow new HttpRequestException(\"{template} failed\", null, response.StatusCode);");
                builder.AppendLine($"\t\tvar result = await response.Content.ReadFromJsonAsync<{symbolRetType}>();");
                builder.AppendLine("\t\treturn result;");
                builder.AppendLine("\t}");
            }

        }

        builder.AppendLine("}");

        context.AddSource($"{ name}.generated.cs", builder.ToString());

        builder = new StringBuilder();
        builder.AppendHeader(syntax, typeSymbol);
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"{asm.Name}\",\"{asm.Version}\")]");
        builder.AppendLine($"public static class {name}DiExtensions");
        builder.AppendLine("{");
        builder.AppendLine($"\tpublic static IServiceCollection Add{name}Client(this IServiceCollection services, Uri baseUrl)");
        builder.AppendLine("\t{");
        builder.AppendLine($"\t\tservices.AddHttpClient<{typeSymbol.Name}, {name}>(\"{clsTemplate}\", client =>  client.BaseAddress = baseUrl);");
        builder.AppendLine("\treturn services;");
        builder.AppendLine("\t}");
        builder.AppendLine("}");

        context.AddSource($"{name}DiExtensions.generated.cs", builder.ToString());
    }

    #endregion // OnGenerate
}