using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using VEF.AestheticScaling;
using Verse;
using Verse.Sound;

namespace Core40k;

/// <summary>
/// A shield field that is dormant until something raises the item's EnergyShieldEnergyMax above
/// zero, so an item can carry the comp permanently and have a fitted upgrade switch it on.
/// Unlike a shield belt it defaults to blocksRangedWeapons false, so the wearer can still shoot.
/// </summary>
[StaticConstructorOnStartup]
public class Comp_ConversionField : CompShield
{
    private static readonly Material FieldBubbleMat = MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent);

    private Vector3 fieldImpactAngleVect;
    private int fieldLastAbsorbDamageTick = -9999;

    private CompProperties_ConversionField FieldProps => props as CompProperties_ConversionField;

    //FieldActive is read every tick on every item with this comp, worn or stored, so the result is cached.
    //Fitting or removing a decoration resets it (Notify_FieldSourceChanged); anything else that moves
    //EnergyShieldEnergyMax is picked up within FieldCheckIntervalTicks.
    private const int FieldCheckIntervalTicks = 250;
    private bool fieldActiveCached;
    private int fieldActiveCheckedTick = -1;

    private bool FieldActive
    {
        get
        {
            var tick = Find.TickManager?.TicksGame ?? 0;
            if (fieldActiveCheckedTick < 0 || tick < fieldActiveCheckedTick || tick - fieldActiveCheckedTick >= FieldCheckIntervalTicks)
            {
                fieldActiveCached = HasFieldSource && parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax) > 0f;
                fieldActiveCheckedTick = tick;
            }

            return fieldActiveCached;
        }
    }

    /// <summary>
    /// Makes the next FieldActive check recompute. Called when the item's decorations change.
    /// </summary>
    public void Notify_FieldSourceChanged()
    {
        fieldActiveCheckedTick = -1;
    }

    /// <summary>
    /// True only while a fitted decoration is what supplies the field. EnergyShieldEnergyMax has no
    /// defaultBaseValue of its own, so a StatDef default of 1 would otherwise give every item with
    /// this comp a full shield; an item meant to carry a field of its own declares the stat instead.
    /// </summary>
    private bool HasFieldSource
    {
        get
        {
            if (parent.def.StatBaseDefined(StatDefOf.EnergyShieldEnergyMax)
                && parent.def.GetStatValueAbstract(StatDefOf.EnergyShieldEnergyMax) > 0f)
            {
                return true;
            }

            foreach (var comp in parent.AllComps)
            {
                if (comp is not CompDecorativeBase decorative)
                {
                    continue;
                }

                foreach (var decoration in decorative.Decorations)
                {
                    if (decoration.Key != null
                        && decoration.Key.statOffsets.GetStatOffsetFromList(StatDefOf.EnergyShieldEnergyMax) > 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public override void CompTick()
    {
        if (!FieldActive)
        {
            energy = 0f;
            ticksToReset = -1;
            return;
        }

        base.CompTick();
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = false;
        if (!FieldActive || ShieldState != ShieldState.Active || PawnOwner == null)
        {
            return;
        }

        if (dinfo.Def == DamageDefOf.EMP)
        {
            energy = 0f;
            BreakField();
        }
        else if (!dinfo.Def.ignoreShields && (dinfo.Def.isRanged || dinfo.Def.isExplosive))
        {
            energy -= dinfo.Amount * Props.energyLossPerDamage;
            if (energy < 0f)
            {
                BreakField();
            }
            else
            {
                FieldAbsorbedDamage(dinfo);
            }
            absorbed = true;
        }
    }

    /// <summary>
    /// The wearer's drawn size relative to a baseline pawn, taken from VEF's render cache so body size stats count.
    /// </summary>
    public float DrawScale
    {
        get
        {
            var pawn = PawnOwner;
            if (pawn == null || FieldProps is { scaleWithVisualSize: false })
            {
                return 1f;
            }

            var cache = pawn.GetCachePrePatched();
            if (cache == null)
            {
                return 1f;
            }

            var xScale = cache.vCosmeticScale.x; 
            var zScale = cache.vCosmeticScale.z; 

            var scale = (xScale + zScale) / 2f;
            return scale > 0f ? scale : 1f;
        }
    }

    private float CurrentDrawSize => Waylith.Math.Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy) * DrawScale;

    private void FieldAbsorbedDamage(DamageInfo dinfo)
    {
        var pawn = PawnOwner;
        SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        fieldImpactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
        var loc = pawn.TrueCenter() + fieldImpactAngleVect.RotatedBy(180f) * 0.5f * DrawScale;
        var flashSize = Math.Min(10f, 2f + dinfo.Amount / 10f);
        FleckMaker.Static(loc, pawn.Map, FleckDefOf.ExplosionFlash, flashSize);
        var puffs = (int)flashSize;
        for (var i = 0; i < puffs; i++)
        {
            FleckMaker.ThrowDustPuff(loc, pawn.Map, Rand.Range(0.8f, 1.2f));
        }
        fieldLastAbsorbDamageTick = Find.TickManager.TicksGame;
        KeepDisplaying();
    }

    private void BreakField()
    {
        var pawn = PawnOwner;
        if (parent.Spawned || (pawn != null && pawn.Spawned))
        {
            var scale = CurrentDrawSize;
            EffecterDefOf.Shield_Break.SpawnAttached(pawn ?? parent, parent.MapHeld, scale);
            var center = pawn?.TrueCenter() ?? parent.TrueCenter();
            FleckMaker.Static(center, parent.MapHeld, FleckDefOf.ExplosionFlash, 12f);
            for (var i = 0; i < 6; i++)
            {
                FleckMaker.ThrowDustPuff(center + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f) * DrawScale, parent.MapHeld, Rand.Range(0.8f, 1.2f));
            }
        }
        energy = 0f;
        ticksToReset = Props.startingTicksToReset;
    }

    private void DrawField()
    {
        if (ShieldState != ShieldState.Active || !ShouldDisplay)
        {
            return;
        }

        var size = CurrentDrawSize;
        var drawPos = PawnOwner.Drawer.DrawPos;
        drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        var ticksSinceImpact = Find.TickManager.TicksGame - fieldLastAbsorbDamageTick;
        if (ticksSinceImpact < 8)
        {
            var jitter = (8 - ticksSinceImpact) / 8f * 0.05f;
            drawPos += fieldImpactAngleVect * jitter;
            size -= jitter;
        }
        var angle = Rand.Range(0, 360);
        var matrix = default(Matrix4x4);
        matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
        Graphics.DrawMesh(MeshPool.plane10, matrix, FieldBubbleMat, 0);
    }

    public override void CompDrawWornExtras()
    {
        if (!FieldActive || !IsApparel)
        {
            return;
        }

        DrawField();
    }

    public override void PostDraw()
    {
        if (!FieldActive || IsApparel)
        {
            return;
        }

        DrawField();
    }

    public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
    {
        if (!FieldActive)
        {
            yield break;
        }

        foreach (var gizmo in base.CompGetWornGizmosExtra())
        {
            yield return gizmo;
        }
    }

    public override float CompGetSpecialApparelScoreOffset()
    {
        return !FieldActive ? 0f : base.CompGetSpecialApparelScoreOffset();
    }
}
