using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Core40k;

public static class EquipmentRepairUtility
{
    private static readonly List<Thing> tmpCandidates = [];
    private static readonly List<ThingCount> tmpChosenMaterials = [];

    public static float CostFactor => Core40kUtils.ModSettings?.repairCostFactor ?? Core40kModSettings.DefaultRepairCostFactor;

    public static bool IsRepairRecipe(RecipeDef recipe)
    {
        return recipe != null && recipe.HasModExtension<DefModExtension_RepairRecipe>();
    }

    /// <summary>
    /// Whether the thing is the equipment being repaired by this recipe, as opposed to a material.
    /// </summary>
    public static bool IsRepairTarget(Thing thing, RecipeDef recipe)
    {
        if (thing?.def == null || !thing.def.useHitPoints || recipe?.ingredients.NullOrEmpty() != false)
        {
            return false;
        }
        if (!thing.def.IsWeapon && !thing.def.IsApparel)
        {
            return false;
        }
        return recipe.ingredients[0].filter.Allows(thing.def);
    }

    public static bool IsDamaged(Thing thing)
    {
        return thing.HitPoints < thing.MaxHitPoints;
    }

    public static float MissingFraction(Thing thing)
    {
        if (thing.MaxHitPoints <= 0)
        {
            return 0f;
        }
        
        return Math.Max(0f, Math.Min(1f, 1f - (float)thing.HitPoints / thing.MaxHitPoints));
    }

    /// <summary>
    /// Materials needed to restore the thing to full hit points.
    /// </summary>
    public static List<ThingDefCountClass> GetRepairCost(Thing thing, DefModExtension_RepairRecipe extension)
    {
        var result = new List<ThingDefCountClass>();
        var factor = CostFactor;
        var missing = MissingFraction(thing);
        if (factor <= 0f || missing <= 0f)
        {
            return result;
        }

        var baseCost = GetBaseCost(thing, extension);
        if (baseCost.Count == 0)
        {
            return result;
        }

        ThingDef largestDef = null;
        var largestAmount = 0f;
        foreach (var (def, count) in baseCost)
        {
            var amount = count * missing * factor;
            if (amount > largestAmount)
            {
                largestAmount = amount;
                largestDef = def;
            }
            var rounded = (int)Math.Floor(amount + 0.5f);
            if (rounded > 0)
            {
                result.Add(new ThingDefCountClass(def, rounded));
            }
        }

        if (result.Count == 0 && largestDef != null)
        {
            result.Add(new ThingDefCountClass(largestDef, 1));
        }
        return result;
    }

    private static List<(ThingDef def, float count)> GetBaseCost(Thing thing, DefModExtension_RepairRecipe extension)
    {
        var baseCost = new List<(ThingDef def, float count)>();
        var costList = thing.CostListAdjusted();
        if (costList != null)
        {
            foreach (var cost in costList)
            {
                if (cost.thingDef != null && cost.count > 0 && cost.thingDef.CountAsResource)
                {
                    baseCost.Add((cost.thingDef, cost.count));
                }
            }
        }
        if (baseCost.Count > 0)
        {
            return baseCost;
        }

        var materialValue = thing.def.GetStatValueAbstract(StatDefOf.MarketValue, thing.Stuff) * extension.materialValueFraction;
        if (materialValue <= 0f)
        {
            return baseCost;
        }

        if (thing.def.techLevel >= TechLevel.Spacer)
        {
            AddValueShare(baseCost, ThingDefOf.Plasteel, materialValue * 0.7f);
            AddValueShare(baseCost, ThingDefOf.ComponentSpacer, materialValue * 0.3f);
        }
        else
        {
            AddValueShare(baseCost, ThingDefOf.Steel, materialValue * 0.8f);
            AddValueShare(baseCost, ThingDefOf.ComponentIndustrial, materialValue * 0.2f);
        }
        return baseCost;
    }

    private static void AddValueShare(List<(ThingDef def, float count)> list, ThingDef def, float value)
    {
        if (def == null || def.BaseMarketValue <= 0f)
        {
            return;
        }
        list.Add((def, value / def.BaseMarketValue));
    }

    public static float GetRepairWork(Thing thing, DefModExtension_RepairRecipe extension)
    {
        var workToMake = thing.def.GetStatValueAbstract(StatDefOf.WorkToMake, thing.Stuff);
        if (workToMake <= 0f)
        {
            workToMake = extension.fallbackWorkToMake;
        }
        return Math.Max(extension.minWork, Math.Min(extension.maxWork, workToMake * MissingFraction(thing) * extension.workFactor));
    }

    /// <summary>
    /// The item being repaired for a running bill, checking the given thing first and then the bench's ingredient cells.
    /// </summary>
    public static Thing FindRepairTarget(Bill bill, Thing thing)
    {
        if (IsRepairTarget(thing, bill.recipe))
        {
            return thing;
        }
        if (bill.billStack?.billGiver is not Thing { Spawned: true } giver)
        {
            return null;
        }
        foreach (var cell in bill.billStack.billGiver.IngredientStackCells)
        {
            var things = cell.GetThingList(giver.Map);
            for (var i = 0; i < things.Count; i++)
            {
                if (IsRepairTarget(things[i], bill.recipe) && IsDamaged(things[i]))
                {
                    return things[i];
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the nearest damaged item the bill allows whose repair materials are available, and fills chosen with the materials followed by the item.
    /// </summary>
    public static bool TryFindRepairIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients)
    {
        chosen.Clear();
        var extension = bill.recipe.GetModExtension<DefModExtension_RepairRecipe>();
        var map = pawn.Map;
        if (map == null || billGiver == null)
        {
            return false;
        }

        var rootCell = billGiver is Building { def.hasInteractionCell: true } building ? building.InteractionCell : billGiver.Position;
        var radiusSq = bill.ingredientSearchRadius * bill.ingredientSearchRadius;

        tmpCandidates.Clear();
        AddCandidates(map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon), bill, pawn, billGiver, radiusSq);
        AddCandidates(map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel), bill, pawn, billGiver, radiusSq);
        if (tmpCandidates.Count == 0)
        {
            return false;
        }
        tmpCandidates.SortBy(t => (t.Position - rootCell).LengthHorizontalSquared);

        List<IngredientCount> firstMissing = null;
        var checkedCount = 0;
        try
        {
            foreach (var candidate in tmpCandidates)
            {
                if (checkedCount >= extension.maxCandidates)
                {
                    break;
                }
                if (!pawn.CanReach(candidate, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    continue;
                }
                checkedCount++;

                var ingredients = ToIngredientCounts(GetRepairCost(candidate, extension));
                if (ingredients.Count == 0)
                {
                    chosen.Add(new ThingCount(candidate, 1));
                    return true;
                }

                tmpChosenMaterials.Clear();
                if (WorkGiver_DoBill.TryFindBestFixedIngredients(ingredients, pawn, billGiver, tmpChosenMaterials, bill.ingredientSearchRadius))
                {
                    chosen.AddRange(tmpChosenMaterials);
                    chosen.Add(new ThingCount(candidate, 1));
                    return true;
                }
                firstMissing ??= ingredients;
            }
        }
        finally
        {
            tmpCandidates.Clear();
            tmpChosenMaterials.Clear();
        }

        if (missingIngredients != null && firstMissing != null)
        {
            missingIngredients.AddRange(firstMissing);
        }
        return false;
    }

    private static void AddCandidates(List<Thing> things, Bill bill, Pawn pawn, Thing billGiver, float radiusSq)
    {
        for (var i = 0; i < things.Count; i++)
        {
            var thing = things[i];
            if (!thing.Spawned || tmpCandidates.Contains(thing))
            {
                continue;
            }
            if (!IsRepairTarget(thing, bill.recipe) || !IsDamaged(thing) || !bill.IsFixedOrAllowedIngredient(thing))
            {
                continue;
            }
            if ((thing.Position - billGiver.Position).LengthHorizontalSquared >= radiusSq)
            {
                continue;
            }
            if (thing.IsForbidden(pawn) || !pawn.CanReserve(thing))
            {
                continue;
            }
            tmpCandidates.Add(thing);
        }
    }

    private static List<IngredientCount> ToIngredientCounts(List<ThingDefCountClass> cost)
    {
        var result = new List<IngredientCount>(cost.Count);
        foreach (var entry in cost)
        {
            var ingredient = new IngredientCount();
            ingredient.filter.SetAllow(entry.thingDef, true);
            ingredient.filter.customSummary = entry.thingDef.label;
            ingredient.SetBaseCount(entry.count);
            result.Add(ingredient);
        }
        return result;
    }
}
