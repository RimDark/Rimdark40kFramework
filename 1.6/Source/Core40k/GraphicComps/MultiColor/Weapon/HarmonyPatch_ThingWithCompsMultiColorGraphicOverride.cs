using HarmonyLib;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(Thing), "Graphic", MethodType.Getter)]
[HarmonyPriority(Priority.Normal)]
public class ThingWithCompsMultiColorGraphicOverride
{
    public static void Postfix(ref Graphic __result, Thing __instance)
    {
        if (__instance is not ThingWithComps weapon)
        {
            return;
        }

        var def = weapon.def;
        //Pawns (CompMultiColorPawn) are painted through their body render node, not Thing.Graphic.
        var mayHaveMultiColor = def.race == null && Core40kUtils.DefHasComp<CompMultiColor>(def);
        var mayHaveAlternateTexture = Core40kUtils.DefHasComp<CompAlternateTexture>(def);
        if (!mayHaveMultiColor && !mayHaveAlternateTexture)
        {
            return;
        }
        
        var multiColor = mayHaveMultiColor ? weapon.GetComp<CompMultiColor>() : null;
        var alternateTexture = mayHaveAlternateTexture ? weapon.GetComp<CompAlternateTexture>() : null;
        if (multiColor == null && alternateTexture == null)
        {
            return;
        }

        if ((multiColor != null && multiColor.RecacheSingleGraphics) || (alternateTexture != null && alternateTexture.RecacheSingleGraphics))
        {
            multiColor?.SetSingleGraphic();
            alternateTexture?.SetSingleGraphic();
        }

        if (multiColor != null)
        {
            __result = multiColor.GetSingleGraphic();
        }
        else if (alternateTexture != null)
        {
            __result = alternateTexture.GetSingleGraphic();
        }
    }
}