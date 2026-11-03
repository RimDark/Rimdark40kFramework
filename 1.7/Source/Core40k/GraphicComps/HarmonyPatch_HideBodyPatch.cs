using HarmonyLib;
using Verse;

namespace Core40k;

/// <summary>
/// Hides the body (DefModExtension_ForcesBodyType.hideBodyGraphic) and the head (DefModExtension_HidesHead)
/// under apparel that asks for it. Runs on every render node's GetGraphic, so the worker type is checked
/// first and the worn apparel comes from WornApparelRenderCache.
/// </summary>
[HarmonyPatch(typeof(PawnRenderNodeWorker), "GetGraphic")]
[HarmonyPriority(Priority.LowerThanNormal)]
public static class HideBodyAndHeadPatch
{
    public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Graphic __result, PawnRenderNodeWorker __instance)
    {
        if (__result == null || (parms.flags & PawnRenderFlags.Clothes) != PawnRenderFlags.Clothes)
        {
            return;
        }

        //Exact type: apparel workers subclass the body worker and must not be hidden.
        var isBody = __instance.GetType() == typeof(PawnRenderNodeWorker_Body);
        var isHead = !isBody
                     && __instance is PawnRenderNodeWorker_Head or PawnRenderNodeWorker_Tattoo_Head
                     && __instance is not PawnRenderNodeWorker_Stump;
        if (!isBody && !isHead)
        {
            return;
        }

        var pawn = parms.pawn;
        if (!(pawn?.RaceProps?.Humanlike).GetValueOrDefault() || pawn.apparel == null)
        {
            return;
        }

        var worn = WornApparelRenderCache.For(pawn);
        if (isBody)
        {
            if (worn.hidesBody)
            {
                __result = Core40kUtils.EmptyMultiGraphic;
            }
            return;
        }

        foreach (var hider in worn.headHiders)
        {
            if (!hider.extension.HidesFor(parms.facing))
            {
                continue;
            }
            if (hider.isHeadgear && !PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms))
            {
                continue;
            }
            __result = Core40kUtils.EmptyMultiGraphic;
            return;
        }
    }
}
