using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class DynamicPawnRenderNodeSetup_DecorativeAddons : DynamicPawnRenderNodeSetup
{
    public override bool HumanlikeOnly => true;
    
    public override IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> GetDynamicNodes(Pawn pawn, PawnRenderTree tree)
    {
        if (pawn.apparel == null || pawn.apparel.WornApparelCount == 0)
        {
            yield break;
        }

        var baseBodyType = BodyTypeUtils.SafeBodyType(pawn);

        var wornApparel = pawn.apparel.WornApparel;
        for (var i = 0; i < wornApparel.Count; i++)
        {
            var decorativeApparel = wornApparel[i];
            var decorativeComp = decorativeApparel.GetComp<CompDecorative>();
            if (decorativeComp == null)
            {
                continue;
            }

            //Scoped to this apparel - a forced bodytype must not leak into the next iteration.
            var pawnBodyType = decorativeApparel.def.GetModExtension<DefModExtension_ForcesBodyType>()?.forcedBodyType
                               ?? baseBodyType;
            foreach (var decoration in decorativeComp.Decorations)
            {
                //Internal upgrades draw nothing, so they get no render node at all.
                if (decoration.Key is not ExtraDecorationDef decorationDef || !decorationDef.HasVisual)
                {
                    continue;
                }
                if (!TryMakeProps(decorationDef, decoration.Value, decorativeComp, pawnBodyType, tree, out var pawnRenderNodeProperty, out var node))
                {
                    continue;
                }

                var pawnRenderNode = new PawnRenderNode_AttachmentExtraDecoration(pawn, pawnRenderNodeProperty, tree);

                pawnRenderNode.decorativeComp = decorativeComp;
                pawnRenderNode.decorationDef = decoration.Key;

                yield return (pawnRenderNode, node);

                if (!decorationDef.HasGlow || !decoration.Value.GlowEnabled
                    || !TryMakeProps(decorationDef, decoration.Value, decorativeComp, pawnBodyType, tree, out var glowProperty, out _))
                {
                    continue;
                }

                //A shader's render queue covers the whole draw, so the part that skips the light overlay has
                //to be a node of its own: the same texture, mask, flip and offsets through the glow shader.
                glowProperty.shaderTypeDef = Core40kDefOf.BEWH_GlowThreeColor;
                glowProperty.color = decorationDef.GlowColour(GlowChannel.Colour, decoration.Value.Color);
                glowProperty.colorTwo = decorationDef.GlowColour(GlowChannel.ColourTwo, decoration.Value.ColorTwo);
                glowProperty.colorThree = decorationDef.GlowColour(GlowChannel.ColourThree, decoration.Value.ColorThree);

                var glowNode = new PawnRenderNode_AttachmentExtraDecoration(pawn, glowProperty, tree)
                {
                    decorativeComp = decorativeComp,
                    decorationDef = decoration.Key,
                    isGlowPass = true,
                };

                yield return (glowNode, node);
            }
        }
    }

    private bool TryMakeProps(ExtraDecorationDef decorationDef, DecorationSettings decorationSettings, CompDecorative decorativeComp, BodyTypeDef pawnBodyType, PawnRenderTree tree, out PawnRenderNodePropertiesMultiColor pawnRenderNodeProperty, out PawnRenderNode node)
    {
        switch (decorativeComp.Props.decorativeType)
        {
            case DecorativeType.Head:
                pawnRenderNodeProperty = MakeHeadProps(decorationDef, decorationSettings, decorativeComp);
                node = (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.ApparelHead, out node) ? node : null) ??
                       (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out node) ? node : null);
                return true;
            case DecorativeType.Body:
                pawnRenderNodeProperty = MakeBodyProps(decorationDef, decorationSettings, decorativeComp, pawnBodyType);
                if (decorationDef.drawInHeadSpace)
                {
                    node = (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.ApparelHead, out node) ? node : null) ??
                           (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out node) ? node : null);
                }
                else
                {
                    node = (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.ApparelBody, out node) ? node : null) ??
                           (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body, out node) ? node : null);
                }
                return true;
            default:
                pawnRenderNodeProperty = null;
                node = null;
                return false;
        }
    }

    private PawnRenderNodePropertiesMultiColor MakeBodyProps(ExtraDecorationDef decorationDef, DecorationSettings decorationSettings, CompDecorative decorativeComp, BodyTypeDef pawnBodyType)
    {
        var pawnRenderNodeProperty = MakeBaseProps(decorationDef, decorationSettings, decorativeComp);
        pawnRenderNodeProperty.parentTagDef = decorationDef.drawInHeadSpace ? PawnRenderNodeTagDefOf.Head : PawnRenderNodeTagDefOf.Body;
        pawnRenderNodeProperty.workerClass = typeof(PawnRenderNodeWorker_AttachmentExtraDecorationBody);
        //A modded bodytype will never be in appliesToBodyTypes literally; accept it when any
        //bodytype it falls back to is listed, and render with that one's textures.
        pawnRenderNodeProperty.useBodyType =
            BodyTypeUtils.MatchesAny(pawnBodyType, decorationDef.appliesToBodyTypes, out var matched);
        pawnRenderNodeProperty.bodyType = matched ?? pawnBodyType;

        return pawnRenderNodeProperty;
    }
    
    private PawnRenderNodePropertiesMultiColor MakeHeadProps(ExtraDecorationDef decorationDef, DecorationSettings decorationSettings, CompDecorative decorativeComp)
    {
        var pawnRenderNodeProperty = MakeBaseProps(decorationDef, decorationSettings, decorativeComp);
        pawnRenderNodeProperty.parentTagDef = PawnRenderNodeTagDefOf.Head;
        pawnRenderNodeProperty.workerClass = typeof(PawnRenderNodeWorker_AttachmentExtraDecorationHead);

        return pawnRenderNodeProperty;
    }
    
    private PawnRenderNodePropertiesMultiColor MakeBaseProps(ExtraDecorationDef decorationDef, DecorationSettings decorationSettings, CompDecorative decorativeComp)
    {
        var pawnRenderNodeProperty = new PawnRenderNodePropertiesMultiColor
        {
            nodeClass = typeof(PawnRenderNode_AttachmentExtraDecoration),
            texPath = decorationDef.drawnTextureIconPath,
            shaderTypeDef = decorationSettings.maskDef?.shaderType ?? decorationDef.shaderType,
            drawData = decorationDef.drawData,
            drawSize = decorationDef.drawSize,
            flipGraphic = decorationSettings.Flipped,
            color = decorationSettings.Color,
            colorTwo = decorationSettings.ColorTwo,
            colorThree = decorationSettings.ColorThree,
            maskDef = decorationSettings.maskDef,
        };

        return pawnRenderNodeProperty;
    }
}