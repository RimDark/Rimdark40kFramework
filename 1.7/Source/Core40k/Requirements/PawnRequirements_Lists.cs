using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Skill levels, each within its listed range: all of them, with oneAmong at least one, or with minCount at least that many.
/// With useSum the listed skills' levels are added up instead and the total must be within sumMin / sumMax.
/// </summary>
public class PawnRequirement_Skills : PawnRequirement_List<SkillLevelRange>
{
    public List<SkillLevelRange> skills = [];

    public bool useSum = false;

    //Bounds on the summed levels with useSum; -1 means no bound on that side.
    public int sumMin = -1;
    public int sumMax = -1;

    protected override List<SkillLevelRange> Items => skills;
    protected override string HeaderAllKey => "BEWH.Framework.RankSystem.RequirementsSkills";
    protected override string HeaderOneAmongKey => "BEWH.Framework.Requirements.SkillsAtLeastOne";

    private static int LevelOf(RequirementContext context, SkillDef skillDef)
    {
        return context.pawn?.skills?.GetSkill(skillDef)?.Level ?? 0;
    }

    protected override bool Has(RequirementContext context, SkillLevelRange item)
    {
        var skill = context.pawn.skills?.GetSkill(item.skill);
        return skill != null && item.InRange(skill.Level);
    }

    protected override string LabelFor(RequirementContext context, SkillLevelRange item)
    {
        var range = item.RangeText;
        return range.NullOrEmpty() ? item.skill.LabelCap.ToString() : item.skill.LabelCap + ": " + range;
    }

    private int Sum(RequirementContext context)
    {
        var sum = 0;
        foreach (var item in skills)
        {
            if (item?.skill != null)
            {
                sum += LevelOf(context, item.skill);
            }
        }

        return sum;
    }

    private bool SumInRange(int sum)
    {
        return (sumMin < 0 || sum >= sumMin) && (sumMax < 0 || sum <= sumMax);
    }

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        if (!useSum)
        {
            return base.Describe(context, report);
        }

        if (skills.NullOrEmpty())
        {
            return true;
        }

        report.Header("BEWH.Framework.Requirements.SkillsSum".Translate());
        var sum = Sum(context);
        var met = context.pawn != null && SumInRange(sum);
        foreach (var item in skills)
        {
            if (item?.skill != null)
            {
                //The skills only add up, so each line is white; the total line carries the colour.
                report.Line(item.skill.LabelCap + ": " + LevelOf(context, item.skill), true);
            }
        }
        report.Line("BEWH.Framework.Requirements.SkillsSumTotal".Translate(sum, SkillLevelRange.FormatRange(Math.Max(sumMin, 0), sumMax)), met);
        return met;
    }

    public override bool IsMet(RequirementContext context)
    {
        if (!useSum)
        {
            return base.IsMet(context);
        }

        return skills.NullOrEmpty() || (context.pawn != null && SumInRange(Sum(context)));
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        foreach (var error in base.ConfigErrors(parent))
        {
            yield return error;
        }

        if (skills != null && skills.Any(item => item?.skill == null))
        {
            yield return $"{parent.defName} has a {GetType().Name} with an entry that names no skill.";
        }

        if (skills != null && skills.Any(item => item != null && item.maxLevel >= 0 && item.maxLevel < item.level))
        {
            yield return $"{parent.defName} has a {GetType().Name} with a maximum level below its minimum.";
        }

        if (!useSum)
        {
            yield break;
        }

        if (sumMin < 0 && sumMax < 0)
        {
            yield return $"{parent.defName} has a {GetType().Name} with useSum but neither sumMin nor sumMax.";
        }
        if (oneAmong || minCount > 0 || (skills != null && skills.Any(item => item != null && (item.level > 0 || item.maxLevel >= 0))))
        {
            yield return $"{parent.defName} has a {GetType().Name} with useSum and per-skill levels, oneAmong or minCount, which are ignored with useSum.";
        }
    }
}

/// <summary>
/// Ranks the pawn must hold, optionally for a number of days (scaled by BEWH_RankLearningFactor).
/// </summary>
public class PawnRequirement_Ranks : PawnRequirement_List<RankData>
{
    public List<RankData> ranks = [];

    //ranks without the entries whose rankDef didn't resolve (e.g. a rank from a mod that isn't loaded). Built on first use.
    [Unsaved]
    private List<RankData> resolvedRanks;

    protected override List<RankData> Items => resolvedRanks ??= ranks?.Where(item => item?.rankDef != null).ToList();
    protected override string HeaderAllKey => "BEWH.Framework.RankSystem.RequirementsRanks";
    protected override string HeaderOneAmongKey => "BEWH.Framework.RankSystem.RequirementsRanksAtLeastOne";

    protected override bool Has(RequirementContext context, RankData item)
    {
        var rankComp = context.RankComp;
        if (rankComp == null || !rankComp.HasRank(item.rankDef))
        {
            return false;
        }

        return item.daysAs <= 0 || rankComp.GetDaysAsRank(item.rankDef) >= RequiredDays(context, item);
    }

    protected override string LabelFor(RequirementContext context, RankData item)
    {
        var rankLabel = item.rankDef.label.CapitalizeFirst();
        return item.daysAs > 0
            ? "BEWH.Framework.RankSystem.HaveBeenRankForDays".Translate(rankLabel, RequiredDays(context, item))
            : "BEWH.Framework.RankSystem.HaveAchievedRank".Translate(rankLabel);
    }

    private static double RequiredDays(RequirementContext context, RankData item)
    {
        return Math.Round(item.daysAs / context.RankLearningFactor);
    }
}

/// <summary>
/// Traits at a given degree.
/// </summary>
public class PawnRequirement_Traits : PawnRequirement_List<TraitData>
{
    public List<TraitData> traits = [];

    protected override List<TraitData> Items => traits;
    protected override string HeaderAllKey => "BEWH.Framework.RankSystem.RequirementsTraitAll";
    protected override string HeaderOneAmongKey => "BEWH.Framework.RankSystem.RequirementsTraitAtLeastOne";

    protected override bool Has(RequirementContext context, TraitData item)
    {
        return context.pawn.story?.traits != null && context.pawn.story.traits.HasTrait(item.traitDef, item.degree);
    }

    protected override string LabelFor(RequirementContext context, TraitData item)
    {
        return item.traitDef.DataAtDegree(item.degree).label.CapitalizeFirst();
    }
}

/// <summary>
/// Royal titles. Ignored entirely while Royalty is not active.
/// </summary>
public class PawnRequirement_Titles : PawnRequirement_List<RoyalTitleRequirement>
{
    public List<RoyalTitleRequirement> titles = [];

    protected override List<RoyalTitleRequirement> Items => ModsConfig.RoyaltyActive ? titles : null;
    protected override string HeaderAllKey => "BEWH.Framework.RankSystem.RequirementsTitleAll";
    protected override string HeaderOneAmongKey => "BEWH.Framework.RankSystem.RequirementsTitleAtLeastOne";

    protected override bool Has(RequirementContext context, RoyalTitleRequirement item)
    {
        return item.MetBy(context.pawn);
    }

    protected override string LabelFor(RequirementContext context, RoyalTitleRequirement item)
    {
        return item.Label;
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (titles.NullOrEmpty())
        {
            yield return $"{parent.defName} has a {GetType().Name} with nothing listed.";
        }
    }
}

/// <summary>
/// Active genes. A pawn without a gene tracker has none of them, unless passWithoutGeneTracker is set: then "all" passes
/// (and "one among" still fails), which is how the RankDef gene fields have always behaved.
/// </summary>
public class PawnRequirement_Genes : PawnRequirement_List<GeneDef>
{
    public List<GeneDef> genes = [];

    public bool passWithoutGeneTracker = false;

    protected override List<GeneDef> Items => genes;
    protected override string HeaderAllKey => "BEWH.Framework.RankSystem.RequirementsGeneAll";
    protected override string HeaderOneAmongKey => "BEWH.Framework.RankSystem.RequirementsGeneAtLeastOne";

    protected override bool Has(RequirementContext context, GeneDef item)
    {
        return context.pawn.genes != null && context.pawn.genes.HasActiveGene(item);
    }

    protected override string LabelFor(RequirementContext context, GeneDef item)
    {
        return item.label.CapitalizeFirst();
    }

    private bool SkipsGenelessPawn(RequirementContext context)
    {
        return passWithoutGeneTracker && context.pawn != null && context.pawn.genes == null;
    }

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        return SkipsGenelessPawn(context) ? !oneAmong || genes.NullOrEmpty() : base.Describe(context, report);
    }

    public override bool IsMet(RequirementContext context)
    {
        return SkipsGenelessPawn(context) ? !oneAmong || genes.NullOrEmpty() : base.IsMet(context);
    }
}

/// <summary>
/// Hediffs the pawn must have.
/// </summary>
public class PawnRequirement_Hediffs : PawnRequirement_List<HediffDef>
{
    public List<HediffDef> hediffs = [];

    protected override List<HediffDef> Items => hediffs;
    protected override string HeaderAllKey => "BEWH.Framework.RankSystem.RequirementsHediffAll";
    protected override string HeaderOneAmongKey => "BEWH.Framework.RankSystem.RequirementsHediffAtLeastOne";

    protected override bool Has(RequirementContext context, HediffDef item)
    {
        return context.pawn.health?.hediffSet != null && context.pawn.health.hediffSet.HasHediff(item);
    }

    protected override string LabelFor(RequirementContext context, HediffDef item)
    {
        return item.label.CapitalizeFirst();
    }
}

/// <summary>
/// Finished research projects. Doesn't look at the pawn, so it also applies when nobody is being judged.
/// </summary>
public class PawnRequirement_Research : PawnRequirement_List<ResearchProjectDef>
{
    public List<ResearchProjectDef> projects = [];

    public override bool DependsOnPawn => false;

    protected override List<ResearchProjectDef> Items => projects;
    protected override string HeaderAllKey => "BEWH.Framework.Requirements.ResearchAll";
    protected override string HeaderOneAmongKey => "BEWH.Framework.Requirements.ResearchAtLeastOne";

    protected override bool Has(RequirementContext context, ResearchProjectDef item)
    {
        return item.IsFinished;
    }

    protected override string LabelFor(RequirementContext context, ResearchProjectDef item)
    {
        return item.LabelCap;
    }
}
