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
    public List<GeneDef> mustHaveGeneOneAmong = null;
    public List<TraitData> mustHaveTrait = null;
    public List<HediffDef> mustHaveHediff = null;
    public List<ResearchProjectDef> mustHaveResearch = null;

    //Extra requirements, checked after the ones built from the mustHave fields above.
    public List<PawnRequirement> requirements = [];
    
    //Offsets and factors on the item's own stats, applied by vanilla through ThingComp.GetStatOffset.
    public List<StatModifier> statOffsets = [];
    public List<StatModifier> statFactors = [];

    //Offsets and factors on the wearer's stats, applied by GetValueUnfinalizedFromVariousFactorPatch.
    public List<StatModifier> pawnStatOffsets = [];
    public List<StatModifier> pawnStatFactors = [];

    public List<string> customEffectDescriptions = [];
    
    public List<AbilityDef> givesAbilities = [];
    public List<VEF.Abilities.AbilityDef> givesVFEAbilities = [];
    
    public List<HediffDef> givesHediffs = [];

    public List<ApparelLayerDef> extraApparelLayers = [];

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
        || !pawnStatOffsets.NullOrEmpty()
        || !pawnStatFactors.NullOrEmpty()
        || !givesAbilities.NullOrEmpty()
        || !givesVFEAbilities.NullOrEmpty()
        || !givesHediffs.NullOrEmpty()
        || !customEffectDescriptions.NullOrEmpty();

    public bool HasCost => !cost.NullOrEmpty();
    
    public bool HasVisual => !isInternal && !drawnTextureIconPath.NullOrEmpty();

    public float RemovalWork => workAmount * removalWorkFactor;

    /// <summary>
    /// Abilities (vanilla, then VFE) and hediffs this decoration gives while the item is equipped.
    /// </summary>
    public IEnumerable<Def> GrantedDefs()
    {
        if (givesAbilities != null)
        {
            foreach (var ability in givesAbilities)
            {
                if (ability != null)
                {
                    yield return ability;
                }
            }
        }
        if (givesVFEAbilities != null)
        {
            foreach (var ability in givesVFEAbilities)
            {
                if (ability != null)
                {
                    yield return ability;
                }
            }
        }
        if (givesHediffs != null)
        {
            foreach (var hediff in givesHediffs)
            {
                if (hediff != null)
                {
                    yield return hediff;
                }
            }
        }
    }

    /// <summary>
    /// Writes what the decoration does into report: item and wearer stats, grants and other effects. Override to add lines of your own (call base).
    /// </summary>
    public virtual void DescribeEffects(EffectReport report)
    {
        report.Stats(statOffsets, statFactors, "BEWH.Framework.Customization.ItemStats".Translate());
        report.Stats(pawnStatOffsets, pawnStatFactors, "BEWH.Framework.Customization.WearerStats".Translate());
        //Under "Grants", like the info card and the fitted report.
        report.Defs("BEWH.Framework.Customization.Grants".Translate(), GrantedDefs());

        if (!customEffectDescriptions.NullOrEmpty())
        {
            foreach (var customEffect in customEffectDescriptions)
            {
                report.Line(EffectReport.OtherEffectsHeader, customEffect?.CapitalizeFirst());
            }
        }
    }

    public virtual string TooltipDescription()
    {
        var stringbuilder = new StringBuilder();
        stringbuilder.AppendLine(label);

        var report = new EffectReport();
        DescribeEffects(report);
        report.AppendTo(stringbuilder);

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

        if (!extraApparelLayers.NullOrEmpty())
        {
            stringbuilder.AppendLine();
            stringbuilder.AppendLine("BEWH.Framework.Customization.ExtraApparelLayers".Translate(ApparelLayerConflictUtility.LayersLabel(extraApparelLayers)));
        }

        var sets = ApparelSetIndex.SetsFor(this);
        if (sets.Count != 0)
        {
            stringbuilder.AppendLine();
            stringbuilder.AppendLine("BEWH.Framework.ApparelSet.PartOfSet".Translate(string.Join(", ", sets.Select(set => set.LabelCap.ToString()))));
        }

        stringbuilder.AppendLine();
        stringbuilder.AppendLine("BEWH.Framework.Customization.WorkAmount".Translate(workAmount.ToString("F0"), RemovalWork.ToString("F0")));

        return stringbuilder.ToString();
    }
    
    public bool ResearchFinished
    {
        get
        {
            if (mustHaveResearch == null)
            {
                return true;
            }
            for (var i = 0; i < mustHaveResearch.Count; i++)
            {
                var project = mustHaveResearch[i];
                if (project != null && !project.IsFinished)
                {
                    return false;
                }
            }
            return true;
        }
    }

    [Unsaved]
    private List<PawnRequirement> allRequirements;

    /// <summary>
    /// Every requirement of this decoration: the ones built from the mustHave fields (and, on armour decorations,
    /// appliesToBodyTypes), then &lt;requirements&gt;.
    /// </summary>
    public List<PawnRequirement> AllRequirements => allRequirements ??= BuildRequirementList();

    private List<PawnRequirement> BuildRequirementList()
    {
        var list = new List<PawnRequirement>();
        RequirementUtility.AddLegacy(list, mustHaveRank, mustHaveGene, mustHaveGeneOneAmong, mustHaveTrait, mustHaveHediff, mustHaveResearch);
        AddBuiltInRequirements(list);
        RequirementUtility.AddListed(list, requirements);
        return list;
    }

    /// <summary>
    /// For subclasses whose own fields turn into requirements; they are checked after the mustHave ones.
    /// </summary>
    protected virtual void AddBuiltInRequirements(List<PawnRequirement> list)
    {
    }

    /// <summary>
    /// The requirements that look at the pawn, without building the reason text. Used to validate fitted
    /// decorations on equip; research is left out as it only gates fitting a decoration.
    /// </summary>
    public virtual bool MeetsPawnRequirements(Pawn pawn)
    {
        return RequirementUtility.AllMet(AllRequirements, new RequirementContext(pawn), pawnOnly: true);
    }

    /// <summary>
    /// The same checks as HasRequirements without building the reason text.
    /// </summary>
    public virtual bool MeetsRequirements(Pawn pawn)
    {
        //Research first, then MeetsPawnRequirements so subclass overrides of it still count.
        RequirementContext context = null;
        foreach (var requirement in AllRequirements)
        {
            if (!requirement.DependsOnPawn && !requirement.IsMet(context ??= new RequirementContext(pawn)))
            {
                return false;
            }
        }

        return MeetsPawnRequirements(pawn);
    }

    /// <summary>
    /// Every requirement, with the text shown in the customization tooltips.
    /// </summary>
    public virtual bool HasRequirements(Pawn pawn, out string lockedReason)
    {
        return RequirementUtility.Report(AllRequirements, new RequirementContext(pawn), out lockedReason);
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (var entry in base.SpecialDisplayStats(req))
        {
            yield return entry;
        }

        var basics = StatCategoryDefOf.Basics;

        var research = mustHaveResearch?.Where(project => project != null).ToList();
        if (!research.NullOrEmpty())
        {
            yield return new StatDrawEntry(basics,
                "BEWH.Framework.Customization.InfoCard.RequiredResearch".Translate(),
                research.Select(project => project.LabelCap.ToString()).ToCommaList(),
                "BEWH.Framework.Customization.InfoCard.RequiredResearchDesc".Translate(),
                4100,
                hyperlinks: research.Select(project => new Dialog_InfoCard.Hyperlink(project)));
        }

        if (appliesToAll)
        {
            yield return new StatDrawEntry(basics,
                "BEWH.Framework.Customization.InfoCard.AppliesTo".Translate(),
                "BEWH.Framework.Customization.InfoCard.AppliesToAll".Translate(),
                "BEWH.Framework.Customization.InfoCard.AppliesToDesc".Translate(),
                4000);
        }
        else
        {
            var things = appliesTo.Select(DefDatabase<ThingDef>.GetNamedSilentFail).Where(thingDef => thingDef != null).ToList();
            if (things.Count > 0)
            {
                var thingList = things.Select(thingDef => thingDef.LabelCap.ToString()).ToCommaList();
                yield return new StatDrawEntry(basics,
                    "BEWH.Framework.Customization.InfoCard.AppliesTo".Translate(),
                    things.Count == 1 ? thingList : "BEWH.Framework.Customization.InfoCard.ItemCount".Translate(things.Count).ToString(),
                    "BEWH.Framework.Customization.InfoCard.AppliesToDesc".Translate() + "\n\n" + thingList,
                    4000,
                    hyperlinks: things.Select(thingDef => new Dialog_InfoCard.Hyperlink(thingDef)));
            }
        }

        if (HasCost && DecorationWorkUtility.CostEnabled)
        {
            var costs = cost.Where(thingCount => thingCount.thingDef != null).ToList();
            yield return new StatDrawEntry(basics,
                "BEWH.Framework.Customization.Cost".Translate(),
                costs.Select(thingCount => thingCount.thingDef.LabelCap.ToString() + " x" + thingCount.count).ToCommaList(),
                "BEWH.Framework.Customization.InfoCard.CostDesc".Translate(),
                3900,
                hyperlinks: costs.Select(thingCount => new Dialog_InfoCard.Hyperlink(thingCount.thingDef)));
        }

        if (DecorationWorkUtility.WorkEnabled)
        {
            yield return new StatDrawEntry(basics,
                "BEWH.Framework.Customization.InfoCard.WorkToFit".Translate(),
                workAmount.ToString("F0"),
                "BEWH.Framework.Customization.InfoCard.WorkToFitDesc".Translate(),
                3800);
            yield return new StatDrawEntry(basics,
                "BEWH.Framework.Customization.InfoCard.WorkToRemove".Translate(),
                RemovalWork.ToString("F0"),
                "BEWH.Framework.Customization.InfoCard.WorkToRemoveDesc".Translate(),
                3790);
        }

        var priority = 3000;
        foreach (var entry in ModifierEntries(statOffsets, basics, false, priority))
        {
            yield return entry;
        }
        foreach (var entry in ModifierEntries(statFactors, basics, true, priority - 100))
        {
            yield return entry;
        }
        foreach (var entry in ModifierEntries(pawnStatOffsets, StatCategoryDefOf.EquippedStatOffsets, false, priority))
        {
            yield return entry;
        }
        foreach (var entry in ModifierEntries(pawnStatFactors, StatCategoryDefOf.EquippedStatOffsets, true, priority - 100))
        {
            yield return entry;
        }

        var grantedDefs = GrantedDefs().ToList();
        if (grantedDefs.Count != 0)
        {
            var grantsDesc = new StringBuilder("BEWH.Framework.Customization.GrantsDesc".Translate().Resolve());
            foreach (var grantedDef in grantedDefs)
            {
                grantsDesc.AppendLine();
                grantsDesc.AppendLine();
                grantsDesc.Append(grantedDef.LabelCap.Resolve());
                if (!grantedDef.description.NullOrEmpty())
                {
                    grantsDesc.AppendLine();
                    grantsDesc.Append(grantedDef.description);
                }
            }
            yield return new StatDrawEntry(StatCategoryDefOf.EquippedStatOffsets,
                "BEWH.Framework.Customization.GrantsLabel".Translate(),
                grantedDefs.Count == 1 ? grantedDefs[0].LabelCap.ToString() : "BEWH.Framework.Customization.GrantsCount".Translate(grantedDefs.Count).ToString(),
                grantsDesc.ToString(),
                priority - 150,
                hyperlinks: grantedDefs.Select(grantedDef => new Dialog_InfoCard.Hyperlink(grantedDef)));
        }

        var customEffects = customEffectDescriptions?.Where(text => !text.NullOrEmpty()).Select(text => text.CapitalizeFirst()).ToList();
        if (!customEffects.NullOrEmpty())
        {
            yield return new StatDrawEntry(basics,
                "BEWH.Framework.Customization.OtherEffects".Translate(),
                customEffects.Count == 1 ? customEffects[0] : "BEWH.Framework.Customization.OtherEffectsCount".Translate(customEffects.Count).ToString(),
                string.Join("\n", customEffects),
                priority - 200);
        }
    }

    private static IEnumerable<StatDrawEntry> ModifierEntries(List<StatModifier> modifiers, StatCategoryDef category, bool factor, int priority)
    {
        if (modifiers.NullOrEmpty())
        {
            yield break;
        }
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier?.stat == null)
            {
                continue;
            }
            yield return new StatDrawEntry(category,
                modifier.stat.LabelCap,
                factor ? modifier.ToStringAsFactor : modifier.ValueToStringAsOffset,
                modifier.stat.description,
                priority - i);
        }
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

        if (extraApparelLayers != null && extraApparelLayers.Contains(null))
        {
            yield return "extraApparelLayers contains a null layer";
        }

        foreach (var error in RequirementUtility.ConfigErrors(this, requirements))
        {
            yield return error;
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