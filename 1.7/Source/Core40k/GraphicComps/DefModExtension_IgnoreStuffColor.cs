using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

/// <summary>
/// Things made from stuff normally take the stuff's colour. Defs with this extension are drawn untinted
/// with every material, including ones added by other mods.
/// </summary>
public class DefModExtension_IgnoreStuffColor : DefModExtension
{
}

//Fills colorPerStuff once at startup, so drawing never has to check the extension.
[StaticConstructorOnStartup]
public static class IgnoreStuffColorUtility
{
    private static readonly AccessTools.FieldRef<ColorForStuff, ThingDef> StuffRef = AccessTools.FieldRefAccess<ColorForStuff, ThingDef>("stuff");
    private static readonly AccessTools.FieldRef<ColorForStuff, Color> ColorRef = AccessTools.FieldRefAccess<ColorForStuff, Color>("color");

    static IgnoreStuffColorUtility()
    {
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            Apply(def);
        }
        foreach (var def in DefDatabase<TerrainDef>.AllDefsListForReading)
        {
            Apply(def);
        }
    }

    private static void Apply(BuildableDef def)
    {
        if (!def.MadeFromStuff || !def.HasModExtension<DefModExtension_IgnoreStuffColor>())
        {
            return;
        }

        def.colorPerStuff ??= new List<ColorForStuff>();

        //Colours a def sets for a specific stuff itself stay as they are.
        var covered = new HashSet<ThingDef>();
        foreach (var entry in def.colorPerStuff)
        {
            covered.Add(entry.Stuff);
        }

        foreach (var stuff in GenStuff.AllowedStuffsFor(def))
        {
            if (!covered.Add(stuff))
            {
                continue;
            }

            var entry = new ColorForStuff();
            StuffRef(entry) = stuff;
            ColorRef(entry) = Color.white;
            def.colorPerStuff.Add(entry);
        }

        //ResolveIcon already took the default stuff's colour before this ran.
        if (def is ThingDef { category: not ThingCategory.Pawn } thingDef)
        {
            var defaultStuff = GenStuff.DefaultStuffFor(thingDef);
            if (defaultStuff != null && (thingDef.colorGenerator == null || !defaultStuff.stuffProps.allowColorGenerators))
            {
                thingDef.uiIconColor = thingDef.GetColorForStuff(defaultStuff);
            }
        }
    }
}
