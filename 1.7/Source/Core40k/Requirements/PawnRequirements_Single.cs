using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Biological age in years. -1 means no limit on that side.
/// </summary>
public class PawnRequirement_Age : PawnRequirement
{
    public float minAge = -1;
    public float maxAge = -1;

    public override bool IsEmpty => minAge < 0 && maxAge < 0;

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        if (minAge < 0 && maxAge < 0)
        {
            return true;
        }

        report.Header("BEWH.Framework.RankSystem.RequirementsAge".Translate());

        var age = context.pawn?.ageTracker?.AgeBiologicalYears;
        var met = true;
        if (minAge >= 0)
        {
            var minAgeMet = age >= minAge;
            met &= minAgeMet;
            report.Line("BEWH.Framework.RankSystem.MinimumBiologicalAge".Translate((int)MathF.Round(minAge)), minAgeMet);
        }

        if (maxAge >= 0)
        {
            var maxAgeMet = age <= maxAge;
            met &= maxAgeMet;
            report.Line("BEWH.Framework.RankSystem.MaximumBiologicalAge".Translate((int)MathF.Round(maxAge)), maxAgeMet);
        }

        return met;
    }

    public override bool IsMet(RequirementContext context)
    {
        if (minAge < 0 && maxAge < 0)
        {
            return true;
        }

        var age = context.pawn?.ageTracker?.AgeBiologicalYears;
        return age != null && (minAge < 0 || age >= minAge) && (maxAge < 0 || age <= maxAge);
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (minAge < 0 && maxAge < 0)
        {
            yield return $"{parent.defName} has a {GetType().Name} with neither minAge nor maxAge set.";
        }
    }
}

/// <summary>
/// The pawn's gender.
/// </summary>
public class PawnRequirement_Gender : PawnRequirement
{
    public Gender gender = Gender.None;

    public override bool IsEmpty => gender == Gender.None;

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        if (gender == Gender.None)
        {
            return true;
        }

        var met = IsMet(context);
        report.Header("BEWH.Framework.RankSystem.RequirementsGender".Translate());
        report.Line("BEWH.Framework.RankSystem.MustBeGender".Translate(gender.GetLabel().CapitalizeFirst()), met);
        return met;
    }

    public override bool IsMet(RequirementContext context)
    {
        return gender == Gender.None || (context.pawn != null && context.pawn.gender == gender);
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (gender == Gender.None)
        {
            yield return $"{parent.defName} has a {GetType().Name} without a gender.";
        }
    }
}

/// <summary>
/// The pawn must be a slave of the colony.
/// </summary>
public class PawnRequirement_SlaveOfColony : PawnRequirement
{
    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        var met = IsMet(context);
        report.Header("BEWH.Framework.RankSystem.RequirementsStatus".Translate());
        report.Line("BEWH.Framework.RankSystem.MustBeSlaveOfColony".Translate(), met);
        return met;
    }

    public override bool IsMet(RequirementContext context)
    {
        return context.pawn != null && context.pawn.IsSlaveOfColony;
    }
}

/// <summary>
/// Ranks the pawn must not hold.
/// </summary>
public class PawnRequirement_IncompatibleRanks : PawnRequirement
{
    public List<RankDef> ranks = [];

    public override bool IsEmpty => ranks.NullOrEmpty();

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        if (ranks.NullOrEmpty())
        {
            return true;
        }

        report.Header("BEWH.Framework.RankSystem.IncompatibleRank".Translate());

        var met = true;
        foreach (var rank in ranks)
        {
            var held = HasRank(context, rank);
            met &= !held;
            report.Line(rank.label.CapitalizeFirst(), !held);
        }

        return met;
    }

    public override bool IsMet(RequirementContext context)
    {
        if (ranks.NullOrEmpty())
        {
            return true;
        }

        foreach (var rank in ranks)
        {
            if (HasRank(context, rank))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasRank(RequirementContext context, RankDef rank)
    {
        return context.RankComp != null && context.RankComp.HasRank(rank);
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (ranks.NullOrEmpty())
        {
            yield return $"{parent.defName} has a {GetType().Name} with nothing listed.";
        }
    }
}

/// <summary>
/// One of the listed body types, as worn: a body type forced by the pawn's customizable armour
/// (DefModExtension_ForcesBodyType) counts, and modded body types match through their fallback chain.
/// Met when the pawn wears no customizable armour (or there is no pawn), since body-typed decorations only
/// matter on armour; this is the check behind appliesToBodyTypes on armour decorations.
/// </summary>
public class PawnRequirement_BodyType : PawnRequirement
{
    public List<BodyTypeDef> bodyTypes = [];

    public override bool IsEmpty => bodyTypes.NullOrEmpty();

    private static bool TryGetWornBodyType(Pawn pawn, out BodyTypeDef bodyType)
    {
        bodyType = null;
        if (pawn?.apparel == null)
        {
            return false;
        }

        var wornApparel = pawn.apparel.WornApparel;
        for (var i = 0; i < wornApparel.Count; i++)
        {
            if (!wornApparel[i].HasComp<CompDecorative>())
            {
                continue;
            }

            bodyType = BodyTypeUtils.SafeBodyType(pawn, wornApparel[i].def?.GetModExtension<DefModExtension_ForcesBodyType>()?.forcedBodyType);
            return true;
        }

        return false;
    }

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        if (bodyTypes.NullOrEmpty() || !TryGetWornBodyType(context.pawn, out var bodyType))
        {
            return true;
        }

        var met = BodyTypeUtils.MatchesAny(bodyType, bodyTypes, out var matched);
        report.Header("BEWH.Framework.Requirements.BodyTypeAtLeastOne".Translate());
        foreach (var listed in bodyTypes)
        {
            report.Line((listed.label ?? listed.defName).CapitalizeFirst(), listed == matched);
        }

        return met;
    }

    public override bool IsMet(RequirementContext context)
    {
        return bodyTypes.NullOrEmpty() || !TryGetWornBodyType(context.pawn, out var bodyType) || BodyTypeUtils.MatchesAny(bodyType, bodyTypes, out _);
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (bodyTypes.NullOrEmpty())
        {
            yield return $"{parent.defName} has a {GetType().Name} with nothing listed.";
        }
    }
}
