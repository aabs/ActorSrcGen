using ActorSrcGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorSrcGen.Model;

public enum NodeType
{
    Action,
    Batch,
    BatchedJoin,
    Buffer,
    Transform,
    TransformMany,
    Broadcast,
    Join,
    WriteOnce
}

// a visitor interface for all the Code Analysis elements of the code graph that the generator will
// need to visit
public interface IActorCodeModelVisitor
{
    ActorNode VisitActor(INamedTypeSymbol type);

    BlockNode? VisitCtor(IMethodSymbol method);

    BlockNode? VisitMethod(IMethodSymbol method);
}

public class ActorNode
{
    public List<BlockNode> EntryNodes => StepNodes.Where(s => s.IsEntryStep).ToList();
    public List<BlockNode> ExitNodes => StepNodes.Where(s => s.IsExitStep).ToList();
    public List<BlockNode> StepNodes { get; set; } = new List<BlockNode>();
    public SyntaxAndSymbol Symbol { get; set; }
    public INamedTypeSymbol TypeSymbol => Symbol.Symbol;


    #region MyRegion
    public bool HasSingleInputType => InputTypeNames.Distinct().Count() == 1;
    public bool HasMultipleInputTypes => InputTypeNames.Distinct().Count() > 1;
    public bool HasAnyInputTypes => InputTypeNames.Distinct().Count() > 0;
    public bool HasDisjointInputTypes => InputTypeNames.Distinct().Count() == InputTypeNames.Count();

    public bool HasSingleOutputType => OutputMethods.Count() == 1;
    public bool HasMultipleOutputTypes => OutputMethods.Count() > 1;
    public IEnumerable<IMethodSymbol> OutputMethods => ExitNodes.Select(n => n.Method).Where(s => !s.ReturnsVoid);
    public string Name => TypeSymbol.Name;
    public IEnumerable<string> InputTypeNames
    {
        get
        {
            return EntryNodes.Select(n => n.InputTypeName);
        }
    }
    public IEnumerable<string> OutputTypeNames
    {
        get
        {
            foreach (var fm in ExitNodes)
            {
                if (fm != null)
                {
                    ITypeSymbol returnType = fm.Method.ReturnType;
                    // extract the underlying return type for async methods if necessary
                    if (returnType.Name == "Task")
                    {
                        if (returnType is INamedTypeSymbol nts)
                        {
                            yield return nts.TypeArguments[0].RenderTypename();
                        }
                        yield return returnType.RenderTypename();
                    }
                    yield return fm.Method.ReturnType.RenderTypename();
                }
            }
        }
    }

    #endregion
}

public class BlockNode
{
    public string HandlerBody { get; set; }
    public int Id { get; set; }
    public IMethodSymbol Method { get; set; }
    public NodeType NodeType { get; set; }
    public int NumNextSteps { get; set; }
    public List<int> NextBlocks { get; set; } = new();
    public bool IsEntryStep { get; set; }
    public bool IsExitStep { get; set; }
    public string InputTypeName => Method.Parameters.First().Type.RenderTypename();
    public string OutputTypeName => Method.ReturnType.RenderTypename();
}
