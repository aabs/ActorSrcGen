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
    public bool HasSingleInputType => InputTypes.Distinct().Count() == 1;
    public bool HasMultipleInputTypes => InputTypes.Distinct().Count() > 1;
    public bool HasAnyInputTypes => InputTypes.Any();
    public bool HasAnyOutputTypes => OutputTypes.Any();
    public bool HasDisjointInputTypes => InputTypeNames.Distinct().Count() == InputTypeNames.Count();

    public bool HasSingleOutputType => OutputTypes.Count() == 1;
    public bool HasMultipleOutputTypes => OutputTypes.Count() > 1;
    public IEnumerable<IMethodSymbol> OutputMethods => ExitNodes.Select(n => n.Method).Where(s => !s.ReturnsVoid);
    public string Name => TypeSymbol.Name;
    public IEnumerable<string> InputTypeNames
    {
        get
        {
            return EntryNodes.Select(n => n.InputTypeName);
        }
    }
    public IEnumerable<ITypeSymbol> InputTypes
    {
        get
        {
            return EntryNodes.Select(n => n.InputType).Where(t => t is not null)!;
        }
    }
    public IEnumerable<ITypeSymbol> OutputTypes
    {
        get
        {
            return ExitNodes.Select(n => n.OutputType).Where(t => t is not null && !t.Name.Equals("void", StringComparison.InvariantCultureIgnoreCase))!;
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
    public ITypeSymbol? InputType => Method.Parameters.First().Type;
    public string InputTypeName => InputType.RenderTypename();
    public ITypeSymbol? OutputType => Method.ReturnType;
    public string OutputTypeName => OutputType.RenderTypename();
}
