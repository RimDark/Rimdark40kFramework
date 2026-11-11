using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

/// <summary>
/// Draws the framework's armour decorations on an outfit stand. A stand is a building and has no
/// pawn render tree, so this mirrors by hand what DynamicPawnRenderNodeSetup_DecorativeAddons and
/// the decoration node workers do on a pawn.
/// </summary>
public class OutfitStandDrawProvider_Decorations : OutfitStandDrawProvider
{
    //Pawn space layers of the render tree nodes decorations hang off. Vanilla's own outfit stand
    //code uses the same two numbers to translate apparel drawData layers into stand space.
    private const float BodyNodeLayer = 20f;
    private const float HeadNodeLayer = 70f;

    public override void CollectDraws(OutfitStandDrawContext context)
    {
        var decorativeComp = context.Apparel.GetComp<CompDecorative>();
        if (decorativeComp == null || decorativeComp.Decorations.Count == 0)
        {
            return;
        }

        var bodyComp = decorativeComp.Props.decorativeType == DecorativeType.Body;

        foreach (var pair in decorativeComp.Decorations)
        {
            if (pair.Key is not ExtraDecorationDef decoration || !decoration.HasVisual)
            {
                continue;
            }

            var settings = pair.Value;
            if (settings == null)
            {
                continue;
            }

            //Only body decorations resolve bodytyped textures on a pawn, so the stand does the same.
            var useBodyType = false;
            var graphicBodyType = context.ApparelBodyType;
            if (bodyComp && BodyTypeUtils.MatchesAny(context.ApparelBodyType, decoration.appliesToBodyTypes, out var matched))
            {
                useBodyType = true;
                graphicBodyType = matched ?? context.ApparelBodyType;
            }

            var headSpace = !bodyComp || decoration.drawInHeadSpace;

            var graphic = ArmorDecorationGraphicUtility.BuildGraphic(decoration, settings, useBodyType, graphicBodyType);
            if (graphic != null)
            {
                AddDraws(context, decorativeComp, decoration, settings, headSpace, graphic, 0f);
            }

            if (decoration.HasGlow && settings.GlowEnabled)
            {
                var glowGraphic = ArmorDecorationGraphicUtility.BuildGlowGraphic(decoration, settings, useBodyType, graphicBodyType);
                if (glowGraphic != null)
                {
                    AddDraws(context, decorativeComp, decoration, settings, headSpace, glowGraphic, ArmorDecorationGraphicUtility.GlowLayerOffset);
                }
            }
        }
    }

    private static void AddDraws(OutfitStandDrawContext context, CompDecorative decorativeComp, ExtraDecorationDef decoration, DecorationSettings settings, bool headSpace, Graphic graphic, float extraLayer)
    {
        //Only the default for a rotation the decoration does not author, so an unlayered
        //decoration lands level with its apparel and draws over it.
        var nodeLayer = headSpace ? HeadNodeLayer : BodyNodeLayer;
        var drawData = decoration.drawData;

        var baseScale = drawData?.scale ?? 1f;
        if (context.IsChild && drawData != null)
        {
            baseScale *= drawData.childScale;
        }

        var showRotation = decoration.ShowRotation(settings.Flipped);

        for (var i = 0; i < 4; i++)
        {
            var rot = new Rot4(i);
            if (showRotation != null && !showRotation.Contains(rot))
            {
                continue;
            }

            var offset = drawData?.OffsetForRot(rot) ?? Vector3.zero;
            if (drawData is { scaleOffsetByBodySize: true })
            {
                offset *= context.BodySizeFactor;
            }
            offset += decorativeComp.GetAdditionalOffsetForRot(rot, decoration);

            //Decoration layers are authored in pawn space, and the distance from the node is
            //signed - a cape is authored behind the body and has to stay behind the armour.
            var pawnLayer = drawData?.LayerForRot(rot, nodeLayer) ?? nodeLayer;
            var layer = context.StandLayerFor(pawnLayer, headSpace)
                        + decorativeComp.GetAdditionalLayerForRot(rot, decoration)
                        + extraLayer;

            var extraScale = decorativeComp.GetAdditionalScaleForRot(rot, decoration);
            var scale = new Vector3(
                decoration.drawSize.x * baseScale * extraScale.x,
                1f,
                decoration.drawSize.y * baseScale * extraScale.z);

            var flipMesh = settings.Flipped ^ (drawData?.FlipForRot(rot) ?? false);
            var angle = drawData?.RotationOffsetForRot(rot) ?? 0f;
            if (flipMesh)
            {
                angle *= -1f;
            }

            var draw = new OutfitStandDraw(graphic, layer, offset, scale, angle, flipMesh, settings.Flipped);

            if (headSpace)
            {
                context.AddHead(rot, draw);
            }
            else
            {
                context.AddBody(rot, draw);
            }
        }
    }
}
