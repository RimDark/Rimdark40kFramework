using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using AbilityDef = RimWorld.AbilityDef;

namespace Core40k;

public class RankDef : Def
{
    [NoTranslate]
    public string rankIconPath;
        
    public List<RankDef> incompatibleRanks = [];
    public List<Aptitude> requiredSkills = [];
        
    public List<GeneDef> requiredGenesAll = [];
    public List<GeneDef> requiredGenesOneAmong = [];
        
    public List<TraitData> requiredTraitsAll = [];
    public List<TraitData> requiredTraitsOneAmong = [];
        
    public List<RoyalTitleRequirement> requiredTitlesAll = [];
    public List<RoyalTitleRequirement> requiredTitlesOneAmong = [];

    public List<HediffDef> requiredHediffsAll = [];
    public List<HediffDef> requiredHediffsOneAmong = [];

    public Vector2 requiredAgeRange = new Vector2(-1, -1);

    public Gender requiredGender = Gender.None;

    public bool slaveRank = false;

    //Extra requirements, checked after the ones built from the fields above.
    public List<PawnRequirement> requirements = [];

    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];
        
    public List<AbilityDef> givesAbilities = [];
    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];
    
    public List<HediffData> givesHediffs = [];
    
    public List<SkillDef> recreationFromSkills = [];
    
    public List<PassionMod> givesPassions = [];
        
    public List<string> customEffectDescriptions = [];

    public List<RankDef> removeRanksOnUnlock = [];

    //Extra effects, applied and described after the ones built from the givesX fields above.
    public List<RankEffect> effects = [];

    public Vector2 colonyLimitOfRank = new Vector2(-1, -1);

    public bool defaultFirstRank = false;

    public int rankTier = 0;
        
    public bool specialistRank = false;

    public string newPawnCardTitle = string.Empty;
        
    [Unsaved]
    private Texture2D rankIcon;

    public Texture2D RankIcon
    {
        get
        {
            if (rankIcon != null)
            {
                return rankIcon;
            }
                
            rankIcon = !rankIconPath.NullOrEmpty() ? ContentFinder<Texture2D>.Get(rankIconPath) : BaseContent.BadTex;
            return rankIcon;
        }
    }

    private static Game cachedGameForRankInfo = null;
    private static GameComponent_RankInfo gameCompRankInfo = null;
    
    internal static GameComponent_RankInfo GameCompRankInfo
    {
        get
        {
            if (gameCompRankInfo != null && cachedGameForRankInfo == Current.Game)
            {
                return gameCompRankInfo;
            }

            cachedGameForRankInfo = Current.Game;
            gameCompRankInfo = cachedGameForRankInfo?.GetComponent<GameComponent_RankInfo>();

            return gameCompRankInfo;
        }
    }
    
    [Unsaved]
    private List<RankEffect> allEffects;

    /// <summary>
    /// Every effect of this rank in order: the ones built from the givesX fields, then &lt;effects&gt;.
    /// Ranks in removeRanksOnUnlock are removed after the others are applied, and customEffectDescriptions come last.
    /// </summary>
    public List<RankEffect> AllEffects => allEffects ??= BuildEffectList();

    private List<RankEffect> BuildEffectList()
    {
        var list = new List<RankEffect>();

        if (!statOffsets.NullOrEmpty() || !statFactors.NullOrEmpty())
        {
            list.Add(new RankEffect_Stats { statOffsets = statOffsets ?? [], statFactors = statFactors ?? [] });
        }
        if (!givesAbilities.NullOrEmpty() || !givesVFEAbilities.NullOrEmpty())
        {
            list.Add(new RankEffect_Abilities { abilities = givesAbilities ?? [], vfeAbilities = givesVFEAbilities ?? [] });
        }
        if (!givesHediffs.NullOrEmpty())
        {
            list.Add(new RankEffect_Hediffs { hediffs = givesHediffs });
        }
        if (!recreationFromSkills.NullOrEmpty())
        {
            list.Add(new RankEffect_RecreationFromSkills { skills = recreationFromSkills });
        }
        if (!givesPassions.NullOrEmpty())
        {
            list.Add(new RankEffect_Passions { passions = givesPassions });
        }
        if (!effects.NullOrEmpty())
        {
            foreach (var effect in effects)
            {
                if (effect != null)
                {
                    list.Add(effect);
                }
            }
        }
        if (!removeRanksOnUnlock.NullOrEmpty())
        {
            list.Add(new RankEffect_RemoveRanks { ranks = removeRanksOnUnlock });
        }
        if (!customEffectDescriptions.NullOrEmpty())
        {
            list.Add(new RankEffect_Description { descriptions = customEffectDescriptions });
        }

        return list;
    }

    [Unsaved]
    private List<StatModifier> allStatOffsets;

    [Unsaved]
    private List<StatModifier> allStatFactors;

    [Unsaved]
    private List<SkillDef> allRecreationSkills;

    [Unsaved]
    private List<PassionMod> allPassions;

    /// <summary>
    /// Stat offsets from every RankEffect_Stats of this rank, statOffsets included.
    /// </summary>
    public List<StatModifier> AllStatOffsets => allStatOffsets ??= Collect<RankEffect_Stats, StatModifier>(effect => effect.statOffsets);

    /// <summary>
    /// Stat factors from every RankEffect_Stats of this rank, statFactors included.
    /// </summary>
    public List<StatModifier> AllStatFactors => allStatFactors ??= Collect<RankEffect_Stats, StatModifier>(effect => effect.statFactors);

    public List<SkillDef> AllRecreationSkills => allRecreationSkills ??= Collect<RankEffect_RecreationFromSkills, SkillDef>(effect => effect.skills);

    public List<PassionMod> AllPassions => allPassions ??= Collect<RankEffect_Passions, PassionMod>(effect => effect.passions);

    private List<TItem> Collect<TEffect, TItem>(Func<TEffect, List<TItem>> items) where TEffect : RankEffect
    {
        var result = new List<TItem>();
        foreach (var effect in AllEffects)
        {
            if (effect is TEffect typed && items(typed) is { } list)
            {
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }
            }
        }

        return result;
    }

    public float StatOffsetFor(StatDef stat)
    {
        return AllStatOffsets.Count == 0 ? 0f : AllStatOffsets.GetStatOffsetFromList(stat);
    }

    public float StatFactorFor(StatDef stat)
    {
        return AllStatFactors.Count == 0 ? 1f : AllStatFactors.GetStatFactorFromList(stat);
    }

    /// <summary>
    /// Applies AllEffects. Overrides should call base, or the rank's effects are not given.
    /// </summary>
    public virtual void UnlockRank(CompRankInfo rankComp)
    {
        foreach (var effect in AllEffects)
        {
            effect.Apply(rankComp, this);
        }
    }

    /// <summary>
    /// Removes AllEffects. Overrides should call base, or the rank's effects stay on the pawn.
    /// </summary>
    public virtual void RemoveRank(CompRankInfo rankComp)
    {
        foreach (var effect in AllEffects)
        {
            effect.Remove(rankComp, this);
        }
    }

    [Unsaved]
    private List<PawnRequirement> allRequirements;

    /// <summary>
    /// Every requirement of this rank in display order: the ones built from the requiredX fields, then &lt;requirements&gt;.
    /// </summary>
    public List<PawnRequirement> AllRequirements => allRequirements ??= BuildRequirementList();

    private List<PawnRequirement> BuildRequirementList()
    {
        var list = new List<PawnRequirement>();

        if (RankUtils.IsLimited(this))
        {
            list.Add(new RankRequirement_ColonyLimit());
        }
        if (!requiredSkills.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Skills { skills = requiredSkills.Where(aptitude => aptitude?.skill != null).Select(aptitude => new SkillLevelRange(aptitude.skill, aptitude.level)).ToList() });
        }
        list.Add(new RankRequirement_CategoryRanks());
        if (!requiredTraitsAll.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Traits { traits = requiredTraitsAll });
        }
        if (!requiredTraitsOneAmong.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Traits { traits = requiredTraitsOneAmong, oneAmong = true });
        }
        if (!requiredTitlesAll.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Titles { titles = requiredTitlesAll });
        }
        if (!requiredTitlesOneAmong.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Titles { titles = requiredTitlesOneAmong, oneAmong = true });
        }
        //Rank gene fields have always let a pawn without a gene tracker through "all".
        if (!requiredGenesAll.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Genes { genes = requiredGenesAll, passWithoutGeneTracker = true });
        }
        if (!requiredGenesOneAmong.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Genes { genes = requiredGenesOneAmong, oneAmong = true, passWithoutGeneTracker = true });
        }
        if (!requiredHediffsAll.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Hediffs { hediffs = requiredHediffsAll });
        }
        if (!requiredHediffsOneAmong.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Hediffs { hediffs = requiredHediffsOneAmong, oneAmong = true });
        }
        if (requiredAgeRange.x >= 0 || requiredAgeRange.y >= 0)
        {
            list.Add(new PawnRequirement_Age { minAge = requiredAgeRange.x, maxAge = requiredAgeRange.y });
        }
        if (requiredGender != Gender.None)
        {
            list.Add(new PawnRequirement_Gender { gender = requiredGender });
        }
        if (slaveRank)
        {
            list.Add(new PawnRequirement_SlaveOfColony());
        }
        if (!incompatibleRanks.NullOrEmpty())
        {
            list.Add(new PawnRequirement_IncompatibleRanks { ranks = incompatibleRanks });
        }
        RequirementUtility.AddListed(list, requirements);

        return list;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
        {
            yield return error;
        }

        foreach (var error in RequirementUtility.ConfigErrors(this, requirements))
        {
            yield return error;
        }

        if (effects == null)
        {
            yield break;
        }

        foreach (var effect in effects)
        {
            if (effect == null)
            {
                yield return $"{defName} has an empty entry in effects.";
                continue;
            }

            foreach (var error in effect.ConfigErrors(this))
            {
                yield return error;
            }
        }
    }

    [Unsaved]
    private bool? overridesRequirementMet;

    //Set by the base RequirementMet, so the checks below can tell when an override skipped it.
    [Unsaved]
    private bool requirementsEvaluated;

    private static readonly StringBuilder DiscardedRequirementText = new();

    private bool OverridesRequirementMet => overridesRequirementMet ??= GetType().GetMethod(nameof(RequirementMet), [typeof(StringBuilder), typeof(Pawn), typeof(CompRankInfo), typeof(RankCategoryDef), typeof(string).MakeByRefType()])?.DeclaringType != typeof(RankDef);

    /// <summary>
    /// Whether the pawn meets every requirement of this rank in the given category, without building any text.
    /// </summary>
    public bool RequirementMet(Pawn pawn, CompRankInfo rankComp, RankCategoryDef rankCategoryDef)
    {
        if (!OverridesRequirementMet)
        {
            return RequirementsMet(new RequirementContext(pawn, rankComp, this, rankCategoryDef), null);
        }

        //Older overrides may not expect a null StringBuilder, so they get one whose text is thrown away.
        DiscardedRequirementText.Clear();
        requirementsEvaluated = false;
        var met = RequirementMet(DiscardedRequirementText, pawn, rankComp, rankCategoryDef, out _);
        if (met && !requirementsEvaluated)
        {
            //The override skipped base.RequirementMet; the rank's own requirements still apply.
            met = RequirementsMet(new RequirementContext(pawn, rankComp, this, rankCategoryDef), null);
        }

        return met;
    }

    /// <summary>
    /// Whether the pawn meets every requirement of this rank in the given category, with the requirement text for the rank tab.
    /// </summary>
    public bool RequirementMet(Pawn pawn, CompRankInfo rankComp, RankCategoryDef rankCategoryDef, out string reason)
    {
        var stringBuilder = new StringBuilder();
        requirementsEvaluated = false;
        var met = RequirementMet(stringBuilder, pawn, rankComp, rankCategoryDef, out reason);
        if (!requirementsEvaluated)
        {
            //The override skipped base.RequirementMet; the rank's own requirements still apply.
            var requirementsMet = RequirementsMet(new RequirementContext(pawn, rankComp, this, rankCategoryDef), new RequirementReport(stringBuilder));
            met = met && requirementsMet;
            reason = RequirementReport.Finish(stringBuilder);
        }

        return met;
    }

    /// <summary>
    /// Adds a section header to the requirement text, spaced like the framework's own headers. Does nothing when stringBuilder is null.
    /// For RequirementMet overrides; new requirements are better written as a PawnRequirement.
    /// </summary>
    public static void AppendRequirementHeader(StringBuilder stringBuilder, string header)
    {
        new RequirementReport(stringBuilder).Header(header);
    }

    /// <summary>
    /// Adds one requirement line, indented and coloured by whether it is met, like the framework's own lines.
    /// For RequirementMet overrides: append before calling base so the line ends up in the reason text. Does nothing when stringBuilder is null.
    /// </summary>
    public static void AppendRequirement(StringBuilder stringBuilder, string text, bool met)
    {
        stringBuilder?.AppendLine(RequirementReport.FormatLine(text, met));
    }

    /// <summary>
    /// Checks AllRequirements. With a null stringBuilder only the result is worked out; otherwise the requirement text is added to it and returned in reason.
    /// Overriding this is the older way to add requirements; a PawnRequirement in &lt;requirements&gt; is preferred.
    /// </summary>
    public virtual bool RequirementMet(StringBuilder stringBuilder, Pawn pawn, CompRankInfo rankComp, RankCategoryDef currentlySelectedRankCategory, out string reason)
    {
        requirementsEvaluated = true;
        var context = new RequirementContext(pawn, rankComp, this, currentlySelectedRankCategory);

        if (stringBuilder == null)
        {
            reason = null;
            return RequirementsMet(context, null);
        }

        var met = RequirementsMet(context, new RequirementReport(stringBuilder));
        reason = RequirementReport.Finish(stringBuilder);
        return met;
    }

    //A null report only checks, stopping at the first requirement that fails.
    private bool RequirementsMet(RequirementContext context, RequirementReport report)
    {
        return report == null
            ? RequirementUtility.AllMet(AllRequirements, context)
            : RequirementUtility.DescribeAll(AllRequirements, context, report);
    }

    /// <summary>
    /// Writes what the rank does into report: every effect's Describe. Override to add lines of your own (call base).
    /// </summary>
    public virtual void DescribeEffects(EffectReport report)
    {
        foreach (var effect in AllEffects)
        {
            effect.Describe(report);
        }
    }

    /// <summary>
    /// The rank tab's bonus text, built from DescribeEffects. stringBuilder is not used; it is kept for older overrides.
    /// </summary>
    public virtual string BuildRankBonusString(StringBuilder stringBuilder)
    {
        var report = new EffectReport();
        DescribeEffects(report);

        var builder = new StringBuilder();
        report.AppendTo(builder, leadingGap: false);
        return builder.ToString();
    }

    public string GetRankBonusString()
    {
        var bonuses = BuildRankBonusString(new StringBuilder());
        if (bonuses.NullOrEmpty())
        {
            return"    " + "BEWH.Framework.CommonKeyword.None".Translate();
        }

        return bonuses;
    }
    
    public virtual void Notify_Killed(CompRankInfo rankComp, Map prevMap, DamageInfo? dinfo = null)
    {
        foreach (var effect in AllEffects)
        {
            effect.Notify_Killed(rankComp, this, prevMap, dinfo);
        }
    }
}
