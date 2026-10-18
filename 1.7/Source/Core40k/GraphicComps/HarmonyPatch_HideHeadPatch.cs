using System.Collections.Concurrent;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(PawnRenderNodeWorker), "GetGraphic")]
[HarmonyPriority(Priority.LowerThanNormal)]
public static class HideHeadPatch
{
    private static readonly ConcurrentDictionary<ThingDef, DefModExtension_HidesHead> hidesHeadCache = new();

    private static DefModExtension_HidesHead HidesHead(ThingDef def)
    {
        return hidesHeadCache.GetOrAdd(def, static thingDef => thingDef.GetModExtension<DefModExtension_HidesHead>());
    }

    /// <summary>
    /// Mirrors how vanilla decides whether apparel is drawn as headgear.
    /// </summary>
    private static bool IsHeadgear(ThingDef def)
    {
        var props = def.apparel;
        if (props == null)
        {
            return false;
        }
        var lastLayer = props.LastLayer;
        return props.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead || lastLayer == ApparelLayerDefOf.Overhead || lastLayer == ApparelLayerDefOf.EyeCover;
    }

    public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Graphic __result, PawnRenderNodeWorker __instance)
    {
        if (__result == null || (parms.flags & PawnRenderFlags.Clothes) != PawnRenderFlags.Clothes)
        {
            return;
        }

        if (__instance is not (PawnRenderNodeWorker_Head or PawnRenderNodeWorker_Tattoo_Head) || __instance is PawnRenderNodeWorker_Stump)
        {
            return;
        }

        if (!(parms.pawn.RaceProps?.Humanlike).GetValueOrDefault() || parms.pawn.apparel == null || !parms.pawn.apparel.AnyApparel)
        {
            return;
        }

        var wornApparel = parms.pawn.apparel.WornApparel;
        for (var i = 0; i < wornApparel.Count; i++)
        {
            var def = wornApparel[i].def;
            var extension = HidesHead(def);
            if (extension == null || !extension.HidesFor(parms.facing))
            {
                continue;
            }
            if (IsHeadgear(def) && !PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms))
            {
                continue;
            }
            __result = Core40kUtils.EmptyMultiGraphic;
            return;
        }
    }
}
