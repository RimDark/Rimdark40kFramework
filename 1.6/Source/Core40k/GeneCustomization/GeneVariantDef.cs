using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public enum GeneStatMode
{
    Add,
    Replace,
}

public enum GeneVariantColourSource
{
    None,
    Skin,
    Hair,
}

/// <summary>
/// A part that can fill a customizable gene slot: a new look, new stats, or both. Mirrors
/// DecorationDef where the meaning is the same so the dialog and tooltips can share code paths.
/// </summary>
public class GeneVariantDef : Def
{
    [NoTranslate]
    public string iconPath;

    [Unsaved]
    private Texture2D icon;

    public Texture2D Icon
    {
        get
        {
            if (icon != null)
            {
                return icon;
            }

            icon = !iconPath.NullOrEmpty() ? ContentFinder<Texture2D>.Get(iconPath) : ContentFinder<Texture2D>.Get("NoTex");
            return icon;
        }
    }

    public float sortOrder = 0f;

    public List<GeneDef> appliesTo = [];
    public List<GeneSlotCategoryDef> appliesToCategories = [];
    public bool appliesToAll = false;

    public GeneStatMode statMode = GeneStatMode.Add;
    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];

    public bool hidesBaseGraphic = true;

    [NoTranslate]
    public string texPath;
    public PawnRenderNodeTagDef parentTagDef;
    public Type workerClass;
    public Vector2 drawSize = Vector2.one;
    public Vector2? overrideMeshSize;
    public DrawData drawData = new();
    public float baseLayer = 0f;
    public ShaderTypeDef shaderType;
    public bool useBodyType = false;
    public List<Rot4> visibleFacing;

    public bool colourable = false;
    public int colorAmount = 1;
    public Color? defaultColour;
    public Color? defaultColourTwo;
    public Color? defaultColourThree;
    public GeneVariantColourSource defaultColourSource = GeneVariantColourSource.None;
    public MaskDef defaultMask;
    public List<DecorationColourPresetDef> availablePresets = [];

    public List<AbilityDef> givesAbilities = [];
    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];
    public List<HediffDef> givesHediffs = [];

    public List<RankDef> mustHaveRank = null;
    public List<GeneDef> mustHaveGene = null;
    public List<TraitData> mustHaveTrait = null;
    public List<HediffDef> mustHaveHediff = null;
    public List<ResearchProjectDef> mustHaveResearch = null;

    public List<GeneVariantCost> costs = [];

    public bool HasVisual => !texPath.NullOrEmpty();

    public bool HasStats => !statOffsets.NullOrEmpty() || !statFactors.NullOrEmpty();

    public bool HasCost => !costs.NullOrEmpty();

    public bool AppliesToGene(GeneDef geneDef)
    {
        if (geneDef == null)
        {
            return false;
        }

        if (appliesToAll || appliesTo.Contains(geneDef))
        {
            return true;
        }

        if (appliesToCategories.NullOrEmpty())
        {
            return false;
        }

        var category = geneDef.GetModExtension<DefModExtension_CustomizableGene>()?.category;
        return category != null && appliesToCategories.Contains(category);
    }

    public virtual bool MeetsRequirements(Pawn pawn)
    {
        return RequirementUtility.MeetsRequirements(pawn, mustHaveRank, mustHaveGene, mustHaveTrait, mustHaveHediff, mustHaveResearch);
    }

    public virtual bool HasRequirements(Pawn pawn, out string lockedReason)
    {
        return RequirementUtility.HasRequirements(pawn, mustHaveRank, mustHaveGene, mustHaveTrait, mustHaveHediff, mustHaveResearch, out lockedReason);
    }

    /// <summary>
    /// Whether every cost can currently be paid. The first failing cost supplies the reason.
    /// </summary>
    public virtual bool CanPayCost(Pawn pawn, out string reason)
    {
        reason = null;
        if (costs.NullOrEmpty())
        {
            return true;
        }

        foreach (var cost in costs)
        {
            if (cost != null && !cost.CanAfford(pawn, this, out reason))
            {
                return false;
            }
        }

        return true;
    }

    public virtual void PayCost(Pawn pawn)
    {
        if (costs.NullOrEmpty())
        {
            return;
        }

        foreach (var cost in costs)
        {
            cost?.Pay(pawn, this);
        }
    }

    public virtual string CostDescription(Pawn pawn)
    {
        if (costs.NullOrEmpty())
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var cost in costs)
        {
            var line = cost?.Description(pawn, this);
            if (!line.NullOrEmpty())
            {
                builder.AppendLine(line);
            }
        }

        return builder.Length == 0 ? null : builder.ToString().TrimEndNewlines();
    }

    /// <summary>
    /// Tooltip body for the dialog grid. Stats are shown relative to what the slot would give
    /// without this variant, so a Replace variant that lowers a stat reads as a downgrade.
    /// </summary>
    public virtual string TooltipDescription(Pawn pawn, GeneDef geneDef, GeneVariantDef current)
    {
        var builder = new StringBuilder();
        builder.AppendLine(LabelCap);
        if (!description.NullOrEmpty())
        {
            builder.AppendLine();
            builder.AppendLine(description);
        }

        //The applied variant shows what the slot gives in total; any other shows the change from it.
        var stats = current == this
            ? GeneVariantStatUtility.NetStatLines(geneDef, this)
            : GeneVariantStatUtility.StatDeltaLines(geneDef, current, this);
        if (stats.Count > 0)
        {
            builder.AppendLine();
            foreach (var line in stats)
            {
                builder.AppendLine(line);
            }
        }

        AppendGrants(builder);

        var sets = GeneVariantIndex.SetsFor(this);
        if (sets.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("BEWH.Framework.GeneCustomization.PartOfSets".Translate());
            foreach (var set in sets)
            {
                builder.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(set.LabelCap));
            }
        }

        var cost = CostDescription(pawn);
        if (!cost.NullOrEmpty())
        {
            builder.AppendLine();
            builder.AppendLine("BEWH.Framework.Customization.Cost".Translate());
            builder.AppendLine(cost);
        }

        return builder.ToString().TrimEndNewlines();
    }

    /// <summary>
    /// Ability and hediff grants as tooltip lines, each block preceded by a blank line.
    /// </summary>
    public void AppendGrants(StringBuilder builder)
    {
        if (!givesAbilities.NullOrEmpty() || !givesVFEAbilities.NullOrEmpty())
        {
            builder.AppendLine();
            builder.AppendLine("BEWH.Framework.GeneCustomization.GivesAbilities".Translate());
            foreach (var ability in givesAbilities)
            {
                builder.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(ability.LabelCap));
            }
            foreach (var ability in givesVFEAbilities)
            {
                builder.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(ability.LabelCap));
            }
        }

        if (!givesHediffs.NullOrEmpty())
        {
            builder.AppendLine();
            builder.AppendLine("BEWH.Framework.GeneCustomization.GivesHediffs".Translate());
            foreach (var hediff in givesHediffs)
            {
                builder.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(hediff.LabelCap));
            }
        }
    }

    /// <summary>
    /// What the gene tab shows for a gene while this variant is applied to it: the variant's own
    /// description followed by the slot's total effect.
    /// </summary>
    public string GeneTabDescription(GeneDef geneDef)
    {
        var builder = new StringBuilder();
        builder.AppendLine(description.NullOrEmpty() ? geneDef.description : description);

        var stats = GeneVariantStatUtility.NetStatLines(geneDef, this);
        if (stats.Count > 0)
        {
            builder.AppendLine();
            foreach (var line in stats)
            {
                builder.AppendLine(line);
            }
        }

        AppendGrants(builder);

        return builder.ToString().TrimEndNewlines();
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var configError in base.ConfigErrors())
        {
            yield return configError;
        }

        if (appliesTo.NullOrEmpty() && appliesToCategories.NullOrEmpty() && !appliesToAll)
        {
            yield return "applies to nothing - set appliesTo, appliesToCategories or appliesToAll";
        }

        if (colorAmount is < 1 or > 3)
        {
            yield return "colorAmount must be between 1 and 3";
        }

        if (!HasVisual && colourable)
        {
            yield return "colourable but has no texPath - nothing is drawn, so nothing can be coloured";
        }

        if (statMode == GeneStatMode.Replace)
        {
            foreach (var geneDef in appliesTo)
            {
                if (!geneDef.conditionalStatAffecters.NullOrEmpty())
                {
                    yield return "statMode is Replace but " + geneDef.defName + " has conditionalStatAffecters, which cannot be replaced";
                }
                if (!geneDef.statFactors.NullOrEmpty())
                {
                    foreach (var factor in geneDef.statFactors)
                    {
                        if (Mathf.Approximately(factor.value, 0f))
                        {
                            yield return "statMode is Replace but " + geneDef.defName + " has a zero factor for " + factor.stat?.defName + ", which cannot be cancelled";
                        }
                    }
                }
            }
        }

        if (workerClass != null && !typeof(PawnRenderNodeWorker).IsAssignableFrom(workerClass))
        {
            yield return "workerClass " + workerClass + " does not derive from PawnRenderNodeWorker";
        }

        if (costs.NullOrEmpty())
        {
            yield break;
        }

        foreach (var cost in costs)
        {
            if (cost == null)
            {
                yield return "null cost entry";
                continue;
            }

            foreach (var error in cost.ConfigErrors(this))
            {
                yield return error;
            }
        }
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();
        shaderType ??= Core40kDefOf.BEWH_CutoutThreeColor;
        defaultMask ??= Core40kDefOf.BEWH_DefaultMask;
        parentTagDef ??= PawnRenderNodeTagDefOf.Head;
        drawData ??= new DrawData();
    }
}
