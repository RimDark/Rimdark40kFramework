using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Core40k;

//A variant that hides its gene's own graphic filters that gene's nodes out of the sequence vanilla
//hands to the render tree. node.gene is assigned before each node is yielded, so no props lookup.
[HarmonyPatch(typeof(DynamicPawnRenderNodeSetup_Genes), nameof(DynamicPawnRenderNodeSetup_Genes.GetDynamicNodes))]
public static class HarmonyPatch_SuppressCustomizedGeneGraphics
{
    public static void Postfix(ref IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> __result, Pawn pawn)
    {
        if (__result == null)
        {
            return;
        }

        var comp = pawn?.GetComp<CompGeneCustomization>();
        if (comp == null || !comp.AnyHidesBaseGraphic)
        {
            return;
        }

        __result = Filter(__result, comp);
    }

    private static IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> Filter(IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> source, CompGeneCustomization comp)
    {
        foreach (var pair in source)
        {
            var gene = pair.node?.gene;
            if (gene != null && comp.HidesBaseGraphic(gene.def))
            {
                continue;
            }

            yield return pair;
        }
    }
}
