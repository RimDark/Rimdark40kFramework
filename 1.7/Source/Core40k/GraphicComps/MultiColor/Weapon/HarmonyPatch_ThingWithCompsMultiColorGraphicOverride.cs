using System;
using HarmonyLib;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(Thing), "Graphic", MethodType.Getter)]
[HarmonyPriority(Priority.Normal)]
public class ThingWithCompsMultiColorGraphicOverride
{
    //Thing.Graphic is read constantly for every item, so whether a def can carry either comp is kept in an
    //array indexed by def.index: one read for the common case instead of two dictionary lookups.
    private const byte Known = 1;
    private const byte HasMultiColor = 2;
    private const byte HasAlternateTexture = 4;

    private static byte[] defFlags = [];

    private static byte FlagsFor(ThingDef def)
    {
        var index = def.index;
        var flags = defFlags;
        if (index < flags.Length && flags[index] != 0)
        {
            return flags[index];
        }

        var value = Known;
        //Pawns (CompMultiColorPawn) are painted through their body render node, not Thing.Graphic.
        if (def.race == null && Core40kUtils.DefHasComp<CompMultiColor>(def))
        {
            value |= HasMultiColor;
        }
        if (Core40kUtils.DefHasComp<CompAlternateTexture>(def))
        {
            value |= HasAlternateTexture;
        }

        //Defs outside the database keep the unset index; don't size the array for those.
        if (index == ushort.MaxValue)
        {
            return value;
        }

        //Runs on the parallel render threads too. Losing a race only means a def is worked out again.
        if (index >= flags.Length)
        {
            var grown = new byte[Math.Max(index + 1, DefDatabase<ThingDef>.DefCount)];
            Array.Copy(flags, grown, flags.Length);
            grown[index] = value;
            defFlags = grown;
        }
        else
        {
            flags[index] = value;
        }

        return value;
    }

    public static void Postfix(ref Graphic __result, Thing __instance)
    {
        if (__instance is not ThingWithComps weapon || weapon.def == null)
        {
            return;
        }

        var flags = FlagsFor(weapon.def);
        var mayHaveMultiColor = (flags & HasMultiColor) != 0;
        var mayHaveAlternateTexture = (flags & HasAlternateTexture) != 0;
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