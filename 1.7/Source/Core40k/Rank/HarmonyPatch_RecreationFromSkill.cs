using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(Pawn_SkillTracker), "Learn")]
public class RecreationFromSkill
{
    private sealed class CompHolder
    {
        public CompRankInfo comp;
    }

    private static bool? anyRankGivesRecreation;

    //Learn runs every tick for every working pawn, so the comp scan is done once per pawn and
    //skipped entirely when no loaded rank grants recreation from a skill.
    private static readonly ConditionalWeakTable<Pawn, CompHolder> rankComps = new();

    private static bool AnyRankGivesRecreation
    {
        get
        {
            if (anyRankGivesRecreation.HasValue)
            {
                return anyRankGivesRecreation.Value;
            }

            var result = false;
            foreach (var rank in DefDatabase<RankDef>.AllDefsListForReading)
            {
                if (rank.AllRecreationSkills.Count > 0)
                {
                    result = true;
                    break;
                }
            }

            anyRankGivesRecreation = result;
            return result;
        }
    }

    public static void Postfix(SkillDef sDef, float xp, Pawn ___pawn)
    {
        if (xp <= 0f || ___pawn == null || !AnyRankGivesRecreation)
        {
            return;
        }

        var rankComp = rankComps.GetValue(___pawn, static pawn => new CompHolder { comp = pawn.GetComp<CompRankInfo>() }).comp;
        if (rankComp == null)
        {
            return;
        }
        if (rankComp.RecreationSkillsFromRanks.Contains(sDef))
        {
            ___pawn.needs?.joy?.GainJoy(xp * 0.001f, Core40kDefOf.BEWH_RecreationFromSkill);
        }
    }
}