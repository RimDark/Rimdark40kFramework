using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

public class CompAbilityEffect_RequireRank : CompAbilityEffect
{
    private new CompProperties_AbilityRequireRank Props => (CompProperties_AbilityRequireRank)props;

    private string RequirementLabel
    {
        get
        {
            var parts = Props.requiredRanksAll.Select(rank => rank.label).ToList();
            if (!Props.requiredRanksOneAmong.NullOrEmpty())
            {
                parts.Add(Props.requiredRanksOneAmong.Select(rank => rank.label).ToCommaListOr());
            }
            return parts.ToCommaList();
        }
    }

    /// <summary>
    /// True when the pawn currently holds every rank in requiredRanksAll and at least one rank in requiredRanksOneAmong.
    /// A pawn without a CompRankInfo only passes if no ranks are required.
    /// </summary>
    public static bool MeetsRequirement(Pawn pawn, CompProperties_AbilityRequireRank props)
    {
        var needsAll = !props.requiredRanksAll.NullOrEmpty();
        var needsOneAmong = !props.requiredRanksOneAmong.NullOrEmpty();
        if (!needsAll && !needsOneAmong)
        {
            return true;
        }

        var rankComp = pawn?.GetComp<CompRankInfo>();
        if (rankComp == null)
        {
            return false;
        }

        if (needsAll && props.requiredRanksAll.Any(rank => !rankComp.HasRank(rank)))
        {
            return false;
        }

        return !needsOneAmong || props.requiredRanksOneAmong.Any(rankComp.HasRank);
    }

    public override bool GizmoDisabled(out string reason)
    {
        if (MeetsRequirement(parent.pawn, Props))
        {
            reason = null;
            return false;
        }

        reason = "BEWH.Framework.Comp.CasterMissingRequiredRank".Translate(RequirementLabel);
        return true;
    }

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        if (MeetsRequirement(parent.pawn, Props))
        {
            return base.Valid(target, throwMessages);
        }

        if (throwMessages)
        {
            Messages.Message("BEWH.Framework.Comp.CasterMissingRequiredRank".Translate(RequirementLabel), parent.pawn, MessageTypeDefOf.RejectInput, false);
        }
        return false;
    }
}
