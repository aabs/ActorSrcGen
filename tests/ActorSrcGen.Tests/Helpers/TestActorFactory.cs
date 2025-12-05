using System.Text;

namespace ActorSrcGen.Tests.Helpers;

public static class TestActorFactory
{
    public static string CreateTestActor(string name, string[] steps)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using ActorSrcGen;");
        builder.AppendLine();
        builder.AppendLine("namespace ActorSrcGen.Generated.Tests;");
        builder.AppendLine("{");
        builder.AppendLine($"    [Actor]\n    public partial class {name}");
        builder.AppendLine("    {");

        foreach (var step in steps)
        {
            builder.AppendLine(step);
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    public static string CreateActorWithIngest(string name)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using ActorSrcGen;");
        builder.AppendLine();
        builder.AppendLine("namespace ActorSrcGen.Generated.Tests;");
        builder.AppendLine("{");
        builder.AppendLine($"    [Actor]\n    public partial class {name}");
        builder.AppendLine("    {");
        builder.AppendLine("        [FirstStep]\n        public void Start(string input) { }");
        builder.AppendLine("        [Ingest]\n        public static Task<string> IngestAsync() => Task.FromResult(\"input\");");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    public static string CreateActorWithMultipleInputs(string name, int inputCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using ActorSrcGen;");
        builder.AppendLine();
        builder.AppendLine("namespace ActorSrcGen.Generated.Tests;");
        builder.AppendLine("{");
        builder.AppendLine($"    [Actor]\n    public partial class {name}");
        builder.AppendLine("    {");

        for (var i = 0; i < inputCount; i++)
        {
            var methodName = $"Step{i + 1}";
            builder.AppendLine($"        [FirstStep]\n        public void {methodName}(string input{ i + 1 }) {{ }}");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
