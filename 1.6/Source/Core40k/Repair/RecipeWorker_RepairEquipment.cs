using Verse;

namespace Core40k;

public class RecipeWorker_RepairEquipment : RecipeWorker
{
    public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
    {
        if (EquipmentRepairUtility.IsRepairTarget(ingredient, recipe))
        {
            return;
        }
        base.ConsumeIngredient(ingredient, recipe, map);
    }
}
