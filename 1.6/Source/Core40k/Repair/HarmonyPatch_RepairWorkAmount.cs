using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(Bill), nameof(Bill.GetWorkAmount))]
public static class HarmonyPatch_RepairWorkAmount
{
    public static void Postfix(Bill __instance, Thing thing, ref float __result)
    {
        if (thing == null)
        {
            return;
        }
        var extension = __instance.recipe?.GetModExtension<DefModExtension_RepairRecipe>();
        if (extension == null)
        {
            return;
        }
        var target = EquipmentRepairUtility.FindRepairTarget(__instance, thing);
        if (target != null)
        {
            __result = EquipmentRepairUtility.GetRepairWork(target, extension);
        }
    }
}
