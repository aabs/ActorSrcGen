using System;
using System.Collections.Generic;
using System.Text;

namespace DataflowSrcGen.Generators;

static class SourceTemplates
{
    public static string CreateBlockDefinition(string name, string methodName, string inputType, string outputType, int capacity, int maxParallelism)
    {
        return $$"""
            _{{name}} = new TransformBlock<{{inputType}}, {{outputType}}>({{methodName}},
                new ExecutionDataflowBlockOptions() {
                    BoundedCapacity = {{capacity}},
                    MaxDegreeOfParallelism = {{maxParallelism}}
            });
            """;
    }

    public static string CreateBlockDeclaration(string name, string inputType, string outputType)
    {
        return $$"""
        TransformBlock<{{inputType}}, {{outputType}}> _{{name}};
        """;
    }

}
