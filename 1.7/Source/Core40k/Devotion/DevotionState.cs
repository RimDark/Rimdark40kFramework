using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

public class DevotionState : IExposable
{
    private const int TicksPerDay = 60000;

    private Dictionary<DevotionCreedDef, float> standing = new Dictionary<DevotionCreedDef, float>();
    private Dictionary<DevotionCreedDef, int> appliedTier = new Dictionary<DevotionCreedDef, int>();
    private Dictionary<DevotionCreedDef, int> highestTierReached = new Dictionary<DevotionCreedDef, int>();
    private Dictionary<DevotionCreedDef, int> lastGainTick = new Dictionary<DevotionCreedDef, int>();

    private List<DevotionCreedDef> standingKeys;
    private List<float> standingValues;
    private List<DevotionCreedDef> appliedTierKeys;
    private List<int> appliedTierValues;
    private List<DevotionCreedDef> highestTierReachedKeys;
    private List<int> highestTierReachedValues;
    private List<DevotionCreedDef> lastGainTickKeys;
    private List<int> lastGainTickValues;

    private CompRankInfo parent;

    private List<DevotionCreedDef> cachedActiveCreeds;
    private DevotionCreedDef cachedDominant;
    private bool dominantDirty = true;

    public DevotionState()
    {
    }

    /// <summary>
    /// Assigned by the owning comp on construction and again after load, since the scribe
    /// instantiates this through the parameterless constructor.
    /// </summary>
    public void SetParent(CompRankInfo comp)
    {
        parent = comp;
    }

    public bool HasAnyDevotion => standing.Count > 0;

    public IReadOnlyList<DevotionCreedDef> ActiveCreeds => cachedActiveCreeds ??= standing.Keys.ToList();

    public DevotionCreedDef DominantCreed
    {
        get
        {
            if (!dominantDirty)
            {
                return cachedDominant;
            }

            dominantDirty = false;
            cachedDominant = null;

            var best = 0f;
            foreach (var pair in standing)
            {
                if (pair.Key == null || pair.Value <= best)
                {
                    continue;
                }

                best = pair.Value;
                cachedDominant = pair.Key;
            }

            return cachedDominant;
        }
    }

    public DevotionFaithDef DominantFaith => DominantCreed?.faith;

    public float GetStanding(DevotionCreedDef creed)
    {
        if (creed == null)
        {
            return 0f;
        }

        return standing.TryGetValue(creed, out var value) ? value : 0f;
    }

    public float GetStandingPct(DevotionCreedDef creed)
    {
        if (creed == null || creed.maxStanding <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(GetStanding(creed) / creed.maxStanding, 0f, 1f);
    }

    public int TierIndexOf(DevotionCreedDef creed)
    {
        return TierIndexFromStanding(creed, GetStanding(creed));
    }

    public DevotionTierDef TierOf(DevotionCreedDef creed)
    {
        var index = TierIndexOf(creed);
        return index < 0 ? null : creed.tiers[index];
    }

    public int HighestTierReached(DevotionCreedDef creed)
    {
        if (creed == null)
        {
            return -1;
        }

        return highestTierReached.TryGetValue(creed, out var value) ? value : -1;
    }

    private static int TierIndexFromStanding(DevotionCreedDef creed, float value)
    {
        if (creed == null || creed.tiers.NullOrEmpty())
        {
            return -1;
        }

        var index = -1;
        for (var i = 0; i < creed.tiers.Count; i++)
        {
            if (value + 0.0001f < creed.tiers[i].threshold)
            {
                break;
            }

            index = i;
        }

        return index;
    }

    /// <summary>
    /// Lowest standing this pawn can fall to: the threshold of the highest non-reversible tier
    /// they have reached. Irreversibility is enforced as a floor rather than by blocking decay.
    /// </summary>
    public float FloorFor(DevotionCreedDef creed)
    {
        var highest = HighestTierReached(creed);
        if (highest < 0 || creed.tiers.NullOrEmpty())
        {
            return 0f;
        }

        var floor = 0f;
        for (var i = 0; i <= highest && i < creed.tiers.Count; i++)
        {
            if (!creed.tiers[i].reversible)
            {
                floor = creed.tiers[i].threshold;
            }
        }

        return floor;
    }

    public float AddStanding(DevotionCreedDef creed, float amount)
    {
        if (creed == null)
        {
            return 0f;
        }

        var current = GetStanding(creed);
        var result = SetStandingInternal(creed, current + amount, false);

        if (amount > 0f)
        {
            lastGainTick[creed] = Find.TickManager?.TicksGame ?? 0;
            ApplyBleed(creed, amount);
        }

        return result;
    }

    public void SetStanding(DevotionCreedDef creed, float value, bool silent = false)
    {
        if (creed == null)
        {
            return;
        }

        var gained = value > GetStanding(creed);
        SetStandingInternal(creed, value, silent);

        if (gained)
        {
            lastGainTick[creed] = Find.TickManager?.TicksGame ?? 0;
        }
    }

    private float SetStandingInternal(DevotionCreedDef creed, float value, bool silent)
    {
        var floor = FloorFor(creed);
        var clamped = Math.Clamp(value, floor, creed.maxStanding);
        var had = standing.ContainsKey(creed);
        var oldTier = appliedTier.TryGetValue(creed, out var applied) ? applied : -1;

        if (clamped <= 0f && floor <= 0f && oldTier < 0)
        {
            if (had)
            {
                standing.Remove(creed);
                InvalidateCreedCaches();
                dominantDirty = true;
                if (!silent)
                {
                    DevotionUtils.Notify_DevotionChanged(parent?.ParentPawn);
                }
            }

            return 0f;
        }

        standing[creed] = clamped;

        if (!had)
        {
            InvalidateCreedCaches();
            if (!silent)
            {
                DevotionUtils.Notify_DevotionChanged(parent?.ParentPawn);
            }
        }

        dominantDirty = true;

        var newTier = TierIndexFromStanding(creed, clamped);
        if (newTier != oldTier)
        {
            ApplyTierTransition(creed, oldTier, newTier, silent);
        }

        return clamped;
    }

    private void ApplyBleed(DevotionCreedDef creed, float amount)
    {
        if (creed.faith == null || creed.faith.budget != DevotionBudget.Shared || creed.bleed.NullOrEmpty())
        {
            return;
        }

        foreach (var entry in creed.bleed)
        {
            var other = entry?.creed;
            if (other == null || other == creed || other.faith != creed.faith || entry.factor <= 0f)
            {
                continue;
            }

            if (!standing.ContainsKey(other))
            {
                continue;
            }

            SetStandingInternal(other, GetStanding(other) - (amount * entry.factor), false);
        }
    }

    private void ApplyTierTransition(DevotionCreedDef creed, int from, int to, bool silent)
    {
        appliedTier[creed] = to;

        if (to > HighestTierReached(creed))
        {
            highestTierReached[creed] = to;
        }

        if (to > from)
        {
            for (var i = from + 1; i <= to && i < creed.tiers.Count; i++)
            {
                creed.tiers[i].Apply(parent, creed);
                if (!silent)
                {
                    SendTierLetter(creed, creed.tiers[i]);
                }
            }
        }
        else
        {
            for (var i = from; i > to; i--)
            {
                if (i < 0 || i >= creed.tiers.Count)
                {
                    continue;
                }

                creed.tiers[i].Remove(parent, creed);
            }
        }

        parent?.InvalidateCaches();
        dominantDirty = true;

        if (!silent)
        {
            DevotionUtils.Notify_TierChanged(parent?.ParentPawn, creed, from, to);
        }
    }

    private void SendTierLetter(DevotionCreedDef creed, DevotionTierDef tier)
    {
        if (tier.letterOnReach == null || parent?.ParentPawn == null)
        {
            return;
        }

        var pawn = parent.ParentPawn;
        if (pawn.Faction is not { IsPlayer: true })
        {
            return;
        }

        var label = tier.letterLabel.NullOrEmpty()
            ? "BEWH.Framework.Devotion.TierLetterLabel".Translate(pawn.LabelShortCap, tier.LabelCap)
            : tier.letterLabel.Formatted(pawn.LabelShortCap, tier.LabelCap, creed.LabelCap);
        var text = tier.letterText.NullOrEmpty()
            ? "BEWH.Framework.Devotion.TierLetterText".Translate(pawn.LabelShortCap, tier.LabelCap, creed.LabelCap)
            : tier.letterText.Formatted(pawn.LabelShortCap, tier.LabelCap, creed.LabelCap);

        //Extra effects describe themselves below the letter text.
        foreach (var effect in tier.extraEffects)
        {
            var explanation = effect?.ExplanationFor(creed, tier);
            if (!explanation.NullOrEmpty())
            {
                text += "\n\n" + explanation;
            }
        }

        Find.LetterStack.ReceiveLetter(label, text, tier.letterOnReach, pawn);
    }

    public bool TryPurge(DevotionCreedDef creed, out string reason)
    {
        reason = null;
        if (creed == null || !standing.ContainsKey(creed))
        {
            reason = "BEWH.Framework.Devotion.NoStanding".Translate();
            return false;
        }

        if (FloorFor(creed) > 0f)
        {
            reason = "BEWH.Framework.Devotion.PurgeTooDeep".Translate(creed.LabelCap);
            return false;
        }

        var oldTier = appliedTier.TryGetValue(creed, out var applied) ? applied : -1;
        if (oldTier >= 0)
        {
            ApplyTierTransition(creed, oldTier, -1, false);
        }

        standing.Remove(creed);
        appliedTier.Remove(creed);
        highestTierReached.Remove(creed);
        lastGainTick.Remove(creed);
        InvalidateCreedCaches();
        dominantDirty = true;
        parent?.InvalidateCaches();
        DevotionUtils.Notify_DevotionChanged(parent?.ParentPawn);
        return true;
    }

    public void ClearAllDevotion()
    {
        foreach (var creed in ActiveCreeds.ToList())
        {
            if (creed == null)
            {
                continue;
            }

            var oldTier = appliedTier.TryGetValue(creed, out var applied) ? applied : -1;
            if (oldTier >= 0)
            {
                ApplyTierTransition(creed, oldTier, -1, false);
            }
        }

        standing.Clear();
        appliedTier.Clear();
        highestTierReached.Clear();
        lastGainTick.Clear();
        InvalidateCreedCaches();
        dominantDirty = true;
        parent?.InvalidateCaches();
        DevotionUtils.Notify_DevotionChanged(parent?.ParentPawn);
    }

    /// <summary>
    /// Called on a slow cadence from GameComponent_Devotion, not from a comp ticker. The current tier's
    /// decayPerDayOverride wins over the creed's decayPerDay when set.
    /// </summary>
    public void TickDecay(int interval)
    {
        if (standing.Count == 0)
        {
            return;
        }

        var now = Find.TickManager?.TicksGame ?? 0;

        foreach (var creed in ActiveCreeds.ToList())
        {
            if (creed == null)
            {
                continue;
            }

            var tier = TierOf(creed);
            var rate = tier != null && tier.decayPerDayOverride >= 0f ? tier.decayPerDayOverride : creed.decayPerDay;
            if (rate <= 0f)
            {
                continue;
            }

            if (lastGainTick.TryGetValue(creed, out var last) && now - last < creed.decayDelayDays * TicksPerDay)
            {
                continue;
            }

            var amount = rate * (interval / (float)TicksPerDay);
            if (amount <= 0f)
            {
                continue;
            }

            SetStandingInternal(creed, GetStanding(creed) - amount, false);
        }
    }

    public float GetStatOffset(StatDef stat)
    {
        var num = 0f;
        foreach (var pair in appliedTier)
        {
            if (pair.Key == null || pair.Value < 0 || pair.Value >= pair.Key.tiers.Count)
            {
                continue;
            }

            for (var i = 0; i <= pair.Value; i++)
            {
                var offsets = pair.Key.tiers[i].statOffsets;
                if (!offsets.NullOrEmpty())
                {
                    num += offsets.GetStatOffsetFromList(stat);
                }
            }
        }

        return num;
    }

    public float GetStatFactor(StatDef stat)
    {
        var num = 1f;
        foreach (var pair in appliedTier)
        {
            if (pair.Key == null || pair.Value < 0 || pair.Value >= pair.Key.tiers.Count)
            {
                continue;
            }

            for (var i = 0; i <= pair.Value; i++)
            {
                var factors = pair.Key.tiers[i].statFactors;
                if (!factors.NullOrEmpty())
                {
                    num *= factors.GetStatFactorFromList(stat);
                }
            }
        }

        return num;
    }

    public void AppendStatsExplanation(StatDef stat, StringBuilder sb, string whitespace)
    {
        if (appliedTier.Count == 0)
        {
            return;
        }

        var stringBuilder = new StringBuilder();

        foreach (var pair in appliedTier)
        {
            if (pair.Key == null || pair.Value < 0)
            {
                continue;
            }

            for (var i = 0; i <= pair.Value && i < pair.Key.tiers.Count; i++)
            {
                var tier = pair.Key.tiers[i];
                var offset = tier.statOffsets.NullOrEmpty() ? 0f : tier.statOffsets.GetStatOffsetFromList(stat);
                if (!Waylith.Math.Mathf.Approximately(offset, 0f))
                {
                    stringBuilder.AppendLine(whitespace + "    " + tier.LabelCap + ": " + Core40kUtils.ValueToString(stat, offset, false, ToStringNumberSense.Offset));
                }

                var factor = tier.statFactors.NullOrEmpty() ? 1f : tier.statFactors.GetStatFactorFromList(stat);
                if (!Waylith.Math.Mathf.Approximately(factor, 1f))
                {
                    stringBuilder.AppendLine(whitespace + "    " + tier.LabelCap + ": " + Core40kUtils.ValueToString(stat, factor, false, ToStringNumberSense.Factor));
                }
            }
        }

        if (stringBuilder.Length == 0)
        {
            return;
        }

        sb.AppendLine(whitespace + "BEWH.Framework.StatReport.Devotion".Translate() + ":");
        sb.Append(stringBuilder);
    }

    private void InvalidateCreedCaches()
    {
        cachedActiveCreeds = null;
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref standing, "standing", LookMode.Def, LookMode.Value, ref standingKeys, ref standingValues, false);
        Scribe_Collections.Look(ref appliedTier, "appliedTier", LookMode.Def, LookMode.Value, ref appliedTierKeys, ref appliedTierValues, false);
        Scribe_Collections.Look(ref highestTierReached, "highestTierReached", LookMode.Def, LookMode.Value, ref highestTierReachedKeys, ref highestTierReachedValues, false);
        Scribe_Collections.Look(ref lastGainTick, "lastGainTick", LookMode.Def, LookMode.Value, ref lastGainTickKeys, ref lastGainTickValues, false);
    }

    /// <summary>
    /// Reconciles the applied tier against the saved standing so threshold edits between versions apply or clean
    /// up their payloads. Runs silently: no letters, no tier-change event. Missing defs were already dropped by the
    /// dictionary loader.
    /// </summary>
    public void PostLoadInit()
    {
        standing ??= new Dictionary<DevotionCreedDef, float>();
        appliedTier ??= new Dictionary<DevotionCreedDef, int>();
        highestTierReached ??= new Dictionary<DevotionCreedDef, int>();
        lastGainTick ??= new Dictionary<DevotionCreedDef, int>();

        InvalidateCreedCaches();
        dominantDirty = true;

        foreach (var creed in ActiveCreeds.ToList())
        {
            if (creed == null)
            {
                continue;
            }

            var current = GetStanding(creed);
            var floor = FloorFor(creed);
            if (current < floor)
            {
                current = floor;
                standing[creed] = current;
            }

            var saved = appliedTier.TryGetValue(creed, out var applied) ? applied : -1;
            var actual = TierIndexFromStanding(creed, current);
            if (saved != actual)
            {
                ApplyTierTransition(creed, saved, actual, true);
            }
        }
    }
}
