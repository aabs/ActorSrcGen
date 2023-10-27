using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace DataflowSrcGen.Generators;

static class SourceTemplates
{
    public static string CreateBlockDefinition(string name, string methodName, string inputType, string outputType, int capacity, int maxParallelism)
    {
        return $$"""
            {{name}} = new TransformBlock<{{inputType}}, {{outputType}}>({{methodName}},
                new ExecutionDataflowBlockOptions() {
                    BoundedCapacity = {{capacity}},
                    MaxDegreeOfParallelism = {{maxParallelism}}
            });
            RegisterChild({{name}});
            """;
    }

    public static string CreateBlockDeclaration(string name, string inputType, string outputType)
    {
        return $$"""
        TransformBlock<{{inputType}}, {{outputType}}> {{name}};
        """;
    }

    public static string CreateActorClass(string className)
    {
        return $$"""
        [System.CodeDom.Compiler.GeneratedCode(\"{asm.Name}\",\"{asm.Version}\")]");
        public partial class {{className}}
        {
        }
        """;

    }
}
