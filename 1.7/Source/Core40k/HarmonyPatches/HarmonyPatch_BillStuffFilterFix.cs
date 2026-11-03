using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Bills saved before their product became stuffable (e.g. by a patch) kept an ingredient filter that allows no stuff,
/// so the bill can never start and no material can be picked. On load such bills get the recipe's default filter.
/// </summary>
[HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]
public static class HarmonyPatch_BillStuffFilterFix
{
    public static void Postfix(Bill __instance)
    {
        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }

        var recipe = __instance.recipe;
        if (recipe == null || !recipe.productHasIngredientStuff || __instance.ingredientFilter == null || recipe.ingredients.NullOrEmpty())
        {
            return;
        }

        //The stuff ingredient is the first one, and the only one with a choice of things.
        var stuffIngredient = recipe.ingredients[0];
        if (stuffIngredient.IsFixedIngredient)
        {
            return;
        }
        foreach (var stuff in stuffIngredient.filter.AllowedThingDefs)
        {
            if (__instance.ingredientFilter.Allows(stuff))
            {
                return;
            }
        }

        __instance.ingredientFilter.CopyAllowancesFrom(recipe.defaultIngredientFilter ?? recipe.fixedIngredientFilter);
        Log.Message($"[Core40k] Bill {__instance.LabelCap} allowed no material; reset it to the recipe's default ingredients.");
    }
}
