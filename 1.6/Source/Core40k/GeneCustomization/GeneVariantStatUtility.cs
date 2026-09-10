using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public static class GeneVariantStatUtility
{
    /// <summary>
    /// Net stat offset a slot contributes with the given variant applied; null means the bare gene.
    /// </summary>
    public static float NetOffset(GeneDef geneDef, GeneVariantDef variant, StatDef stat)
    {
        var baseOffset = geneDef?.statOffsets.NullOrEmpty() == false ? geneDef.statOffsets.GetStatOffsetFromList(stat) : 0f;
        if (variant == null)
        {
            return baseOffset;
        }

        var variantOffset = variant.statOffsets.NullOrEmpty() ? 0f : variant.statOffsets.GetStatOffsetFromList(stat);
        return variant.statMode == GeneStatMode.Replace ? variantOffset : baseOffset + variantOffset;
    }

    public static float NetFactor(GeneDef geneDef, GeneVariantDef variant, StatDef stat)
    {
        var baseFactor = geneDef?.statFactors.NullOrEmpty() == false ? geneDef.statFactors.GetStatFactorFromList(stat) : 1f;
        if (variant == null)
        {
            return baseFactor;
        }

        var variantFactor = variant.statFactors.NullOrEmpty() ? 1f : variant.statFactors.GetStatFactorFromList(stat);
        return variant.statMode == GeneStatMode.Replace ? variantFactor : baseFactor * variantFactor;
    }

    private static void CollectStats(HashSet<StatDef> into, List<StatModifier> modifiers)
    {
        if (modifiers.NullOrEmpty())
        {
            return;
        }

        foreach (var modifier in modifiers)
        {
            if (modifier?.stat != null)
            {
                into.Add(modifier.stat);
            }
        }
    }

    /// <summary>
    /// Lines describing what the slot contributes in total with the variant applied: the gene's own
    /// stats combined with (or replaced by) the variant's.
    /// </summary>
    public static List<string> NetStatLines(GeneDef geneDef, GeneVariantDef variant)
    {
        var lines = new List<string>();
        var offsetStats = new HashSet<StatDef>();
        var factorStats = new HashSet<StatDef>();
        CollectStats(offsetStats, geneDef?.statOffsets);
        CollectStats(factorStats, geneDef?.statFactors);
        CollectStats(offsetStats, variant?.statOffsets);
        CollectStats(factorStats, variant?.statFactors);

        var offsetLines = new List<string>();
        foreach (var stat in offsetStats)
        {
            var offset = NetOffset(geneDef, variant, stat);
            if (Mathf.Approximately(offset, 0f))
            {
                continue;
            }

            offsetLines.Add(stat.LabelCap + ": " + stat.Worker.ValueToString(offset, false, ToStringNumberSense.Offset));
        }

        var factorLines = new List<string>();
        foreach (var stat in factorStats)
        {
            var factor = NetFactor(geneDef, variant, stat);
            if (Mathf.Approximately(factor, 1f))
            {
                continue;
            }

            factorLines.Add(stat.LabelCap + ": " + factor.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor));
        }

        if (offsetLines.Count > 0)
        {
            lines.Add("BEWH.Framework.CommonKeyword.StatOffset".Translate());
            lines.AddRange(offsetLines);
        }

        if (factorLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }
            lines.Add("BEWH.Framework.CommonKeyword.StatFactor".Translate());
            lines.AddRange(factorLines);
        }

        return lines;
    }

    /// <summary>
    /// Lines describing how the slot's stats change when going from current to candidate.
    /// </summary>
    public static List<string> StatDeltaLines(GeneDef geneDef, GeneVariantDef current, GeneVariantDef candidate)
    {
        var lines = new List<string>();
        if (candidate == null && current == null)
        {
            return lines;
        }

        var offsetStats = new HashSet<StatDef>();
        var factorStats = new HashSet<StatDef>();

        if (candidate?.statMode == GeneStatMode.Replace || current?.statMode == GeneStatMode.Replace)
        {
            CollectStats(offsetStats, geneDef?.statOffsets);
            CollectStats(factorStats, geneDef?.statFactors);
        }

        CollectStats(offsetStats, current?.statOffsets);
        CollectStats(offsetStats, candidate?.statOffsets);
        CollectStats(factorStats, current?.statFactors);
        CollectStats(factorStats, candidate?.statFactors);

        var offsetLines = new List<string>();
        foreach (var stat in offsetStats)
        {
            var delta = NetOffset(geneDef, candidate, stat) - NetOffset(geneDef, current, stat);
            if (Mathf.Approximately(delta, 0f))
            {
                continue;
            }

            offsetLines.Add(stat.LabelCap + ": " + stat.Worker.ValueToString(delta, false, ToStringNumberSense.Offset));
        }

        var factorLines = new List<string>();
        foreach (var stat in factorStats)
        {
            var currentFactor = NetFactor(geneDef, current, stat);
            var candidateFactor = NetFactor(geneDef, candidate, stat);
            if (Mathf.Approximately(currentFactor, 0f))
            {
                continue;
            }

            var ratio = candidateFactor / currentFactor;
            if (Mathf.Approximately(ratio, 1f))
            {
                continue;
            }

            factorLines.Add(stat.LabelCap + ": " + ratio.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor));
        }

        if (offsetLines.Count > 0)
        {
            lines.Add("BEWH.Framework.CommonKeyword.StatOffset".Translate());
            lines.AddRange(offsetLines);
        }

        if (factorLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }
            lines.Add("BEWH.Framework.CommonKeyword.StatFactor".Translate());
            lines.AddRange(factorLines);
        }

        return lines;
    }
}
