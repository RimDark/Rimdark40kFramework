using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(StatDef), "PopulateMutableStats")]
public class PopulateMutableStatsWithRankStatsPatch
{
    public static void Postfix(ref HashSet<StatDef> ___mutableStats)
    {
        foreach (var rankDef in DefDatabase<RankDef>.AllDefsListForReading)
        {
            if (rankDef.statFactors != null)
            {
                ___mutableStats.AddRange(rankDef.statFactors.Select(mod => mod.stat));
            }
            if (rankDef.statOffsets != null)
            {
                ___mutableStats.AddRange(rankDef.statOffsets.Select(mod => mod.stat));
            }
        }
        
        foreach (var decorationDef in DefDatabase<DecorationDef>.AllDefsListForReading)
        {
            if (decorationDef.statFactors != null)
            {
                ___mutableStats.AddRange(decorationDef.statFactors.Select(mod => mod.stat));
            }
            if (decorationDef.statOffsets != null)
            {
                ___mutableStats.AddRange(decorationDef.statOffsets.Select(mod => mod.stat));
            }
        }
        
        foreach (var variantDef in DefDatabase<GeneVariantDef>.AllDefsListForReading)
        {
            if (variantDef.statFactors != null)
            {
                ___mutableStats.AddRange(variantDef.statFactors.Select(mod => mod.stat));
            }
            if (variantDef.statOffsets != null)
            {
                ___mutableStats.AddRange(variantDef.statOffsets.Select(mod => mod.stat));
            }
        }

        foreach (var setDef in DefDatabase<GeneVariantSetDef>.AllDefsListForReading)
        {
            foreach (var tier in setDef.tiers)
            {
                if (tier.statFactors != null)
                {
                    ___mutableStats.AddRange(tier.statFactors.Select(mod => mod.stat));
                }
                if (tier.statOffsets != null)
                {
                    ___mutableStats.AddRange(tier.statOffsets.Select(mod => mod.stat));
                }
            }
        }

        foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            var ammoChangerExtension = thingDef.GetModExtension<DefModExtension_AmmoChanger>();
            if (ammoChangerExtension == null)
            {
                continue;
            }
            
            if (ammoChangerExtension.statFactors != null)
            {
                ___mutableStats.AddRange(ammoChangerExtension.statFactors.Select(mod => mod.stat));
            }
            if (ammoChangerExtension.statOffsets != null)
            {
                ___mutableStats.AddRange(ammoChangerExtension.statOffsets.Select(mod => mod.stat));
            }
        }
    }
}