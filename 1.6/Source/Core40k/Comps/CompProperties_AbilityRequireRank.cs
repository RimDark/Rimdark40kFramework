using System.Collections.Generic;
using RimWorld;

namespace Core40k;

public class CompProperties_AbilityRequireRank : CompProperties_AbilityEffect
{
    public List<RankDef> requiredRanksOneAmong = [];
    public List<RankDef> requiredRanksAll = [];

    public CompProperties_AbilityRequireRank()
    {
        compClass = typeof(CompAbilityEffect_RequireRank);
    }
}
