using UnityEngine;
using Verse;

namespace Core40k;

public class PawnRenderNodeWorker_AttachmentExtraDecoration : PawnRenderNodeWorker
{
    /// <summary>
    /// False when the apparel this decoration belongs to has render nodes and none of them would draw right now, so hidden helmets and armour hide their decorations too.
    /// </summary>
    protected static bool HostApparelVisible(PawnRenderNode_AttachmentExtraDecoration node, PawnDrawParms parms, out bool hostFound)
    {
        var hosts = node.HostApparelNodes;
        hostFound = !hosts.NullOrEmpty();
        if (!hostFound)
        {
            return true;
        }

        foreach (var host in hosts)
        {
            if (HostNodeDraws(host, parms))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HostNodeDraws(PawnRenderNode host, PawnDrawParms parms)
    {
        var worker = host.Worker;
        if (worker == null || !worker.CanDrawNow(host, parms))
        {
            return false;
        }

        foreach (var subWorker in host.Props.SubWorkers)
        {
            if (!subWorker.CanDrawNowSub(host, parms))
            {
                return false;
            }
        }

        return true;
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        var offset = base.OffsetFor(node, parms, out pivot);
        
        if (node is not PawnRenderNode_AttachmentExtraDecoration pawnRenderNode)
        {
            return offset;
        }

        offset += pawnRenderNode.decorativeComp.GetAdditionalOffsetForRot(parms.facing, pawnRenderNode.decorationDef);
        
        return offset;
    }
    
    public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
    {
        var layer = base.LayerFor(node, parms);

        if (node is not PawnRenderNode_AttachmentExtraDecoration pawnRenderNode)
        {
            return layer;
        }
        
        layer += pawnRenderNode.decorativeComp.GetAdditionalLayerForRot(parms.facing, pawnRenderNode.decorationDef);
        
        return layer;
    }
    
    public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
    {
        var scale = base.ScaleFor(node, parms);
        
        if (node is not PawnRenderNode_AttachmentExtraDecoration pawnRenderNode)
        {
            return scale;
        }
        
        var additionalScale = pawnRenderNode.decorativeComp.GetAdditionalScaleForRot(parms.facing, pawnRenderNode.decorationDef);

        scale.x *= additionalScale.x;
        scale.z *= additionalScale.z;
        
        return scale;
    }
}