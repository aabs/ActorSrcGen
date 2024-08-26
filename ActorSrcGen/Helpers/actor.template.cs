using System;
using System.Collections.Generic;
using System.Text;
using ActorSrcGen.Helpers;
using ActorSrcGen.Model;
using Microsoft.CodeAnalysis;

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
        if (step.NodeType == NodeType.Action)
        {
            sb.AppendFormat("<{0}>", methodFirstParamTypeName);
        }
        else
        {
            var methodReturnTypeName = step.Method.ReturnType.RenderTypename(true);
            if (step.NodeType == NodeType.Broadcast)
            {
                sb.AppendFormat("<{0}>", methodReturnTypeName);
            }
            else
            {
                sb.AppendFormat("<{0},{1}>", methodFirstParamTypeName,
                    methodReturnTypeName);
            }
        }

        return sb.ToString();
    }
    private static string GetBlockBaseType(BlockNode step)
    {
        return step.NodeType switch
        {
            NodeType.Action => "ActionBlock",
            NodeType.Batch => "TransformBlock",
            NodeType.BatchedJoin => "BatchedJoinBlock",
            NodeType.Buffer => "BufferBlock",
            NodeType.Transform => "TransformBlock",
            NodeType.TransformMany => "TransformManyBlock",
            NodeType.Broadcast => "BroadcastBlock",
            NodeType.Join => "JoinBlock",
            NodeType.WriteOnce => "WriteOnceBlock",
            _ => "TransformBlock",
        };
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