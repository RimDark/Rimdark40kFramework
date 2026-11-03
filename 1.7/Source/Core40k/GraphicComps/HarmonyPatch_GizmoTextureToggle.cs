using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(Thing), "GetGizmos")]
public class GizmoTextureTogglePatch
{
    private static Game cachedGameForCoreUtils;
    private static GameComponent_CoreUtils coreUtils;

    private static GameComponent_CoreUtils CoreUtils
    {
        get
        {
            if (coreUtils != null && cachedGameForCoreUtils == Current.Game)
            {
                return coreUtils;
            }

            cachedGameForCoreUtils = Current.Game;
            coreUtils = cachedGameForCoreUtils?.GetComponent<GameComponent_CoreUtils>();
            return coreUtils;
        }
    }
    
    //Runs for every selected thing every frame, so anything without a toggle flag is handed the
    //original enumerable back untouched instead of being wrapped in an iterator.
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Thing __instance)
    {
        var defMod = __instance?.def?.GetModExtension<DefModExtension_TextureFlags>();
        if (defMod == null || defMod.GizmoFlags.Count == 0)
        {
            return __result;
        }

        return WithToggles(__result, __instance, defMod.GizmoFlags);
    }

    private static IEnumerable<Gizmo> WithToggles(IEnumerable<Gizmo> original, Thing __instance, List<TextureFlag> toggleFlags)
    {
        foreach (var gizmo in original)
        {
            yield return gizmo;
        }
        
        var wearer = __instance.ParentHolder is not Pawn_ApparelTracker pawn_ApparelTracker ? null : pawn_ApparelTracker.pawn;
        var holder = __instance.ParentHolder is not Pawn_EquipmentTracker pawn_EquipmentTracker ? null : pawn_EquipmentTracker.pawn;

        var pawn = wearer ?? holder;

        var coreUtils = CoreUtils;
        if (pawn == null || coreUtils == null)
        {
            yield break;
        }

        //Only toggles that are on are stored; a missing pair reads as off.
        var pair = (pawn, __instance);

        foreach (var textureFlag in toggleFlags)
        {
            var gizmoOn = coreUtils.cachedGizmoToggles.TryGetValue(pair, out var on) && on;

            var toggleCommand = new Command_Toggle
            {
                defaultLabel = gizmoOn ? textureFlag.gizmoOnText : textureFlag.gizmoOffText,
                icon = Core40kUtils.FlippedIconTex,
                isActive = () => coreUtils.cachedGizmoToggles.TryGetValue(pair, out var active) && active,
                toggleAction = delegate
                {
                    var current = coreUtils.cachedGizmoToggles.TryGetValue(pair, out var active) && active;
                    if (current)
                    {
                        coreUtils.cachedGizmoToggles.Remove(pair);
                    }
                    else
                    {
                        coreUtils.cachedGizmoToggles[pair] = true;
                    }
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                },
            };

            yield return toggleCommand;
        }
    }
}
