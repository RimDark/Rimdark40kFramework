using RimWorld;
using Verse;

namespace Core40k;

public class PawnRenderNodeWorker_AttachmentExtraDecorationHead : PawnRenderNodeWorker_AttachmentExtraDecoration
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        var pawn = parms.pawn;

        if (node is not PawnRenderNode_AttachmentExtraDecoration pawnRenderNode)
        {
            return false;
        }
        
        if (pawnRenderNode.decorationDef is not ExtraDecorationDef extraDecoration)
        {
            return false;
        }
            
        if (!HostApparelVisible(pawnRenderNode, parms, out var hostFound))
        {
            return false;
        }

        if (!hostFound && !PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms))
        {
            return false;
        }

        var showWhenFacing = extraDecoration.ShowRotation(node.Props.flipGraphic);
        if (parms.Portrait)
        {
            if (!showWhenFacing.Contains(parms.facing))
            {
                return false;
            }
        }
        else
        {
            if (parms.posture is PawnPosture.LayingOnGroundNormal or PawnPosture.LayingOnGroundFaceUp)
            {
                return true;
            }

            if (pawn.Swimming)
            {
                return false;
            }
                
            if (!showWhenFacing.Contains(parms.facing))
            {
                return false;
            }
                
            if (parms.posture == PawnPosture.Standing)
            {
                return true;
            }
            
            var mindState = pawn.mindState;
            if (mindState != null && mindState.duty?.def?.drawBodyOverride.HasValue == true)
            {
                return pawn.mindState.duty.def.drawBodyOverride.Value;
            }
            if (parms.bed != null && parms.pawn.RaceProps.Humanlike)
            {
                return parms.bed.def.building.bed_showSleeperBody;
            }
        }
            
        return base.CanDrawNow(node, parms);
    }
}