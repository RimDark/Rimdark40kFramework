using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

//The gene tab builds its tooltip from gene.LabelCap and gene.def.DescriptionFull. While a variant is
//applied to the gene, both are routed through here so the tab describes the variant instead.
[HarmonyPatch(typeof(GeneUIUtility), nameof(GeneUIUtility.DrawGene))]
public static class HarmonyPatch_GeneTabVariantDescription
{
    private static readonly MethodInfo DescriptionFullGetter = AccessTools.PropertyGetter(typeof(GeneDef), nameof(GeneDef.DescriptionFull));
    private static readonly MethodInfo GeneLabelCapGetter = AccessTools.PropertyGetter(typeof(Gene), nameof(Gene.LabelCap));
    private static readonly MethodInfo DescriptionForMethod = AccessTools.Method(typeof(HarmonyPatch_GeneTabVariantDescription), nameof(DescriptionFor));
    private static readonly MethodInfo LabelForMethod = AccessTools.Method(typeof(HarmonyPatch_GeneTabVariantDescription), nameof(LabelFor));

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (var i = 0; i < codes.Count; i++)
        {
            var code = codes[i];

            if (code.Calls(DescriptionFullGetter))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code).MoveBlocksFrom(code);
                yield return new CodeInstruction(OpCodes.Call, DescriptionForMethod);
                continue;
            }

            if (code.Calls(GeneLabelCapGetter) && i > 0 && codes[i - 1].opcode == OpCodes.Ldarg_0)
            {
                yield return new CodeInstruction(OpCodes.Call, LabelForMethod).MoveLabelsFrom(code).MoveBlocksFrom(code);
                continue;
            }

            yield return code;
        }
    }

    private static GeneVariantDef VariantOf(Gene gene)
    {
        if (gene?.pawn == null || !gene.Active)
        {
            return null;
        }

        return gene.pawn.GetComp<CompGeneCustomization>()?.VariantFor(gene.def);
    }

    public static string DescriptionFor(GeneDef geneDef, Gene gene)
    {
        var variant = VariantOf(gene);
        return variant == null ? geneDef.DescriptionFull : variant.GeneTabDescription(geneDef);
    }

    public static string LabelFor(Gene gene)
    {
        var variant = VariantOf(gene);
        return variant == null ? gene.LabelCap : gene.LabelCap + " (" + variant.label + ")";
    }
}
