using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class CompProperties_AbilityHideIfHasAbility : CompProperties_AbilityEffect
{
    public List<AbilityDef> abilities = [];

    public CompProperties_AbilityHideIfHasAbility()
    {
        compClass = typeof(CompAbilityEffect_HideIfHasAbility);
    }
}
