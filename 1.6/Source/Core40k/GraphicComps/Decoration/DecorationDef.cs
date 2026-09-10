using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class DecorationDef : Def
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
    
    [NoTranslate]
    public string drawnTextureIconPath;
        
    public float sortOrder = 0f;
    
    public List<string> appliesTo = [];
    public bool appliesToAll = false;
    
    public DrawData drawData = new();
    public ShaderTypeDef shaderType;
    public Vector2 drawSize = Vector2.one;
    
    public bool colourable = false;
    public int colorAmount = 1;
        
    public Color? defaultColour;
    public Color? defaultColourTwo;
    public Color? defaultColourThree;
    
    public bool useParentColourAsDefault = false;
    public bool hasParentColourPaletteOption = false;
    
    public bool flipable = false;

    //Placed and removed by a tab of its own, such as a mod's shoulder icon tab. The Decoration and
    //Upgrades tabs neither list it nor touch it with Remove all or presets.
    public bool showInDecorationTab = true;
    
    [Obsolete]
    public bool useMask = false;
    public MaskDef defaultMask;
    
    public DecorationTypeDef decorationType;
    
    public List<DecorationColourPresetDef> availablePresets = [];
    
    public bool isIncompatibleWithBaseTexture = false;
    public List<DecorationDef> incompatibleDecorations = [];
    
    public List<RankDef> mustHaveRank = null;
    public List<GeneDef> mustHaveGene = null;
    public List<TraitData> mustHaveTrait = null;
    public List<HediffDef> mustHaveHediff = null;
    public List<ResearchProjectDef> mustHaveResearch = null;
    
    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];
    
    public List<AbilityDef> givesAbilities = [];
    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];
    
    public List<HediffDef> givesHediffs = [];

    public bool isInternal = false;
    
    public int slotCost = 1;

    public List<ThingDefCountClass> cost = [];
    
    public float workAmount = 100f;
    
    public float removalWorkFactor = 0.5f;
    
    public bool? isUpgrade = null;

    [Unsaved]
    private bool? isUpgradeCached;

    public bool IsUpgrade => isUpgradeCached ??= isUpgrade ?? AutoDetectUpgrade;

    protected virtual bool AutoDetectUpgrade =>
        isInternal
        || !statOffsets.NullOrEmpty()
        || !statFactors.NullOrEmpty()
        || !givesAbilities.NullOrEmpty()
        || !givesVFEAbilities.NullOrEmpty()
        || !givesHediffs.NullOrEmpty();

    public bool HasCost => !cost.NullOrEmpty();
    
    public bool HasVisual => !isInternal && !drawnTextureIconPath.NullOrEmpty();

    public float RemovalWork => workAmount * removalWorkFactor;

    public virtual string TooltipDescription()
    {
        var stringbuilder = new StringBuilder();
        stringbuilder.AppendLine(label);

        if (!statOffsets.NullOrEmpty())
        {
            stringbuilder.AppendLine();
            stringbuilder.AppendLine("BEWH.Framework.CommonKeyword.StatOffset".Translate());
            foreach (var statOffset in statOffsets)
            {
                stringbuilder.AppendLine(statOffset.stat.label.CapitalizeFirst() + ": " + statOffset.ValueToStringAsOffset);
            }
        }
        
        if (!statFactors.NullOrEmpty())
        {
            stringbuilder.AppendLine();
            stringbuilder.AppendLine("BEWH.Framework.CommonKeyword.StatFactor".Translate());
            foreach (var statFactor in statFactors)
            {
                stringbuilder.AppendLine(statFactor.stat.label.CapitalizeFirst() + ": x" + statFactor.ValueToStringAsOffset);
            }
        }

        if (HasCost && DecorationWorkUtility.CostEnabled)
        {
            stringbuilder.AppendLine();
            stringbuilder.AppendLine("BEWH.Framework.Customization.Cost".Translate());
            foreach (var thingCount in cost)
            {
                stringbuilder.AppendLine("BEWH.Framework.Customization.CostLine".Translate(thingCount.thingDef.LabelCap, thingCount.count));
            }
        }

        if (isInternal && slotCost > 0)
        {
            stringbuilder.AppendLine();
            stringbuilder.AppendLine("BEWH.Framework.Customization.SlotCost".Translate(slotCost));
        }

        stringbuilder.AppendLine();
        stringbuilder.AppendLine("BEWH.Framework.Customization.WorkAmount".Translate(workAmount.ToString("F0"), RemovalWork.ToString("F0")));

        return stringbuilder.ToString();
    }
    
    /// <summary>
    /// The same checks as HasRequirements without building the reason text. Use this wherever the
    /// reason is discarded, such as validating fitted decorations on equip.
    /// </summary>
    public virtual bool MeetsRequirements(Pawn pawn)
    {
        return RequirementUtility.MeetsRequirements(pawn, mustHaveRank, mustHaveGene, mustHaveTrait, mustHaveHediff, mustHaveResearch);
    }

    public virtual bool HasRequirements(Pawn pawn, out string lockedReason)
    {
        return RequirementUtility.HasRequirements(pawn, mustHaveRank, mustHaveGene, mustHaveTrait, mustHaveHediff, mustHaveResearch, out lockedReason);
    }
    
    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var configError in base.ConfigErrors())
        {
            yield return configError;
        }

        if (workAmount < 0f)
        {
            yield return "workAmount is negative";
        }

        if (removalWorkFactor < 0f)
        {
            yield return "removalWorkFactor is negative";
        }

        if (isInternal)
        {
            if (!drawnTextureIconPath.NullOrEmpty())
            {
                yield return "isInternal but has a drawnTextureIconPath - internal upgrades are never drawn";
            }
            if (isIncompatibleWithBaseTexture)
            {
                yield return "isInternal but isIncompatibleWithBaseTexture - nothing is drawn, so it cannot clash with a base texture";
            }
            if (slotCost < 0)
            {
                yield return "slotCost is negative";
            }
        }
        else if (drawnTextureIconPath.NullOrEmpty() && this is not AlternateBaseFormDef)
        {
            yield return "no drawnTextureIconPath and not marked isInternal - nothing will be drawn for this decoration";
        }

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

    public override void ResolveReferences()
    {
        shaderType ??= Core40kDefOf.BEWH_CutoutThreeColor;
        defaultMask ??= Core40kDefOf.BEWH_DefaultMask;

        if (isInternal)
        {
            colourable = false;
            flipable = false;
            decorationType ??= Core40kDefOf.BEWH_DecoCategory_Internal;
        }

        decorationType ??= Core40kDefOf.BEWH_UndefinedType;
        if (useMask)
        {
            Log.Warning(defName + "has useMask set, this field is no longer needed and should be removed.");
        }
        base.ResolveReferences();
    }
}