using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(PawnRenderTree), "AdjustParms")]
public static class AlternateFormRenderSkipFlagsPatch
{
    private static readonly ConcurrentDictionary<ThingDef, bool> hasAlternateTextureCache = new();

    private static bool HasAlternateTexture(ThingDef def)
    {
        return hasAlternateTextureCache.GetOrAdd(def, static thingDef => thingDef.HasComp<CompAlternateTexture>());
    }

    private static List<RenderSkipFlagDef> FormSkipFlags(Apparel apparel)
    {
        if (!HasAlternateTexture(apparel.def))
        {
            return null;
        }
        return apparel.GetComp<CompAlternateTexture>()?.CurrentAlternateBaseForm?.renderSkipFlags;
    }

    public static void Prefix(ref PawnDrawParms parms, out ulong __state)
    {
        __state = parms.skipFlags;
    }

    public static void Postfix(PawnRenderTree __instance, ref PawnDrawParms parms, ulong __state)
    {
        var pawn = __instance.pawn;
        if (pawn?.apparel == null || !pawn.RaceProps.Humanlike || !PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms))
        {
            return;
        }

        var wornApparel = pawn.apparel.WornApparel;
        var anyOverride = false;
        for (var i = 0; i < wornApparel.Count; i++)
        {
            if (FormSkipFlags(wornApparel[i]) != null)
            {
                anyOverride = true;
                break;
            }
        }
        if (!anyOverride)
        {
            return;
        }

        var vanillaFlags = ApparelSkipFlags(wornApparel, parms.facing, __state, false);
        var formFlags = ApparelSkipFlags(wornApparel, parms.facing, __state, true);
        parms.skipFlags = (parms.skipFlags & ~vanillaFlags) | formFlags;
    }

    /// <summary>
    /// Mirrors the apparel part of PawnRenderTree.AdjustParms, optionally using the skip flags of each item's current alternate form.
    /// </summary>
    private static ulong ApparelSkipFlags(List<Apparel> wornApparel, Rot4 facing, ulong flags, bool useForms)
    {
        for (var i = 0; i < wornApparel.Count; i++)
        {
            var apparel = wornApparel[i];
            var props = apparel.def.apparel;
            var skipFlags = (useForms ? FormSkipFlags(apparel) : null) ?? props.renderSkipFlags;
            if (skipFlags != null)
            {
                for (var j = 0; j < skipFlags.Count; j++)
                {
                    if (skipFlags[j] != null && skipFlags[j] != RenderSkipFlagDefOf.None)
                    {
                        flags |= skipFlags[j];
                    }
                }
            }
            else
            {
                if (props.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead))
                {
                    flags |= RenderSkipFlagDefOf.Hair;
                }
                if (props.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead))
                {
                    flags |= RenderSkipFlagDefOf.Hair;
                    flags |= RenderSkipFlagDefOf.Beard;
                    flags |= RenderSkipFlagDefOf.Eyes;
                }
            }
            if (props.forceEyesVisibleForRotations.Contains(facing.AsInt))
            {
                flags &= ~(ulong)RenderSkipFlagDefOf.Eyes;
            }
        }
        return flags;
    }
}
