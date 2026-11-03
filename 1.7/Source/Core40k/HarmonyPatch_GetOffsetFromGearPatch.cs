using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

//Only artifact wielder offsets reach the wearer here. Decoration and alternate form statOffsets stay on the item
//(vanilla sums a thing's comps into its own stats); wearer bonuses come from pawnStatOffsets instead.
[HarmonyPatch(typeof(StatWorker), "StatOffsetFromGear")]
public static class GetOffsetFromGearPatch
{
    public static void Postfix(ref float __result, Thing gear, StatDef stat)
    {
        //Runs per uncached pawn stat for every piece of gear, so stats no artifact trait offsets stop here.
        if (!DecorationIndex.AffectsWielderStatOffset(stat) || !Core40kUtils.DefHasComp<CompArtifactWeapon>(gear?.def))
        {
            return;
        }

        if (gear is ThingWithComps thingWithComps && thingWithComps.GetComp<CompArtifactWeapon>() is { } artifact)
        {
            __result += artifact.GetWielderStatOffset(stat);
        }
    }
}
