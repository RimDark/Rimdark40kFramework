using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// One threshold of a set: the rewards granted once enough pieces of the set are active.
/// Shared by gene variant sets and apparel sets.
/// </summary>
public class SetBonusTier
{
    public int requiredCount = 1;

    public string label;

    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];

    public List<AbilityDef> givesAbilities = [];
    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];
    public List<HediffDef> givesHediffs = [];

    public string LabelOrCount => label.NullOrEmpty() ? "BEWH.Framework.GeneCustomization.TierPieces".Translate(requiredCount).ToString() : label;

    /// <summary>
    /// Writes the tier's stats and grants into report.
    /// </summary>
    public void DescribeEffects(EffectReport report)
    {
        report.Stats(statOffsets, statFactors);
        report.Grants(givesAbilities, givesVFEAbilities, givesHediffs);
    }

    public string Description()
    {
        var report = new EffectReport();
        DescribeEffects(report);
        return report.ToString();
    }
}
