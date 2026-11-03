using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Something a rank does while held. Listed in RankDef.effects as li Class="..."; the givesX fields are turned into these at load.
/// Remove must be safe on a pawn that has lost what Apply gave (surgery, another mod).
/// </summary>
public abstract class RankEffect
{
    /// <summary>
    /// Runs when the pawn gains the rank.
    /// </summary>
    public virtual void Apply(CompRankInfo rankComp, RankDef rank)
    {
    }

    /// <summary>
    /// Runs when the pawn loses the rank. The rank is already out of rankComp.UnlockedRanks.
    /// </summary>
    public virtual void Remove(CompRankInfo rankComp, RankDef rank)
    {
    }

    /// <summary>
    /// Writes what the effect does into the rank tab's bonus text.
    /// </summary>
    public virtual void Describe(EffectReport report)
    {
    }

    public virtual void Notify_Killed(CompRankInfo rankComp, RankDef rank, Map prevMap, DamageInfo? dinfo)
    {
    }

    public virtual IEnumerable<string> ConfigErrors(Def parent)
    {
        yield break;
    }
}
