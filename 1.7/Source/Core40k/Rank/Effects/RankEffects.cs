using System.Collections.Generic;
using RimWorld;
using Verse;
using AbilityDef = RimWorld.AbilityDef;

namespace Core40k;

/// <summary>
/// Stat offsets and factors on the pawn while the rank is held (statOffsets / statFactors).
/// </summary>
public class RankEffect_Stats : RankEffect
{
    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];

    public override void Describe(EffectReport report)
    {
        report.Stats(statOffsets, statFactors);
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (statOffsets.NullOrEmpty() && statFactors.NullOrEmpty())
        {
            yield return $"{parent.defName} has a {GetType().Name} with no stats.";
        }
    }
}

/// <summary>
/// Abilities, vanilla and VEF, given with the rank and taken away with it (givesAbilities / givesVFEAbilities).
/// </summary>
public class RankEffect_Abilities : RankEffect
{
    public List<AbilityDef> abilities = [];
    public List<VEF.Abilities.AbilityDef> vfeAbilities = [];

    public override void Apply(CompRankInfo rankComp, RankDef rank)
    {
        rankComp.ParentPawn?.AddAbilities(abilities ?? [], vfeAbilities ?? []);
    }

    public override void Remove(CompRankInfo rankComp, RankDef rank)
    {
        rankComp.ParentPawn?.RemoveAbilities(abilities ?? [], vfeAbilities ?? []);
    }

    public override void Describe(EffectReport report)
    {
        report.Grants(abilities, vfeAbilities, null);
    }
}

/// <summary>
/// Hediffs added with the rank and removed with it (givesHediffs).
/// </summary>
public class RankEffect_Hediffs : RankEffect
{
    public List<HediffData> hediffs = [];

    public override void Apply(CompRankInfo rankComp, RankDef rank)
    {
        var pawn = rankComp.ParentPawn;
        if (pawn?.health == null || hediffs.NullOrEmpty())
        {
            return;
        }

        foreach (var hediffData in hediffs)
        {
            if (hediffData?.hediffDef == null)
            {
                continue;
            }

            var hediff = HediffMaker.MakeHediff(hediffData.hediffDef, pawn, pawn.health.hediffSet.GetBodyPartRecord(hediffData.bodyPartDef));
            hediff.Severity = hediffData.initialSeverity;
            pawn.health.AddHediff(hediff);
        }
    }

    public override void Remove(CompRankInfo rankComp, RankDef rank)
    {
        var hediffSet = rankComp.ParentPawn?.health?.hediffSet;
        if (hediffSet == null || hediffs.NullOrEmpty())
        {
            return;
        }

        foreach (var hediffData in hediffs)
        {
            //Already gone (surgery, another mod) is fine; RemoveHediff throws on null.
            var hediff = hediffData?.hediffDef == null ? null : hediffSet.GetFirstHediffOfDef(hediffData.hediffDef);
            if (hediff != null)
            {
                rankComp.ParentPawn.health.RemoveHediff(hediff);
            }
        }
    }

    public override void Describe(EffectReport report)
    {
        if (hediffs.NullOrEmpty())
        {
            return;
        }

        foreach (var hediffData in hediffs)
        {
            if (hediffData?.hediffDef != null)
            {
                report.Line(EffectReport.HediffsHeader, hediffData.hediffDef.LabelCap);
            }
        }
    }
}

/// <summary>
/// Skills whose training gives the pawn recreation (recreationFromSkills).
/// </summary>
public class RankEffect_RecreationFromSkills : RankEffect
{
    public List<SkillDef> skills = [];

    public override void Describe(EffectReport report)
    {
        report.Defs("BEWH.Framework.RankSystem.RecreationFromSkill".Translate(), skills);
    }
}

/// <summary>
/// Passion changes while the rank is held (givesPassions). The pawn's original passions come back when no held rank changes them any more.
/// </summary>
public class RankEffect_Passions : RankEffect
{
    public List<PassionMod> passions = [];

    public override void Apply(CompRankInfo rankComp, RankDef rank)
    {
        var skills = rankComp.ParentPawn?.skills;
        if (skills == null || passions.NullOrEmpty())
        {
            return;
        }

        foreach (var passion in passions)
        {
            var skill = passion?.skill == null ? null : skills.GetSkill(passion.skill);
            if (skill == null || rankComp.originalPassions.ContainsKey(skill.def))
            {
                continue;
            }

            rankComp.originalPassions.Add(skill.def, skill.passion);
            skill.passion = passion.NewPassionFor(skill);
        }
        rankComp.RecalculatePassions();
    }

    public override void Remove(CompRankInfo rankComp, RankDef rank)
    {
        if (!rankComp.originalPassions.NullOrEmpty())
        {
            rankComp.RecalculatePassions();
        }
    }

    public override void Describe(EffectReport report)
    {
        if (passions.NullOrEmpty())
        {
            return;
        }

        var header = "BEWH.Framework.RankSystem.PassionMod".Translate();
        foreach (var passionMod in passions)
        {
            if (passionMod?.skill == null)
            {
                continue;
            }

            var passionText = passionMod.skill.label.CapitalizeFirst();
            switch (passionMod.modType)
            {
                case PassionMod.PassionModType.AddOneLevel:
                    passionText += "BEWH.Framework.RankSystem.PassionModAddOne".Translate();
                    break;
                case PassionMod.PassionModType.DropAll:
                    passionText += "BEWH.Framework.RankSystem.PassionModDropAll".Translate();
                    break;
            }
            report.Line(header, passionText);
        }
    }
}

/// <summary>
/// Ranks taken away from the pawn when this one is gained (removeRanksOnUnlock), e.g. the rank this one replaces.
/// </summary>
public class RankEffect_RemoveRanks : RankEffect
{
    public List<RankDef> ranks = [];

    public override void Apply(CompRankInfo rankComp, RankDef rank)
    {
        if (ranks.NullOrEmpty())
        {
            return;
        }

        foreach (var rankDef in ranks)
        {
            if (rankDef != null && rankDef != rank)
            {
                rankComp.RemoveRank(rankDef, true);
            }
        }
    }
}

/// <summary>
/// Text only, listed under "Other effects" (customEffectDescriptions). For effects done elsewhere in code.
/// </summary>
public class RankEffect_Description : RankEffect
{
    [MustTranslate]
    public List<string> descriptions = [];

    public override void Describe(EffectReport report)
    {
        if (descriptions.NullOrEmpty())
        {
            return;
        }

        foreach (var description in descriptions)
        {
            report.Line(EffectReport.OtherEffectsHeader, description?.CapitalizeFirst());
        }
    }
}
