using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

public class CompProperties_PhysicalShield : CompProperties
{
    public float frontArcDegrees = 90f;
    public float backArcDegrees = 90f;

    public float meleeBlockChance = 0.40f;
    public float rangedBlockChance = 0.40f;

    public float frontBlockFactor = 1f;
    public float sideBlockFactor = 0.4f;
    public float backBlockFactor = 0f;

    public bool shieldOnLeft = true;
    public float offSideFactor = 1f;

    public bool blocksExplosions = false;
    public List<DamageDef> neverBlocks;

    public float shieldArmorRating = 0.4f;
    public float maxBlockChance = 0.9f;
    public float undraftedFactor = 0.5f;
    public int blockCooldownTicks = 20;

    public float hitPointLossFactor = 0.25f;
    public int minHitPointLoss = 1;

    public bool requiresManipulation = true;
    public bool hideWhenInactive = false;

    public float blockSkillXp = 50f;

    public float counterBashChance = 0f;
    public int counterBashStunTicks = 60;
    public float counterBashDamage = 0f;
    public DamageDef counterBashDamageDef;
    public float counterBashArmorPenetration = -1f;
    public int counterBashKnockbackCells = 0;
    public float counterBashMaxKnockbackBodySize = 1.5f;

    public EffecterDef blockEffecter;
    public bool showBlockText = true;

    public CompProperties_PhysicalShield()
    {
        compClass = typeof(Comp_PhysicalShield);
    }

    /// <summary>
    /// Zone for the signed angle between the wearer's facing and the direction of the attacker (-180..180).
    /// </summary>
    public ShieldZone ZoneFor(float delta)
    {
        var abs = Math.Abs(delta);
        if (abs <= frontArcDegrees / 2f)
        {
            return ShieldZone.Front;
        }

        if (abs >= 180f - backArcDegrees / 2f)
        {
            return ShieldZone.Back;
        }

        return ShieldZone.Side;
    }

    /// <summary>
    /// Whether a negative delta (the wearer's left) or a positive one is the side without the shield.
    /// </summary>
    public bool IsWeaponArmSide(ShieldZone zone, float delta)
    {
        if (zone != ShieldZone.Side)
        {
            return false;
        }

        return shieldOnLeft ? delta > 0f : delta < 0f;
    }

    /// <summary>
    /// Base block chance for a hit from the given zone, before the wearer's proficiency and readiness.
    /// </summary>
    public float BlockChanceFor(ShieldZone zone, bool weaponArmSide, bool ranged)
    {
        var zoneFactor = zone switch
        {
            ShieldZone.Front => frontBlockFactor,
            ShieldZone.Side => sideBlockFactor,
            _ => backBlockFactor
        };

        if (weaponArmSide)
        {
            zoneFactor *= offSideFactor;
        }

        return (ranged ? rangedBlockChance : meleeBlockChance) * zoneFactor;
    }

    /// <summary>
    /// Shield stats for the info card. On the props rather than the comp so they also show for the def (crafting menu, research, trade).
    /// </summary>
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (var entry in base.SpecialDisplayStats(req))
        {
            yield return entry;
        }

        var category = Core40kDefOf.BEWH_Shields;
        var priority = 5000;

        yield return Entry(category, "MeleeBlock", meleeBlockChance.ToStringPercent(), priority--);
        yield return Entry(category, "RangedBlock", rangedBlockChance.ToStringPercent(), priority--);

        if (frontBlockFactor != 1f)
        {
            yield return Entry(category, "FrontFactor", "x" + frontBlockFactor.ToStringPercent(), priority--);
        }

        yield return Entry(category, "SideFactor", "x" + sideBlockFactor.ToStringPercent(), priority--);
        yield return Entry(category, "BackFactor", "x" + backBlockFactor.ToStringPercent(), priority--);

        yield return Entry(category, "FrontArc", frontArcDegrees.ToString("F0") + "°", priority--);
        yield return Entry(category, "BackArc", backArcDegrees.ToString("F0") + "°", priority--);

        if (offSideFactor != 1f)
        {
            yield return Entry(category, "OffSide", "x" + offSideFactor.ToStringPercent(), priority--);
        }

        yield return Entry(category, "ArmorRating", shieldArmorRating.ToStringPercent(), priority--);
        yield return Entry(category, "MaxBlock", maxBlockChance.ToStringPercent(), priority--);
        yield return Entry(category, "Explosions", blocksExplosions ? "Yes".Translate() : "No".Translate(), priority--);

        if (counterBashChance > 0f)
        {
            yield return Entry(category, "CounterBash", counterBashChance.ToStringPercent(), priority);
        }
    }

    private static StatDrawEntry Entry(StatCategoryDef category, string key, string value, int priority)
    {
        return new StatDrawEntry(category,
            ("BEWH.Framework.Shield.Stat." + key).Translate(),
            value,
            ("BEWH.Framework.Shield.Stat." + key + "Desc").Translate(),
            priority);
    }

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (var error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }

        if (parentDef.apparel == null)
        {
            yield return $"{nameof(CompProperties_PhysicalShield)} on {parentDef.defName}, which is not apparel.";
        }
        else if (parentDef.apparel.layers.NullOrEmpty() || !parentDef.apparel.layers.Any(layer => layer?.defName == "BEWH_Shield"))
        {
            yield return $"{nameof(CompProperties_PhysicalShield)} on {parentDef.defName}, which is not on the BEWH_Shield apparel layer.";
        }

        if (frontArcDegrees < 0f || backArcDegrees < 0f || frontArcDegrees + backArcDegrees > 360f)
        {
            yield return $"{nameof(CompProperties_PhysicalShield)} on {parentDef.defName} has invalid arcs: front and back must be non-negative and add up to at most 360.";
        }

        if (meleeBlockChance is < 0f or > 1f || rangedBlockChance is < 0f or > 1f)
        {
            yield return $"{nameof(CompProperties_PhysicalShield)} on {parentDef.defName} has a block chance outside 0-1.";
        }

        if (frontBlockFactor < 0f || sideBlockFactor < 0f || backBlockFactor < 0f)
        {
            yield return $"{nameof(CompProperties_PhysicalShield)} on {parentDef.defName} has a negative zone block factor.";
        }

        if (!parentDef.tools.NullOrEmpty() && (parentDef.comps == null || !parentDef.comps.Any(comp => comp.compClass != null && typeof(CompEquippable).IsAssignableFrom(comp.compClass))))
        {
            yield return $"{nameof(CompProperties_PhysicalShield)} on {parentDef.defName} has tools but no CompProperties_Equippable, so the shield bash attacks will never be used.";
        }
    }
}
