using System.Collections.Generic;
using Verse;

namespace Core40k;

/// <summary>
/// One cost charged when a variant is chosen for a slot. Subclass and override to charge anything
/// at all: resources, a currency, a stat, a hediff. The base class is free.
/// </summary>
public class GeneVariantCost
{
    public virtual bool CanAfford(Pawn pawn, GeneVariantDef variant, out string reason)
    {
        reason = null;
        return true;
    }

    public virtual void Pay(Pawn pawn, GeneVariantDef variant)
    {
    }

    public virtual string Description(Pawn pawn, GeneVariantDef variant)
    {
        return null;
    }

    public virtual IEnumerable<string> ConfigErrors(GeneVariantDef variant)
    {
        yield break;
    }
}

/// <summary>
/// Consumes items from storage on the pawn's map, using the same availability rules as decoration
/// upgrades. No hauling: the items are destroyed where they lie.
/// </summary>
public class GeneVariantCost_Resources : GeneVariantCost
{
    public List<ThingDefCountClass> cost = [];

    public override bool CanAfford(Pawn pawn, GeneVariantDef variant, out string reason)
    {
        reason = null;
        if (cost.NullOrEmpty())
        {
            return true;
        }

        if (pawn?.Map == null)
        {
            reason = "BEWH.Framework.GeneCustomization.NotOnMap".Translate();
            return false;
        }

        if (UpgradeCostUtility.CanAfford(pawn, cost, out var missing))
        {
            return true;
        }

        reason = "BEWH.Framework.Customization.MissingResource".Translate(missing.thingDef.LabelCap, missing.count);
        return false;
    }

    public override void Pay(Pawn pawn, GeneVariantDef variant)
    {
        UpgradeCostUtility.Consume(pawn, cost);
    }

    public override string Description(Pawn pawn, GeneVariantDef variant)
    {
        return UpgradeCostUtility.CostToString(cost);
    }

    public override IEnumerable<string> ConfigErrors(GeneVariantDef variant)
    {
        if (cost.NullOrEmpty())
        {
            yield break;
        }

        foreach (var thingCount in cost)
        {
            if (thingCount.thingDef == null)
            {
                yield return "cost entry has no thingDef";
            }
            else if (thingCount.count <= 0)
            {
                yield return "cost entry for " + thingCount.thingDef.defName + " has a count of " + thingCount.count;
            }
        }
    }
}
