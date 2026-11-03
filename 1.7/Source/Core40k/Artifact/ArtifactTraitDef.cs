using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

public class ArtifactTraitDef : Def
{
    public List<string> kindTags = [];

    public List<string> weaponTags = [];

    public bool meleeOnly = false;

    public bool rangedOnly = false;

    public List<string> exclusionTags = [];

    public float commonality = 1f;

    public bool canGenerateAlone = true;

    public bool isDrawback = false;

    [MustTranslate]
    public List<string> traitAdjectives = [];

    //Offsets and factors on the weapon's own stats, applied by vanilla through ThingComp.GetStatOffset/GetStatFactor.
    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];

    //Offsets ride StatWorker.StatOffsetFromGear, factors ride the cached pawn stat route.
    public List<StatModifier> wielderStatOffsets = [];
    public List<StatModifier> wielderStatFactors = [];

    public VerbModifier verbModifier = null;

    public List<ExtraDamage> extraDamages = null;

    public DamageDef damageDefOverride;

    public float additionalStoppingPower = 0f;

    public List<HediffDef> equippedHediffs = [];

    public List<AbilityDef> givesAbilities = [];

    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];

    public ThoughtDef killThought;

    public List<ForcedDecoration> forcedDecorations = [];

    public ColourPresetDef forcedColours;

    public bool lockColours = false;

    public RequirementData requirements;

    public DevotionOnKill devotionOnKill;

    public Type workerClass = typeof(ArtifactTraitWorker);

    [Unsaved]
    private ArtifactTraitWorker worker;

    public ArtifactTraitWorker Worker
    {
        get
        {
            if (worker == null)
            {
                worker = (ArtifactTraitWorker)Activator.CreateInstance(workerClass);
                worker.def = this;
            }
            return worker;
        }
    }

    public bool HasVerbEffects => verbModifier != null || !extraDamages.NullOrEmpty() || damageDefOverride != null || additionalStoppingPower != 0f;

    public bool Overlaps(ArtifactTraitDef other)
    {
        if (other == this)
        {
            return true;
        }
        if (exclusionTags.NullOrEmpty() || other.exclusionTags.NullOrEmpty())
        {
            return false;
        }
        return exclusionTags.Any(tag => other.exclusionTags.Contains(tag));
    }

    public bool AppliesTo(ThingDef weaponDef)
    {
        if (weaponDef == null)
        {
            return false;
        }
        if (meleeOnly && !weaponDef.IsMeleeWeapon)
        {
            return false;
        }
        if (rangedOnly && !weaponDef.IsRangedWeapon)
        {
            return false;
        }
        if (weaponTags.NullOrEmpty())
        {
            return true;
        }
        return !weaponDef.weaponTags.NullOrEmpty() && weaponDef.weaponTags.Any(tag => weaponTags.Contains(tag));
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var configError in base.ConfigErrors())
        {
            yield return configError;
        }

        if (!typeof(ArtifactTraitWorker).IsAssignableFrom(workerClass))
        {
            yield return "workerClass " + workerClass + " does not derive from ArtifactTraitWorker";
        }
        if (requirements != null)
        {
            foreach (var error in requirements.ConfigErrors(this, nameof(requirements)))
            {
                yield return error;
            }
        }
        if (!isDrawback && commonality <= 0f)
        {
            yield return "commonality <= 0 on a non-drawback trait, it can never be rolled";
        }
        if (meleeOnly && rangedOnly)
        {
            yield return "both meleeOnly and rangedOnly are set";
        }
        if (meleeOnly && damageDefOverride != null)
        {
            yield return "damageDefOverride only applies to projectiles and this trait is meleeOnly";
        }
        if (devotionOnKill != null && devotionOnKill.creed == null)
        {
            yield return "devotionOnKill has no creed";
        }
        foreach (var forced in forcedDecorations)
        {
            if (forced?.def == null)
            {
                yield return "forcedDecorations entry has no def";
            }
        }
    }
}
