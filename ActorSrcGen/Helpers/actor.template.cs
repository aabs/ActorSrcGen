using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using System.Text;

namespace ActorSrcGen.Templates;

public partial class Actor(ActorNode ActorNode)
{
    private static string ChooseBlockName(BlockNode step)
    {
        return $"_{step.Method.Name}" + (step.NodeType == NodeType.Broadcast ? "BC" : "");
    }
    private static string ChooseBlockType(BlockNode step)
    {


        var sb = new StringBuilder();
        sb.Append(GetBlockBaseType(step));

        var methodFirstParamTypeName = step.Method.Parameters.First().Type.RenderTypename(true);
        if (step.NodeType == NodeType.Action )
        {
            sb.AppendFormat("<{0}>", methodFirstParamTypeName);
        }
        else if ( step.NodeType == NodeType.Broadcast)
        {
            sb.AppendFormat("<{0}>", step.Method.ReturnType.RenderTypename(true, true));
        }
        else
        {
            var methodReturnTypeName = step.Method.ReturnType.RenderTypename(true, true);
            {
                sb.AppendFormat("<{0},{1}>", methodFirstParamTypeName,
                    methodReturnTypeName);
            }
        }

        return sb.ToString();
    }
    private static string GetBlockBaseType(BlockNode step)
    {
        if (step.NodeType is NodeType.Broadcast)
        {
            return "BroadcastBlock";
        }

        if (step.Method.ReturnType.Name == "Void")
        {
            return "ActionBlock";
        }

        if (step.IsReturnTypeCollection)
        {
            return "TransformManyBlock";
        }

        return "TransformBlock";
    }

}

public partial class HandlerBody
{
    public HandlerBody(BlockNode step)
    {
        this.step = step;
    }
    public BlockNode step { get; set; }
}