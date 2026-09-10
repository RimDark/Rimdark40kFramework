using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class PawnRenderNode_GeneVariant : PawnRenderNode
{
    public GeneDef geneDef;
    public GeneVariantDef variantDef;

    public PawnRenderNode_GeneVariant(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
    }

    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        if (Props.overrideMeshSize.HasValue)
        {
            return base.MeshSetFor(pawn);
        }

        if (Props.parentTagDef == PawnRenderNodeTagDefOf.Body)
        {
            return HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn, Props.drawSize.x, Props.drawSize.y);
        }

        return HumanlikeMeshPoolUtility.GetHumanlikeHairSetForPawn(pawn, Props.drawSize.x, Props.drawSize.y);
    }

    public override Graphic GraphicFor(Pawn pawn)
    {
        var propsMulti = (PawnRenderNodePropertiesMultiColor)Props;

        var texPath = propsMulti.texPath;
        var shader = propsMulti.shaderTypeDef ?? Core40kDefOf.BEWH_CutoutThreeColor;
        var maskPath = propsMulti.maskDef?.maskPath ?? string.Empty;

        var bodyType = propsMulti.bodyType ?? pawn?.story?.bodyType;
        var useBodyType = propsMulti.useBodyType && bodyType != null;
        var gender = pawn?.gender ?? Gender.None;

        if (useBodyType)
        {
            texPath = BodyTypeUtils.BodyTypedPath(texPath, bodyType, gender);
        }

        if (maskPath != string.Empty)
        {
            if (useBodyType)
            {
                maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType, gender) ?? maskPath;
            }
        }
        else
        {
            maskPath = propsMulti.texPath;
            if (useBodyType)
            {
                maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType, gender) ?? maskPath;
            }
            maskPath += "_mask";
        }

        return MultiColorUtils.GetGraphic<Graphic_Multi>(texPath, shader.Shader, propsMulti.drawSize, propsMulti.color ?? Color.white, propsMulti.colorTwo ?? Color.white, propsMulti.colorThree ?? Color.white, null, maskPath);
    }
}
