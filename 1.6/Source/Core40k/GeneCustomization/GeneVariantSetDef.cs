using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class GeneSetTier
{
    public int requiredCount = 1;

    public string label;

    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];

    public List<AbilityDef> givesAbilities = [];
    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];
    public List<HediffDef> givesHediffs = [];

    public string Description()
    {
        var builder = new StringBuilder();

        if (!statOffsets.NullOrEmpty())
        {
            foreach (var statOffset in statOffsets)
            {
                builder.AppendLine(statOffset.stat.LabelCap + ": " + statOffset.ValueToStringAsOffset);
            }
        }

        if (!statFactors.NullOrEmpty())
        {
            foreach (var statFactor in statFactors)
            {
                builder.AppendLine(statFactor.stat.LabelCap + ": " + statFactor.ToStringAsFactor);
            }
        }

        foreach (var ability in givesAbilities)
        {
            builder.AppendLine("BEWH.Framework.GeneCustomization.GrantsLine".Translate(ability.LabelCap));
        }

        foreach (var ability in givesVFEAbilities)
        {
            builder.AppendLine("BEWH.Framework.GeneCustomization.GrantsLine".Translate(ability.LabelCap));
        }

        foreach (var hediff in givesHediffs)
        {
            builder.AppendLine("BEWH.Framework.GeneCustomization.GrantsLine".Translate(hediff.LabelCap));
        }

        return builder.ToString().TrimEndNewlines();
    }
}

/// <summary>
/// A group of variants that grant extra rewards when enough of them are applied at once. Tiers are
/// cumulative: at five pieces the two-piece and four-piece tiers are both active.
/// </summary>
public class GeneVariantSetDef : Def
{
    [NoTranslate]
    public string iconPath;

    [Unsaved]
    private Texture2D icon;

    public Texture2D Icon => icon ??= !iconPath.NullOrEmpty() ? ContentFinder<Texture2D>.Get(iconPath) : null;

    public float sortOrder = 0f;

    public List<GeneVariantDef> variants = [];

    public List<GeneSetTier> tiers = [];

    public int CountIn(ICollection<GeneVariantDef> applied)
    {
        var count = 0;
        foreach (var variant in variants)
        {
            if (variant != null && applied.Contains(variant))
            {
                count++;
            }
        }

        return count;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var configError in base.ConfigErrors())
        {
            yield return configError;
        }

        if (variants.NullOrEmpty())
        {
            yield return "set has no variants";
        }

        foreach (var tier in tiers)
        {
            if (tier == null)
            {
                yield return "null tier";
                continue;
            }

            if (tier.requiredCount < 1)
            {
                yield return "tier requiredCount must be at least 1";
            }

            if (!variants.NullOrEmpty() && tier.requiredCount > variants.Count)
            {
                yield return "tier requires " + tier.requiredCount + " pieces but the set only has " + variants.Count;
            }
        }
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (tiers.NullOrEmpty())
        {
            tiers = [new GeneSetTier { requiredCount = variants.Count }];
        }

        tiers.SortBy(tier => tier.requiredCount);
    }
}
