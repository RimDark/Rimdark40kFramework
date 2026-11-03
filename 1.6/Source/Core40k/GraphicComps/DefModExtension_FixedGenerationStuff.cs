using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Pawns generated wearing apparel or carrying a weapon with this extension always get it in one stuff, instead of a random one.
/// Players can still craft it from any stuff it allows.
/// </summary>
public class DefModExtension_FixedGenerationStuff : DefModExtension
{
    //Null uses the def's defaultStuff.
    public ThingDef stuff;
}

[HarmonyPatch(typeof(PawnApparelGenerator), nameof(PawnApparelGenerator.Reset))]
public static class HarmonyPatch_FixedGenerationStuff
{
    private static readonly AccessTools.FieldRef<List<ThingStuffPair>> AllApparelPairs = AccessTools.StaticFieldRefAccess<List<ThingStuffPair>>(AccessTools.Field(typeof(PawnApparelGenerator), "allApparelPairs"));

    //Every way pawn generation picks apparel goes through allApparelPairs, so leaving one pair per def is enough.
    public static void Postfix()
    {
        KeepFixedStuffOnly(AllApparelPairs());
    }

    public static void KeepFixedStuffOnly(List<ThingStuffPair> pairs)
    {
        if (pairs == null)
        {
            return;
        }

        var fixedStuff = new Dictionary<ThingDef, ThingDef>();
        var checkedDefs = new HashSet<ThingDef>();
        foreach (var pair in pairs)
        {
            if (pair.stuff == null || !checkedDefs.Add(pair.thing))
            {
                continue;
            }
            var defMod = pair.thing.GetModExtension<DefModExtension_FixedGenerationStuff>();
            if (defMod == null)
            {
                continue;
            }
            var stuff = defMod.stuff ?? GenStuff.DefaultStuffFor(pair.thing);
            if (!pairs.Any(p => p.thing == pair.thing && p.stuff == stuff))
            {
                Log.Error($"[Core40k] DefModExtension_FixedGenerationStuff on {pair.thing.defName}: {stuff?.defName ?? "null"} is not a stuff it can be made from. Leaving its stuff random.");
                continue;
            }
            fixedStuff[pair.thing] = stuff;
        }

        if (fixedStuff.Count == 0)
        {
            return;
        }

        pairs.RemoveAll(p => p.stuff != null && fixedStuff.TryGetValue(p.thing, out var stuff) && p.stuff != stuff);

        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.stuff == null || !fixedStuff.ContainsKey(pair.thing))
            {
                continue;
            }
            //A def's random-stuff pairs added up to its generateCommonality; the one pair left keeps all of it.
            var commonality = pair.stuff.stuffProps.commonality;
            pairs[i] = new ThingStuffPair(pair.thing, pair.stuff, commonality > 0f ? 1f / commonality : 1f);
        }
    }
}

//Pawn weapons are picked from PawnWeaponGenerator's own pair list.
[HarmonyPatch(typeof(PawnWeaponGenerator), nameof(PawnWeaponGenerator.Reset))]
public static class HarmonyPatch_FixedGenerationStuffWeapons
{
    public static void Postfix()
    {
        HarmonyPatch_FixedGenerationStuff.KeepFixedStuffOnly(PawnWeaponGenerator.AllWeaponPairs);
    }
}
