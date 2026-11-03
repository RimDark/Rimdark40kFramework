using Verse;

namespace Core40k;

public enum TabTargetKind
{
    Any = 0,
    Apparel = 1,
    Equipment = 2,
    //Non-humanlike pawn races (mechs, animals). Any only matches items.
    Pawn = 3,
}

public abstract class CustomizationTabWorker
{
    public CustomizationTabDef def;

    public abstract bool AppliesTo(ThingDef thingDef);

    protected bool MatchesKind(ThingDef thingDef)
    {
        if (thingDef == null)
        {
            return false;
        }

        return def.targetKind switch
        {
            TabTargetKind.Apparel => thingDef.IsApparel,
            TabTargetKind.Equipment => thingDef.IsWeapon,
            TabTargetKind.Pawn => thingDef.race is { Humanlike: false },
            _ => thingDef.race == null,
        };
    }

    protected bool HasRequiredComps(ThingDef thingDef)
    {
        foreach (var compType in def.requiredComps)
        {
            if (!thingDef.HasCompAssignable(compType))
            {
                return false;
            }
        }

        return true;
    }
}

//Comps only. Used by the coloring tabs, where having CompMultiColor is the whole requirement.
public class CustomizationTabWorker_Comp : CustomizationTabWorker
{
    public override bool AppliesTo(ThingDef thingDef)
    {
        return MatchesKind(thingDef) && HasRequiredComps(thingDef);
    }
}

//Comps plus a base texture or alternate form that can be recoloured.
public class CustomizationTabWorker_Coloring : CustomizationTabWorker_Comp
{
    public override bool AppliesTo(ThingDef thingDef)
    {
        if (!base.AppliesTo(thingDef))
        {
            return false;
        }

        if (thingDef.GetCompProperties<CompProperties_MultiColor>() is not { baseTextureColourable: false })
        {
            return true;
        }

        if (!thingDef.HasCompAssignable(typeof(CompAlternateTexture)))
        {
            return false;
        }

        foreach (var alternate in DecorationIndex.AlternatesFor(thingDef))
        {
            if (alternate.colorAmount > 0)
            {
                return true;
            }
        }

        return false;
    }
}

//Comps plus at least one applicable cosmetic decoration.
public class CustomizationTabWorker_Decoration : CustomizationTabWorker_Comp
{
    public override bool AppliesTo(ThingDef thingDef)
    {
        return base.AppliesTo(thingDef) && DecorationIndex.HasDecorations(thingDef, upgrades: false);
    }
}

//Comps plus at least one applicable upgrade.
public class CustomizationTabWorker_Upgrade : CustomizationTabWorker_Comp
{
    public override bool AppliesTo(ThingDef thingDef)
    {
        return base.AppliesTo(thingDef) && DecorationIndex.HasDecorations(thingDef, upgrades: true);
    }
}

//Comps plus at least one alternate base form.
public class CustomizationTabWorker_AlternateTexture : CustomizationTabWorker_Comp
{
    public override bool AppliesTo(ThingDef thingDef)
    {
        return base.AppliesTo(thingDef) && DecorationIndex.AlternatesFor(thingDef).Count > 0;
    }
}
