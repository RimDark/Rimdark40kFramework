using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class DynamicPawnRenderNodeSetup_GeneVariants : DynamicPawnRenderNodeSetup
{
    public override bool HumanlikeOnly => true;

    private static readonly List<Type> SetupAfterGenes = [typeof(DynamicPawnRenderNodeSetup_Genes)];

    public override List<Type> SetupAfter => SetupAfterGenes;

    public override IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> GetDynamicNodes(Pawn pawn, PawnRenderTree tree)
    {
        if (!ModsConfig.BiotechActive || pawn.genes == null)
        {
            yield break;
        }

        var comp = pawn.GetComp<CompGeneCustomization>();
        if (comp == null || comp.IsEmpty)
        {
            yield break;
        }

        var bodyType = BodyTypeUtils.SafeBodyType(pawn);

        foreach (var choice in comp.ActiveChoices)
        {
            var variant = choice.variant;
            if (!variant.HasVisual)
            {
                continue;
            }

            var settings = choice.settings;
            var parentTag = variant.parentTagDef ?? PawnRenderNodeTagDefOf.Head;
            var props = new PawnRenderNodePropertiesMultiColor
            {
                debugLabel = variant.defName,
                nodeClass = typeof(PawnRenderNode_GeneVariant),
                workerClass = variant.workerClass ?? DefaultWorkerFor(parentTag),
                texPath = variant.texPath,
                parentTagDef = parentTag,
                shaderTypeDef = settings.maskDef?.shaderType ?? variant.shaderType,
                drawData = variant.drawData,
                drawSize = variant.drawSize,
                overrideMeshSize = variant.overrideMeshSize,
                baseLayer = variant.baseLayer,
                visibleFacing = variant.visibleFacing,
                rotDrawMode = RotDrawMode.Fresh | RotDrawMode.Rotting,
                color = settings.Color,
                colorTwo = settings.ColorTwo,
                colorThree = settings.ColorThree,
                maskDef = settings.maskDef,
                useBodyType = variant.useBodyType,
                bodyType = bodyType,
            };

            var node = new PawnRenderNode_GeneVariant(pawn, props, tree)
            {
                geneDef = choice.gene,
                variantDef = variant,
            };

            yield return (node, null);
        }
    }

    private static Type DefaultWorkerFor(PawnRenderNodeTagDef parentTag)
    {
        return parentTag == PawnRenderNodeTagDefOf.Body ? typeof(PawnRenderNodeWorker_AttachmentBody) : typeof(PawnRenderNodeWorker_FlipWhenCrawling);
    }
}
