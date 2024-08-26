using ActorSrcGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Model;

public class ActorVisitor
{
    public int BlockCounter { get; set; } = 0;
    public List<ActorNode> Actors => _actorStack.ToList();
    public Dictionary<IMethodSymbol, List<IMethodSymbol>> DependencyGraph { get; set; }
    private Stack<ActorNode> _actorStack = new Stack<ActorNode>();
    private Stack<BlockNode> _blockStack = new Stack<BlockNode>();
    private static IEnumerable<IMethodSymbol> GetStepMethods(INamedTypeSymbol typeSymbol)
    {
        return from m in typeSymbol.GetMembers()
               let ms = m as IMethodSymbol
               where ms is not null
               where ms.GetBlockAttr() is not null
               where ms.Name != ".ctor"
               select ms;
    }

    private Dictionary<IMethodSymbol, List<IMethodSymbol>> BuildDependencyGraph(INamedTypeSymbol typeSymbol)
    {
        var methods = GetStepMethods(typeSymbol).ToArray();
        //var methods = (from fromStep in ss.Symbol.GetMembers()
        //               let ms = fromStep as IMethodSymbol
        //               where ms is not null
        //               where ms.GetAttributes().Any(a => a.AttributeClass.Name.EndsWith("StepAttribute"))
        //               where ms.Name != ".ctor"
        //               select ms).ToArray();

        var deps = new Dictionary<IMethodSymbol, List<IMethodSymbol>>();
        foreach (var fromStep in methods.Where(x => x.GetBlockAttr().AttributeClass.Name != nameof(LastStepAttribute)))
        {
            deps[fromStep] = new();
            foreach (var a in fromStep.GetNextStepAttrs())
            {
                var nextArg = a.GetArg<string>(0);
                var toStep = methods.FirstOrDefault(n => n.Name == nextArg);
                deps[fromStep].Add(toStep);
            }
        }
        return deps;
    }
    public void VisitActor(SyntaxAndSymbol symbol)
    {
        DependencyGraph = BuildDependencyGraph(symbol.Symbol);
        ActorNode actor = new()
        {
            Symbol = symbol
        };
        var methods = GetStepMethods(symbol.Symbol);
        foreach (var mi in methods)
        {
            VisitMethod(mi);
        }
        actor.StepNodes = _blockStack.ToList();
        _actorStack.Push(actor);
        _blockStack.Clear();

        // now wire up the blocks using data from the dependency graph
        foreach (var block in actor.StepNodes)
        {
            if (DependencyGraph.TryGetValue(block.Method, out var nextSteps))
            {
                if (nextSteps.Count > 1)
                {
                    if (block.NodeType == NodeType.Broadcast)
                    {
                        // nothing to be done - the source node is already wired to the broadcast node
                        var broadcastNode = actor.StepNodes.FirstOrDefault(b => b.Id == block.NextBlocks.First()); // not sure this can cope with circularity

                        continue;
                    }
                    else
                    {
                        var broadcastNode = actor.StepNodes.FirstOrDefault(b => b.Id == block.NextBlocks.First()); // not sure this can cope with circularity
                        foreach (var nextStep in nextSteps)
                        {
                            var nextBlock = actor.StepNodes.FirstOrDefault(b => b.Method == nextStep);
                            if (nextBlock != null)
                            {
                                broadcastNode.NextBlocks.Add(nextBlock.Id);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var nextStep in nextSteps)
                    {
                        var nextBlock = actor.StepNodes.FirstOrDefault(b => b.Method == nextStep);
                        if (nextBlock != null)
                        {
                            block.NextBlocks.Add(nextBlock.Id);
                        }
                    }
                }
            }
        }
    }

    public void VisitMethod(IMethodSymbol method)
    {
        BlockNode? blockNode = null;
        if (IsReturnTypeCollection(method))
        {
            if (IsAsyncMethod(method))
            {
                blockNode = CreateAsyncManyNode(method);
            }
            else
            {
                blockNode = CreateManyNode(method);
            }
        }
        else
        {
            if (IsAsyncMethod(method))
            {
                blockNode = CreateAsyncNode(method);
            }
            else
            {
                blockNode = CreateDefaultNode(method);
            }

        }

        if (method.ReturnType.Name == "Void")
        {
            blockNode = CreateActionNode(method);
        }

        blockNode.IsAsync = IsAsyncMethod(method);
        blockNode.IsReturnTypeCollection = IsReturnTypeCollection(method);
        blockNode.Id = ++BlockCounter;
        blockNode.NumNextSteps = blockNode.Method.GetNextStepAttrs().Count();
        
        if (blockNode.NumNextSteps > 1)
        {
            // if we get here, we have to split via a synthetic BroadcastBlock.
            var bn = CreateIdentityBroadcastNode(blockNode.Method);
            bn.Id = ++BlockCounter;
            //blockNode.NumNextSteps = 1;
            blockNode.NextBlocks.Add(bn.Id);
            _blockStack.Push(bn);
        }

        blockNode.IsEntryStep = method.IsStartStep();
        blockNode.IsExitStep = method.IsEndStep();
        _blockStack.Push(blockNode);
    }

    private bool IsReturnTypeCollection(IMethodSymbol method)
    {
        var t = method.ReturnType;
        if (t.Name == "Task")
        {
            t = t.GetFirstTypeParameter();
        }
        var returnTypeIsEnumerable = t.AllInterfaces.Any(i => i.Name.StartsWith("IEnumerable", StringComparison.InvariantCultureIgnoreCase));
        return returnTypeIsEnumerable;
    }

    private bool IsAsyncMethod(IMethodSymbol method)
    {
        return (method.IsAsync || method.ReturnType.Name == "Task");
    }

    private BlockNode CreateActionNode(IMethodSymbol method)
    {
        string inputTypeName = method.Parameters.First().Type.RenderTypename();
        return new()
        {
            Method = method,
            NodeType = NodeType.Action,
            HandlerBody = $$"""
                    ({{inputTypeName}} x) => {
                        try
                        {
                            {{method.Name}}(x);
                        }catch{}
                    }
            """
        };
    }

    private BlockNode CreateIdentityBroadcastNode(IMethodSymbol method)
    {
        string inputTypeName = method.ReturnType.RenderTypename(true, true);
        return new()
        {
            Method = method,
            NodeType = NodeType.Broadcast,
            HandlerBody = $"({inputTypeName} x) => x"
        };
    }

    private BlockNode CreateAsyncManyNode(IMethodSymbol method)
    {
        var collectionType = method.ReturnType.GetFirstTypeParameter().RenderTypename();
        string inputTypeName = method.Parameters.First().Type.RenderTypename();
        var returnTypeName = method.ReturnType.RenderTypename(false).ToLowerInvariant();
        return new()
        {
            Method = method,
            NodeType = NodeType.TransformMany,
            HandlerBody = $$"""
                    async ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.AddRange(await {{method.Name}}(x));
                        }catch{}
                        return result;
                    }
            """
        };
    }

    private BlockNode CreateAsyncNode(IMethodSymbol method)
    {
        var collectionType = method.ReturnType.GetFirstTypeParameter().RenderTypename();
        string inputTypeName = method.Parameters.First().Type.RenderTypename();
        var returnTypeName = method.ReturnType.RenderTypename(false).ToLowerInvariant();
        return new()
        {
            Method = method,
            NodeType = NodeType.TransformMany,
            HandlerBody = $$"""
                    async ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.Add(await {{method.Name}}(x));
                        }catch{}
                        return result;
                    }
            """
        };
    }

    private BlockNode CreateDefaultNode(IMethodSymbol method)
    {
        string inputTypeName = method.Parameters.First().Type.RenderTypename();
        var returnTypeName = method.ReturnType.RenderTypename(false).ToLowerInvariant();
        return new()
        {
            Method = method,
            NodeType = NodeType.Transform,
            HandlerBody = $$"""
                    ({{inputTypeName}} x) => {
                        try
                        {
                            return {{method.Name}}(x);
                        }
                        catch
                        {
                            return default;
                        }
                    }
            """
        };
    }

    private BlockNode CreateManyNode(IMethodSymbol method)
    {
        var collectionType = method.ReturnType.GetFirstTypeParameter().RenderTypename();
        string inputTypeName = method.Parameters.First().Type.RenderTypename();
        var returnTypeName = method.ReturnType.RenderTypename(false).ToLowerInvariant();
        return new()
        {
            Method = method,
            NodeType = NodeType.Transform,
            HandlerBody = $$"""
                    ({{inputTypeName}} x) => {
                        var result = new List<{{collectionType}}>();
                        try
                        {
                            result.AddRange({{method.Name}}(x));
                        }catch{}
                        return result;
                    }
            """
        };
    }
}