using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Core40k;

[HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
public static class HarmonyPatch_RepairProducts
{
    public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, RecipeDef recipeDef, List<Thing> ingredients)
    {
        foreach (var product in __result)
        {
            yield return product;
        }
        if (!EquipmentRepairUtility.IsRepairRecipe(recipeDef) || ingredients == null)
        {
            yield break;
        }
        foreach (var ingredient in ingredients)
        {
            if (!EquipmentRepairUtility.IsRepairTarget(ingredient, recipeDef) || ingredient.Destroyed)
            {
                continue;
            }
            ingredient.HitPoints = ingredient.MaxHitPoints;
            if (ingredient.Spawned)
            {
                ingredient.DeSpawn();
            }
            yield return ingredient;
        }
    }
}
