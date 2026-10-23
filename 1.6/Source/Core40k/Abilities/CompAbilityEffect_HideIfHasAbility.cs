using RimWorld;
using Verse;

namespace Core40k;

public class CompAbilityEffect_HideIfHasAbility : CompAbilityEffect
{
    private new CompProperties_AbilityHideIfHasAbility Props => (CompProperties_AbilityHideIfHasAbility)props;

    /// <summary>
    /// Hides the gizmo while the pawn has any of the listed abilities.
    /// </summary>
    public override bool ShouldHideGizmo
    {
        get
        {
            var abilityTracker = parent.pawn?.abilities;
            if (abilityTracker == null || Props.abilities.NullOrEmpty())
            {
                return false;
            }

            for (var i = 0; i < Props.abilities.Count; i++)
            {
                var ability = Props.abilities[i];
                if (ability != null && ability != parent.def && abilityTracker.GetAbility(ability, true) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
