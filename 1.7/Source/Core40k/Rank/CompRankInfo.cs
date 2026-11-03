using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

public class CompRankInfo : ThingComp
{
    private const int TicksPerDay = 60000;
    public CompProperties_RankInfo Props => (CompProperties_RankInfo)props;
        
    private List<RankDef> unlockedRanks = [];

    public List<RankDef> unlockedRanksAtDeath = [];

    private HashSet<RankDef> limitCountedRanks = [];

    public HashSet<RankDef> LimitCountedRanks => limitCountedRanks ??= [];

    private HashSet<RankDef> announcedEligibleRanks = [];

    public bool HasAnnouncedEligibility(RankDef rankDef)
    {
        return announcedEligibleRanks != null && announcedEligibleRanks.Contains(rankDef);
    }

    public void MarkEligibilityAnnounced(RankDef rankDef)
    {
        announcedEligibleRanks ??= [];
        announcedEligibleRanks.Add(rankDef);
    }

    public List<RankDef> UnlockedRanks => unlockedRanks ??= [];

    private DevotionState devotion;

    public DevotionState Devotion
    {
        get
        {
            if (devotion != null)
            {
                return devotion;
            }

            devotion = new DevotionState();
            devotion.SetParent(this);
            return devotion;
        }
    }

    public bool HasAnyDevotion => devotion is { HasAnyDevotion: true };

    public IReadOnlyList<DevotionCreedDef> ActiveCreeds => devotion == null ? Array.Empty<DevotionCreedDef>() : devotion.ActiveCreeds;

    public DevotionCreedDef DominantCreed => devotion?.DominantCreed;

    public DevotionFaithDef DominantFaith => devotion?.DominantFaith;

    public float GetStanding(DevotionCreedDef creed) => devotion?.GetStanding(creed) ?? 0f;

    public float GetStandingPct(DevotionCreedDef creed) => devotion?.GetStandingPct(creed) ?? 0f;

    public float AddStanding(DevotionCreedDef creed, float amount) => Devotion.AddStanding(creed, amount);

    public void SetStanding(DevotionCreedDef creed, float value, bool silent = false) => Devotion.SetStanding(creed, value, silent);

    public void TickDevotionDecay(int interval) => devotion?.TickDecay(interval);

    public int TierIndexOf(DevotionCreedDef creed) => devotion?.TierIndexOf(creed) ?? -1;

    public DevotionTierDef TierOf(DevotionCreedDef creed) => devotion?.TierOf(creed);

    public int HighestTierReached(DevotionCreedDef creed) => devotion?.HighestTierReached(creed) ?? -1;

    public bool TryPurge(DevotionCreedDef creed, out string reason)
    {
        if (devotion == null)
        {
            reason = "BEWH.Framework.Devotion.NoStanding".Translate();
            return false;
        }

        return devotion.TryPurge(creed, out reason);
    }

    public void ClearAllDevotion() => devotion?.ClearAllDevotion();

    private RankCategoryDef lastOpenedRankCategory = null;
        
    public RankCategoryDef LastOpenedRankCategory => lastOpenedRankCategory;
    
    private Dictionary<RankDef, int> daysAsRank = new Dictionary<RankDef, int>();
    
    public Dictionary<SkillDef, Passion> originalPassions = new Dictionary<SkillDef, Passion>();

    private bool migratedLimitCounts;

    private GameComponent_RankInfo gameComponentRankInfo = null;

    public GameComponent_RankInfo GameComponentRankInfo => gameComponentRankInfo ??= Current.Game.GetComponent<GameComponent_RankInfo>();
    
    private HashSet<SkillDef> cachedRecreationSkills;

    public HashSet<SkillDef> RecreationSkillsFromRanks
    {
        get
        {
            if (cachedRecreationSkills != null)
            {
                return cachedRecreationSkills;
            }

            cachedRecreationSkills = [];
            foreach (var rank in UnlockedRanks)
            {
                if (rank == null)
                {
                    continue;
                }

                foreach (var skill in rank.AllRecreationSkills)
                {
                    cachedRecreationSkills.Add(skill);
                }
            }

            return cachedRecreationSkills;
        }
    }

    public void InvalidateCaches()
    {
        cachedRecreationSkills = null;
        cachedStatOffset = new Dictionary<StatDef, float>();
        cachedStatFactor = new Dictionary<StatDef, float>();
    }

    public Pawn ParentPawn => parent as Pawn;

    private ApparelSetState apparelSets;

    private bool apparelSetsChecked;

    public ApparelSetState ApparelSets => apparelSets ??= new ApparelSetState(this);

    public ApparelSetState ApparelSetsIfAny
    {
        get
        {
            if (apparelSets != null || apparelSetsChecked)
            {
                return apparelSets;
            }

            apparelSetsChecked = true;
            if (ApparelSetIndex.AnySets && HasAnySetPiece())
            {
                apparelSets = new ApparelSetState(this);
            }

            return apparelSets;
        }
    }

    private bool HasAnySetPiece()
    {
        var worn = ParentPawn?.apparel?.WornApparel;
        if (worn != null)
        {
            foreach (var apparel in worn)
            {
                if (ApparelSetIndex.IsPiece(apparel?.def) || ApparelSetIndex.CarriesSetDecoration(apparel))
                {
                    return true;
                }
            }
        }

        var equipment = ParentPawn?.equipment?.AllEquipmentListForReading;
        if (equipment != null)
        {
            foreach (var eq in equipment)
            {
                if (ApparelSetIndex.IsPiece(eq?.def) || ApparelSetIndex.CarriesSetDecoration(eq))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void Notify_ApparelChanged()
    {
        Notify_GearChanged();
    }

    /// <summary>
    /// Rebuilds the apparel set state after worn apparel, equipment or fitted decorations changed.
    /// </summary>
    public void Notify_GearChanged(bool notify = true)
    {
        if (!ApparelSetIndex.AnySets)
        {
            return;
        }

        apparelSetsChecked = true;
        ApparelSets.Notify_GearChanged(notify);
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (!ApparelSetIndex.AnySets)
        {
            return;
        }

        apparelSetsChecked = true;
        ApparelSets.Notify_GearChanged(false);
    }

    private Dictionary<StatDef, float> cachedStatOffset = new Dictionary<StatDef, float>();
    public Dictionary<StatDef, float> CachedStatOffset => cachedStatOffset;
    
    private Dictionary<StatDef, float> cachedStatFactor = new Dictionary<StatDef, float>();
    public Dictionary<StatDef, float> CachedStatFactor => cachedStatFactor;
    
    public void UnlockRank(RankDef rank)
    {
        UnlockRank(rank, true);
    }

    public void UnlockRank(RankDef rank, bool countTowardsLimit)
    {
        if (UnlockedRanks.Contains(rank))
        {
            return;
        }

        if (ParentPawn == null)
        {
            return;
        }
            
        if (rank.rankTier > HighestRank() && ParentPawn.story != null)
        {
            ParentPawn.story.Title = rank.newPawnCardTitle == string.Empty ? rank.label : rank.newPawnCardTitle;
        }
            
        UnlockedRanks.Add(rank);
            
        if (!daysAsRank.ContainsKey(rank))
        {
            daysAsRank.Add(rank, Find.TickManager?.TicksGame ?? 0);
        }
        
        rank.UnlockRank(this);

        if (RankUtils.IsLimited(rank))
        {
            GameComponentRankInfo.Notify_RankHoldersChanged();
        }

        InvalidateCaches();
    }

    public void RemoveRank(RankDef rank, bool removeFromRankLimit)
    {
        //A rank the pawn never held has nothing to take back (removeRanksOnUnlock lists ranks the pawn may not have).
        if (!HasRank(rank))
        {
            return;
        }

        UnlockedRanks.Remove(rank);
        daysAsRank.Remove(rank);
            
        if (parent is not Pawn pawn)
        {
            return;
        }
            
        rank.RemoveRank(this);

        LimitCountedRanks.Remove(rank);

        if (RankUtils.IsLimited(rank))
        {
            GameComponentRankInfo.Notify_RankHoldersChanged();
        }

        if (pawn.story != null)
        {
            var newHighestRank = HighestRankDef(false);
            pawn.story.Title = newHighestRank == null ? null : newHighestRank.newPawnCardTitle == string.Empty ? newHighestRank.label : newHighestRank.newPawnCardTitle;
        }
        
        InvalidateCaches();
    }
    
    public void RecalculatePassions()
    {
        foreach (var originalPassion in originalPassions)
        {
            var skill = ParentPawn.skills.GetSkill(originalPassion.Key);
            skill.passion = originalPassion.Value;
        }
        var passionMods = UnlockedRanks.SelectMany(def => def.AllPassions);
        var skillDefPassionCol = new Dictionary<SkillDef, List<PassionMod.PassionModType>>();
        foreach (var passionMod in passionMods)
        {
            if (!skillDefPassionCol.ContainsKey(passionMod.skill))
            {
                skillDefPassionCol.Add(passionMod.skill, [passionMod.modType]);
            }
            else
            {
                skillDefPassionCol[passionMod.skill].Add(passionMod.modType);
            }
        }

        foreach (var col in skillDefPassionCol)
        {
            var skill = ParentPawn.skills.GetSkill(col.Key);
            if (col.Value.NullOrEmpty())
            {
                if (originalPassions.TryGetValue(col.Key, out var original))
                {
                    skill.passion = original;
                    originalPassions.Remove(col.Key);
                }
                continue;
            }
            
            if (col.Value.Any(type => type == PassionMod.PassionModType.DropAll))
            {
                skill.passion = Passion.None;
                continue;
            }

            foreach (var passion in col.Value)
            {
                if (passion == PassionMod.PassionModType.AddOneLevel)
                {
                    skill.passion = skill.passion switch
                    {
                        Passion.None => Passion.Minor,
                        Passion.Minor => Passion.Major,
                        _ => skill.passion
                    };
                }
            }
        }
    }

    public int HighestRank()
    {
        if (UnlockedRanks.NullOrEmpty())
        {
            return -1;
        }
            
        return UnlockedRanks.MaxBy(rank => rank.rankTier).rankTier;
    }

    public float GetDaysAsRank(RankDef rankDef)
    {
        if (daysAsRank.TryGetValue(rankDef, out var days))
        {
            return Math.Abs((float)(days - Find.TickManager.TicksGame)) / TicksPerDay;
        }
        
        return 0f;
    }
    
    public int HighestRank(RankCategoryDef rankCategoryDef)
    {
        if (UnlockedRanks.NullOrEmpty())
        {
            return -1;
        }

        var unlockedRanksOfDef = UnlockedRanksOfDef(rankCategoryDef).Where(HasRank).Select(rankDef => rankDef).ToList();
        if (unlockedRanksOfDef.NullOrEmpty())
        {
            return -1;
        }
            
        return unlockedRanksOfDef.MaxBy(rank => rank.rankTier).rankTier;
    }
        
    public RankDef HighestRankDef(bool onlySpecialist, RankCategoryDef rankCategoryDef)
    {
        
        var list = UnlockedRanksOfDef(rankCategoryDef).Where(def => !onlySpecialist || def.specialistRank).ToList();
            
        return list.NullOrEmpty() ? null : list.MaxBy(rank => rank.rankTier);
    }
    
    public RankDef HighestRankDef(bool onlySpecialist)
    {
        var list = UnlockedRanks.Where(def => !onlySpecialist || def.specialistRank).ToList();
            
        return list.NullOrEmpty() ? null : list.MaxBy(rank => rank.rankTier);
    }

    public List<RankDef> UnlockedRanksOfDef(RankCategoryDef rankCategoryDef)
    {
        var unlockedRanksOfDef = rankCategoryDef.ranks.Where(data => HasRank(data.rankDef)).Select(data => data.rankDef).ToList();
        return unlockedRanksOfDef;
    }

    public bool HasRankOfCategory(RankCategoryDef rankCategoryDef)
    {
        return UnlockedRanksOfDef(rankCategoryDef).Any();
    }

    public void ResetRanks(RankCategoryDef rankCategoryDef)
    {
        var ranksToRemove = rankCategoryDef != null
            ? UnlockedRanksOfDef(rankCategoryDef)
            : UnlockedRanks.ToList();

        foreach (var rankDef in ranksToRemove)
        {
            RemoveRank(rankDef, true);

            announcedEligibleRanks?.Remove(rankDef);
        }
    }

    public bool HasRank(RankDef rankDef)
    {
        return UnlockedRanks.Contains(rankDef);
    }
        
    public void OpenedRankCategory(RankCategoryDef rankCategory)
    {
        lastOpenedRankCategory = rankCategory;
    }
        
    public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
    {
        base.Notify_Killed(prevMap, dinfo);
        foreach (var rank in UnlockedRanks)
        {
            rank.Notify_Killed(this, prevMap, dinfo);
        }
        LimitCountedRanks.Clear();
        GameComponentRankInfo.Notify_RankHoldersChanged();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        LimitCountedRanks.Clear();
        GameComponentRankInfo.Notify_RankHoldersChanged();
    }

    public void IncreaseDaysForAllRank()
    {
        var daysAsRankTemp = daysAsRank.ToList();
        foreach (var rank in daysAsRankTemp)
        {
            IncreaseDaysAsRank(rank.Key);
        }
    }
        
    public void IncreaseDaysAsRank(RankDef rankDef)
    {
        daysAsRank[rankDef] -= TicksPerDay;
    }
    
    public void SetDaysAsRank(RankDef rankDef, float days)
    {
        daysAsRank[rankDef] = (Find.TickManager?.TicksGame ?? 0) - (int)(days * TicksPerDay);
    }
        
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref unlockedRanks, "unlockedRanks", LookMode.Def);
        Scribe_Collections.Look(ref unlockedRanksAtDeath, "unlockedRanksAtDeath", LookMode.Def);
        Scribe_Collections.Look(ref limitCountedRanks, "limitCountedRanks", LookMode.Def);
        Scribe_Collections.Look(ref announcedEligibleRanks, "announcedEligibleRanks", LookMode.Def);
        Scribe_Collections.Look(ref daysAsRank, "daysAsRank");
        Scribe_Collections.Look(ref originalPassions, "originalPassions", LookMode.Def, LookMode.Value);
        Scribe_Defs.Look(ref lastOpenedRankCategory, "lastOpenedRankCategory");
        Scribe_Values.Look(ref migratedLimitCounts, "migratedLimitCounts");
        Scribe_Deep.Look(ref devotion, "devotion");

        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }
            
        //A removed content mod leaves null entries behind; they are dropped here once rather
        //than checked on every read.
        unlockedRanks ??= [];
        unlockedRanks.RemoveAll(def => def == null);
        daysAsRank ??= new Dictionary<RankDef, int>();
        originalPassions ??= new Dictionary<SkillDef, Passion>();
        limitCountedRanks ??= [];
        announcedEligibleRanks ??= [];

        if (devotion != null)
        {
            devotion.SetParent(this);
            devotion.PostLoadInit();
        }

        if (!migratedLimitCounts)
        {
            migratedLimitCounts = true;

            if (limitCountedRanks.Count == 0 && !UnlockedRanks.NullOrEmpty() && ParentPawn?.Faction is { IsPlayer: true })
            {
                limitCountedRanks.AddRange(UnlockedRanks);
            }
        }
    }
    
    //StatOffset
    public override float GetStatOffset(StatDef stat)
    {
        var num = 0f;
        if (CachedStatOffset.TryGetValue(stat, out var cachedStatOffsetOut))
        {
            num += cachedStatOffsetOut;
        }
        else
        {
            foreach (var rank in UnlockedRanks)
            {
                num += rank.StatOffsetFor(stat);
            }

            if (devotion != null)
            {
                num += devotion.GetStatOffset(stat);
            }

            CachedStatOffset.Add(stat, num);
        }

        var sets = ApparelSetsIfAny;
        if (sets != null)
        {
            num += sets.GetStatOffset(stat);
        }

        return num;
    }
    
    //Stat Factor
    public override float GetStatFactor(StatDef stat)
    {
        var num = 1f;
        
        if (CachedStatFactor.TryGetValue(stat, out var cachedStatFactorOut))
        {
            num *= cachedStatFactorOut;
        }
        else
        {
            foreach (var rank in UnlockedRanks)
            {
                num *= rank.StatFactorFor(stat);
            }
                    
            if (devotion != null)
            {
                num *= devotion.GetStatFactor(stat);
            }

            CachedStatFactor.Add(stat, num);
        }

        var sets = ApparelSetsIfAny;
        if (sets != null)
        {
            num *= sets.GetStatFactor(stat);
        }

        return num;
    }
    
    public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
    {
        var sets = ApparelSetsIfAny;
        if (UnlockedRanks.NullOrEmpty() && !HasAnyDevotion && sets is not { AnyActive: true })
        {
            base.GetStatsExplanation(stat, sb, whitespace);
            return;
        }
        var stringBuilder = new StringBuilder();
        
        foreach (var rank in UnlockedRanks)
        {
            var statOffsetFromList = rank.StatOffsetFor(stat);
            if (!Waylith.Math.Mathf.Approximately(statOffsetFromList, 0f))
            {
                stringBuilder.AppendLine(whitespace + "    " + rank.LabelCap + ": " + stat.Worker.ValueToString(statOffsetFromList, finalized: false, ToStringNumberSense.Offset));
            }
            var statFactorFromList = rank.StatFactorFor(stat);
            if (!Waylith.Math.Mathf.Approximately(statFactorFromList, 1f))
            {
                stringBuilder.AppendLine(whitespace + "    " + rank.LabelCap + ": " + stat.Worker.ValueToString(statFactorFromList, finalized: false, ToStringNumberSense.Factor));
            }
        }
        
        if (stringBuilder.Length != 0)
        {
            sb.AppendLine(whitespace + "BEWH.Framework.StatReport.Rank".Translate() + ":");
            sb.Append(stringBuilder);
        }

        devotion?.AppendStatsExplanation(stat, sb, whitespace);

        sets?.AppendStatsExplanation(stat, sb, whitespace);
    }

}