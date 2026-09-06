using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Core40k;

/// <summary>
/// Forces the hit part of incoming damage on pawns when the DamageDef (or the weapon) carries a
/// DefModExtension_TargetBodyParts. Setting DamageInfo.HitPart before the worker runs makes every
/// DamageWorker_AddInjury subclass skip its own ChooseHitPart, so this works with any damage type.
/// If none of the configured parts are present on the pawn the hit part is left unset and the worker
/// picks a part as normal.
/// </summary>
[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public class TargetBodyPartsPatch
{
    private static readonly List<BodyPartRecord> Candidates = new();

    public static void Prefix(Thing __instance, ref DamageInfo dinfo)
    {
        if (__instance is not Pawn pawn || dinfo.HitPart != null)
        {
            return;
        }

        var targeting = dinfo.Def?.GetModExtension<DefModExtension_TargetBodyParts>()
                        ?? dinfo.Weapon?.GetModExtension<DefModExtension_TargetBodyParts>();
        if (targeting == null || !Rand.Chance(targeting.chance))
        {
            return;
        }

        var hediffSet = pawn.health?.hediffSet;
        if (hediffSet == null)
        {
            return;
        }

        Candidates.Clear();
        foreach (var part in hediffSet.GetNotMissingParts())
        {
            if (targeting.Matches(part))
            {
                Candidates.Add(part);
            }
        }

        if (Candidates.Count == 0)
        {
            return;
        }

        var hitPart = targeting.weightByCoverage
            ? Candidates.RandomElementByWeight(p => p.coverage)
            : Candidates.RandomElement();

        dinfo.SetHitPart(hitPart);
        Candidates.Clear();
    }
}