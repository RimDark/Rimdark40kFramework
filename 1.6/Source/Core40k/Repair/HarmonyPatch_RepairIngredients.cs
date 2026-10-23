using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
public static class HarmonyPatch_RepairIngredients
{
    public static bool Prefix(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients, ref bool __result)
    {
        if (!EquipmentRepairUtility.IsRepairRecipe(bill?.recipe))
        {
            return true;
        }
        __result = EquipmentRepairUtility.TryFindRepairIngredients(bill, pawn, billGiver, chosen, missingIngredients);
        return false;
    }
}
