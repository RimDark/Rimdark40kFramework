using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class Comp_PhysicalShield : ThingComp
{
    private int lastBlockTick = -99999;

    private int lastAttackedTick = -99999;

    public bool Visible;

    public CompProperties_PhysicalShield Props => (CompProperties_PhysicalShield)props;

    public Pawn Wearer => (parent as Apparel)?.Wearer;

    public bool OnCooldown => Find.TickManager.TicksGame - lastBlockTick < Props.blockCooldownTicks;

    public bool AttackedRecently => Find.TickManager.TicksGame - lastAttackedTick < ShieldUtils.AttackedVisibleTicks;

    /// <summary>
    /// Records that the wearer was attacked, so the shield is drawn when shields are only shown in combat.
    /// </summary>
    public void Notify_Attacked(Pawn pawn)
    {
        lastAttackedTick = Find.TickManager.TicksGame;
        UpdateVisibility(pawn);
    }

    /// <summary>
    /// Re-evaluates whether the shield should be drawn and refreshes the wearer's graphics when that changes.
    /// </summary>
    public void UpdateVisibility(Pawn pawn)
    {
        var visible = ShieldUtils.ShouldShowShield(pawn, this);
        if (visible == Visible)
        {
            return;
        }

        Visible = visible;
        ShieldUtils.RefreshShieldGraphic(pawn, parent as Apparel);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref lastBlockTick, "lastBlockTick", -99999);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var equippable = parent.GetComp<CompEquippable>();
            if (equippable != null && equippable.verbTracker == null)
            {
                equippable.verbTracker = new VerbTracker(equippable);
            }

            SetBashVerbCaster(Wearer);
        }
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = false;
        var pawn = Wearer;
        if (pawn == null)
        {
            return;
        }

        ShieldUtils.TryApplyShield(pawn, this, dinfo, out absorbed);
    }

    public override void Notify_Equipped(Pawn pawn)
    {
        base.Notify_Equipped(pawn);
        SetBashVerbCaster(pawn);
    }

    public override void Notify_Unequipped(Pawn pawn)
    {
        base.Notify_Unequipped(pawn);
        SetBashVerbCaster(null);
    }

    /// <summary>
    /// Vanilla adds worn apparel's melee verbs to the wearer's melee pool but never assigns them a caster.
    /// </summary>
    private void SetBashVerbCaster(Pawn pawn)
    {
        var verbs = parent.GetComp<CompEquippable>()?.AllVerbs;
        if (verbs == null)
        {
            return;
        }

        for (var i = 0; i < verbs.Count; i++)
        {
            verbs[i].caster = pawn;
        }
    }

    /// <summary>
    /// Records a successful block: cooldown, melee training, feedback and wear on the shield.
    /// </summary>
    public void Notify_Blocked(Pawn pawn, DamageInfo dinfo)
    {
        lastBlockTick = Find.TickManager.TicksGame;

        if (Props.blockSkillXp > 0f)
        {
            pawn.skills?.Learn(SkillDefOf.Melee, Props.blockSkillXp);
        }

        PlayBlockEffects(pawn, dinfo);
        TryCounterBash(pawn, dinfo);

        if (!parent.def.useHitPoints)
        {
            return;
        }

        var loss = Math.Max(Props.minHitPointLoss, GenMath.RoundRandom(dinfo.Amount * Props.hitPointLossFactor));
        if (loss > 0)
        {
            parent.TakeDamage(new DamageInfo(dinfo.Def, loss));
        }
    }

    private void TryCounterBash(Pawn pawn, DamageInfo dinfo)
    {
        var p = Props;
        if (p.counterBashChance <= 0f || ShieldBashUtility.ApplyingQueued || dinfo.Def.isRanged || dinfo.Def.isExplosive)
        {
            return;
        }

        if (dinfo.Instigator is not Pawn attacker || attacker == pawn || !attacker.Spawned || !pawn.Spawned || attacker.Map != pawn.Map || !attacker.Position.AdjacentTo8WayOrInside(pawn.Position))
        {
            return;
        }

        var chance = Math.Clamp(p.counterBashChance * pawn.GetStatValue(Core40kDefOf.BEWH_ShieldProficiency), 0f, 1f);
        if (!Rand.Chance(chance))
        {
            return;
        }

        ShieldBashUtility.Queue(pawn, attacker, new ShieldBashParams
        {
            damageDef = p.counterBashDamageDef,
            damageAmount = p.counterBashDamage,
            armorPenetration = p.counterBashArmorPenetration,
            stunTicks = p.counterBashStunTicks,
            knockbackCells = p.counterBashKnockbackCells,
            maxKnockbackBodySize = p.counterBashMaxKnockbackBodySize,
            weapon = parent.def
        });

        if (p.showBlockText)
        {
            MoteMaker.ThrowText(attacker.DrawPos, attacker.Map, "BEWH.Framework.Shield.CounterBash".Translate());
        }
    }

    private void PlayBlockEffects(Pawn pawn, DamageInfo dinfo)
    {
        if (!pawn.Spawned)
        {
            return;
        }

        var map = pawn.Map;
        var effecterDef = Props.blockEffecter ?? DefaultBlockEffecter(dinfo);
        if (effecterDef != null)
        {
            var health = pawn.health;
            if (health.deflectionEffecter == null || health.deflectionEffecter.def != effecterDef)
            {
                health.deflectionEffecter?.Cleanup();
                health.deflectionEffecter = effecterDef.Spawn();
            }

            var target = new TargetInfo(pawn.Position, map);
            health.deflectionEffecter.Trigger(target, dinfo.Instigator != null ? (TargetInfo)dinfo.Instigator : target);
        }

        pawn.Drawer.Notify_DamageDeflected(dinfo);

        if (Props.showBlockText)
        {
            MoteMaker.ThrowText(pawn.DrawPos, map, "BEWH.Framework.Shield.Blocked".Translate());
        }
    }

    private EffecterDef DefaultBlockEffecter(DamageInfo dinfo)
    {
        var bullet = dinfo.Def == DamageDefOf.Bullet;
        if (parent.def.apparel?.useDeflectMetalEffect == true && dinfo.Def.canUseDeflectMetalEffect)
        {
            return bullet ? EffecterDefOf.Deflect_Metal_Bullet : EffecterDefOf.Deflect_Metal;
        }

        return bullet ? EffecterDefOf.Deflect_General_Bullet : EffecterDefOf.Deflect_General;
    }

    public override void CompDrawWornExtras()
    {
        base.CompDrawWornExtras();

        var pawn = Wearer;
        if (pawn != null)
        {
            UpdateVisibility(pawn);
        }

        if (!Prefs.DevMode || !DebugSettings.godMode)
        {
            return;
        }

        if (pawn is not { Spawned: true } || !Find.Selector.IsSelected(pawn))
        {
            return;
        }

        var center = pawn.DrawPos;
        center.y = AltitudeLayer.MetaOverlays.AltitudeFor();
        var facing = pawn.Rotation.AsAngle;
        var frontHalf = Props.frontArcDegrees / 2f;
        var backEdge = 180f - Props.backArcDegrees / 2f;

        DrawZoneEdge(center, facing - frontHalf, SimpleColor.Green);
        DrawZoneEdge(center, facing + frontHalf, SimpleColor.Green);
        DrawZoneEdge(center, facing - backEdge, SimpleColor.Red);
        DrawZoneEdge(center, facing + backEdge, SimpleColor.Red);
        DrawZoneEdge(center, facing + (Props.shieldOnLeft ? -90f : 90f), SimpleColor.Cyan);
    }

    private static void DrawZoneEdge(Vector3 center, float angle, SimpleColor color)
    {
        GenDraw.DrawLineBetween(center, center + Vector3Utility.HorizontalVectorFromAngle(angle) * 1.5f, color, 0.05f);
    }

}
