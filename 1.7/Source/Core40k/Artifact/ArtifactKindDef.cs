using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

public class ArtifactKindDef : Def
{
    public List<string> traitTags = [];

    public List<string> weaponTags = [];

    public List<ArtifactStage> stages = [];

    public RequirementData wielderRequirements;

    public List<ForcedDecoration> forcedDecorations = [];

    public ColourPresetDef forcedColours;

    public bool lockColours = false;

    public QualityCategory? qualityOnCreate;

    public RulePackDef nameRulePack;

    [MustTranslate]
    public List<string> namerLabels;

    public ThoughtDef destroyedThought;

    public Type workerClass = typeof(ArtifactKindWorker);

    [Unsaved]
    private ArtifactKindWorker worker;

    public ArtifactKindWorker Worker
    {
        get
        {
            if (worker == null)
            {
                worker = (ArtifactKindWorker)Activator.CreateInstance(workerClass);
                worker.def = this;
            }
            return worker;
        }
    }

    public bool HasStages => !stages.NullOrEmpty();

    public ArtifactStage StageAt(int index)
    {
        return index >= 0 && index < stages.Count ? stages[index] : null;
    }

    public bool AllowsTrait(ArtifactTraitDef trait)
    {
        if (trait == null)
        {
            return false;
        }
        if (traitTags.NullOrEmpty())
        {
            return true;
        }
        return !trait.kindTags.NullOrEmpty() && trait.kindTags.Any(tag => traitTags.Contains(tag));
    }

    public bool AllowsWeapon(ThingDef weaponDef)
    {
        if (weaponDef == null)
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

        if (!typeof(ArtifactKindWorker).IsAssignableFrom(workerClass))
        {
            yield return "workerClass " + workerClass + " does not derive from ArtifactKindWorker";
        }

        if (wielderRequirements != null)
        {
            foreach (var error in wielderRequirements.ConfigErrors(this, nameof(wielderRequirements)))
            {
                yield return error;
            }
        }

        if (stages.NullOrEmpty())
        {
            yield return "no stages - an artifact kind needs at least stage 0";
            yield break;
        }

        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage == null)
            {
                yield return "stage " + i + " is null";
                continue;
            }
            if (stage.traitCount.max <= 0 && stage.drawbackCount.max <= 0)
            {
                yield return "stage " + i + " rolls neither traits nor drawbacks";
            }
            if (i > 0 && !stage.HasConditions)
            {
                yield return "stage " + i + " has no conditions and can never be reached";
            }
            if (stage.conditions.Any(condition => condition == null))
            {
                yield return "stage " + i + " has a null condition";
            }
        }

        foreach (var forced in forcedDecorations)
        {
            if (forced?.def == null)
            {
                yield return "forcedDecorations entry has no def";
            }
        }
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (stages.NullOrEmpty() || !stages.Any(stage => stage != null && stage.drawbackCount.min > 0))
        {
            return;
        }

        if (!DefDatabase<ArtifactTraitDef>.AllDefsListForReading.Any(trait => trait.isDrawback && AllowsTrait(trait)))
        {
            Log.Warning("[RimDark] " + defName + " requires drawbacks but no drawback trait matches its traitTags.");
        }
    }
}
