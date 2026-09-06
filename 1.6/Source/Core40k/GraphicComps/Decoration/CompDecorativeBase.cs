using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class CompDecorativeBase : CompGraphicParent
{
    public CompMultiColor MultiColor => parent.GetComp<CompMultiColor>();
    
    public Dictionary<DecorationDef, DecorationDrawData> originalDrawDatas = new(); 
    public Dictionary<DecorationDef, DecorationDrawData> drawDatas = new(); 
    
    protected Dictionary<DecorationDef, DecorationSettings> originalDecorations = new ();
    protected Dictionary<DecorationDef, DecorationSettings> decorations = new ();
    
    public Dictionary<DecorationDef, DecorationSettings> Decorations => decorations ??= new Dictionary<DecorationDef, DecorationSettings>();
    
    private List<DecorationDef> unlockedDecorations = [];

    public bool IsUnlocked(DecorationDef decoration)
    {
        return !decoration.HasCost || unlockedDecorations.Contains(decoration);
    }

    public void Unlock(DecorationDef decoration)
    {
        if (!unlockedDecorations.Contains(decoration))
        {
            unlockedDecorations.Add(decoration);
        }
    }

    public int TotalInternalSlots
    {
        get
        {
            var baseValue = parent.def.statBases.GetStatValueFromList(Core40kDefOf.BEWH_InternalUpgradeSlots, 0f);
            return Mathf.Max(0, Mathf.RoundToInt(baseValue + GetStatOffset(Core40kDefOf.BEWH_InternalUpgradeSlots)));
        }
    }

    public int UsedInternalSlots
    {
        get
        {
            var used = 0;
            foreach (var decoration in Decorations)
            {
                if (decoration.Key is { isInternal: true })
                {
                    used += decoration.Key.slotCost;
                }
            }
            return used;
        }
    }

    public int FreeInternalSlots => TotalInternalSlots - UsedInternalSlots;

    public bool HasRoomFor(DecorationDef decoration)
    {
        return !decoration.isInternal || decoration.slotCost <= FreeInternalSlots;
    }
    
    protected virtual void OnDecorationsChanged()
    {
    }

    //Add
    public virtual void ApplyDecorationsFromList(List<DecorationDef> list, bool free = false)
    {
        foreach (var extraDecoration in list)
        {
            AddOrRemoveDecoration(extraDecoration, free);
        }
    }
    public virtual void AddOrRemoveDecoration(DecorationDef decoration, bool free = false)
    {
        if (decorations.TryGetValue(decoration, out var setting))
        {
            if (decoration.flipable && !setting.Flipped)
            {
                setting.Flipped = true;
            }
            else
            {
                RemoveDecoration(decoration);
            }
        }
        else
        {
            AddDecoration(decoration, setDefaultColors: true, free: free);
        }
        Notify_GraphicChanged();
    }

    public virtual void AddDecoration(DecorationDef decoration, DecorationSettings decorationSettings = null, bool setDefaultColors = false, bool free = false)
    {
        if (!decorations.ContainsKey(decoration))
        {
            decorations.Add(decoration, decorationSettings ?? new DecorationSettings());
            AddAbilitiesOf(decoration);
        }

        if (!drawDatas.ContainsKey(decoration))
        {
            drawDatas.Add(decoration, new DecorationDrawData());
        }

        if (free)
        {
            Unlock(decoration);
        }

        if (setDefaultColors)
        {
            ApplyDefaultColors(decoration, resetMaskDef: true);
        }
    }

    //Remove
    public virtual bool RemoveDecoration(DecorationDef decoration)
    {
        if (!decorations.Remove(decoration))
        {
            return false;
        }

        RemoveAbilitiesOf(decoration);
        drawDatas.Remove(decoration);

        return true;
    }

    public virtual void RemoveAllDecorations()
    {
        foreach (var decoration in decorations.Keys.ToList())
        {
            if (decoration == null || !decoration.showInDecorationTab)
            {
                continue;
            }

            RemoveDecoration(decoration);
        }

        OnDecorationsChanged();
        Notify_GraphicChanged();
    }

    public virtual void RemoveAllDecorations(bool upgrades)
    {
        var toRemove = decorations.Keys.Where(def => def != null && def.showInDecorationTab && def.IsUpgrade == upgrades).ToList();
        foreach (var decoration in toRemove)
        {
            RemoveDecoration(decoration);
        }
        Notify_GraphicChanged();
    }
    public virtual void RemoveInvalidDecorations(Pawn pawn)
    {
        List<DecorationDef> toRemove = null;
        foreach (var def in decorations.Keys)
        {
            if (def == null || !def.MeetsRequirements(pawn))
            {
                toRemove ??= [];
                toRemove.Add(def);
            }
        }
        RemoveEach(toRemove);
    }
    public virtual void RemoveDecorationsIncompatibleWithAlternate(AlternateBaseFormDef alternateBaseFormDef)
    {
        var toRemove = decorations.Keys.Where(def =>
            def == null ||
            (alternateBaseFormDef == null && def.isIncompatibleWithBaseTexture) ||
            (alternateBaseFormDef != null && alternateBaseFormDef.incompatibleDecorations.Contains(def))).ToList();
        RemoveEach(toRemove);
    }

    private void RemoveEach(List<DecorationDef> toRemove)
    {
        if (toRemove.NullOrEmpty())
        {
            return;
        }

        foreach (var decoration in toRemove)
        {
            //Null keys can survive in the dictionary when a content mod is removed; drop them too.
            if (decoration == null)
            {
                decorations.Remove(null);
                drawDatas.Remove(null);
                continue;
            }

            RemoveDecoration(decoration);
        }

        OnDecorationsChanged();
    }
    
    
    //Decos Set
    public virtual void SetDecorationColourOne(DecorationDef decoration, Color colour)
    {
        decorations[decoration].Color = colour;
        Notify_GraphicChanged();
    }
    public virtual void SetDecorationColourTwo(DecorationDef decoration, Color colour)
    {
        decorations[decoration].ColorTwo = colour;
        Notify_GraphicChanged();
    }
    public virtual void SetDecorationColourThree(DecorationDef decoration, Color colour)
    {
        decorations[decoration].ColorThree = colour;
        Notify_GraphicChanged();
    }
    public void SetDecorationMask(DecorationDef decoration, MaskDef maskDef)
    {
        decorations[decoration].maskDef = maskDef;
        Notify_GraphicChanged();
    }
    public void SetDecorationFlipped(DecorationDef decoration, bool flipped)
    {
        if (!decorations.TryGetValue(decoration, out var settings) || settings.Flipped == flipped)
        {
            return;
        }
        settings.Flipped = flipped;
        Notify_GraphicChanged();
    }
    public void SetDecorationToParentColors(DecorationDef decoration)
    {
        decorations[decoration].Color = MultiColor.DrawColor;
        decorations[decoration].ColorTwo = MultiColor.DrawColorTwo;
        decorations[decoration].ColorThree = MultiColor.DrawColorThree;
        Notify_GraphicChanged();
    }
    /// <summary>
    /// Sets all three colours with a single graphic change notification.
    /// </summary>
    public void SetDecorationColours(DecorationDef decoration, Color colour, Color colourTwo, Color colourThree)
    {
        var settings = decorations[decoration];
        settings.Color = colour;
        settings.ColorTwo = colourTwo;
        settings.ColorThree = colourThree;
        Notify_GraphicChanged();
    }
    public virtual void SetDefaultColors(DecorationDef decoration, bool resetMaskDef = true)
    {
        ApplyDefaultColors(decoration, resetMaskDef);
        Notify_GraphicChanged();
    }
    private void ApplyDefaultColors(DecorationDef decoration, bool resetMaskDef)
    {
        var settings = decorations[decoration];
        settings.Color = decoration.defaultColour ?? (decoration.useParentColourAsDefault ? MultiColor.DrawColor : Color.white);
        settings.ColorTwo = decoration.defaultColourTwo ?? (decoration.useParentColourAsDefault ? MultiColor.DrawColorTwo : Color.white);
        settings.ColorThree = decoration.defaultColourThree ?? (decoration.useParentColourAsDefault ? MultiColor.DrawColorThree : Color.white);
        if (resetMaskDef)
        {
            settings.maskDef = decoration.defaultMask;
        }
    }
    public void SetDrawData(DecorationDef decoDef, DecorationDrawData drawData)
    {
        drawDatas[decoDef] = drawData;
        Notify_GraphicChanged();
    }
    
    public virtual void LoadFromPreset(DecorationPreset preset, bool free = false)
    {
        foreach (var presetPart in preset.decorationPresetParts)
        {
            var decoDef = Core40kUtils.GetDecoDefFromString(presetPart.extraDecorationDefs);
            //The preset can still name a decoration whose mod is gone. Skip it rather than
            //keying the dictionary with null.
            if (decoDef == null || !decoDef.showInDecorationTab || decorations.ContainsKey(decoDef))
            {
                continue;
            }
            var extraDecorationsSetting = new DecorationSettings()
            {
                Flipped = presetPart.flipped,
                Color = presetPart.colour,
                ColorTwo = presetPart.colourTwo,
                ColorThree = presetPart.colourThree,
                maskDef = presetPart.maskDef ?? Core40kDefOf.BEWH_DefaultMask,
            };
            
            AddDecoration(decoDef, extraDecorationsSetting, free: free);
        }
    }
    public void LoadFromPreset(DecorationPresetDef preset, bool free = false)
    {
        foreach (var presetPart in preset.presetData)
        {
            if (presetPart.decorationDef == null || decorations.ContainsKey(presetPart.decorationDef))
            {
                continue;
            }
            var multiColComp = parent.GetComp<CompMultiColor>();
            var extraDecorationsSetting = new DecorationSettings()
            {
                Flipped = presetPart.flipped,
                Color = presetPart.colour ?? (presetPart.decorationDef.useParentColourAsDefault ? multiColComp?.DrawColor ?? parent.DrawColor : Color.white),
                ColorTwo = presetPart.colourTwo ?? (presetPart.decorationDef.useParentColourAsDefault ? multiColComp?.DrawColorTwo ?? parent.DrawColorTwo : Color.white),
                ColorThree = presetPart.colourThree ?? (presetPart.decorationDef.useParentColourAsDefault ? multiColComp?.DrawColorThree ?? Color.white : Color.white),
                maskDef = presetPart.maskDef ?? Core40kDefOf.BEWH_DefaultMask,
            };
            
            AddDecoration(presetPart.decorationDef, extraDecorationsSetting, free: free);
        }
    }
    
    //Originals
    public override void SetOriginals()
    {
        SetOriginalDecorations();
        SetOriginalDrawDatas();
        base.SetOriginals();
    }
    public void SetOriginalDrawDatas()
    {   
        originalDrawDatas = new Dictionary<DecorationDef, DecorationDrawData>();
        foreach (var drawData in drawDatas)
        {
            var newDrawData = new DecorationDrawData();
            newDrawData.CopyFrom(drawData.Value);
            originalDrawDatas.Add(drawData.Key, newDrawData);
        }
    }
    public void SetOriginalDrawData(DecorationDef decoDef)
    {   
        originalDrawDatas.Remove(decoDef);
        var drawData = new DecorationDrawData();
        if (drawDatas.TryGetValue(decoDef, out var data))
        {
            drawData.CopyFrom(data);
        }
        originalDrawDatas.Add(decoDef, drawData);
    }
    public void SetOriginalDecorations()
    {
        originalDecorations = new Dictionary<DecorationDef, DecorationSettings>();
        foreach (var decoration in decorations)
        {
            originalDecorations.Add(decoration.Key, new DecorationSettings(decoration.Value));
        }
    }
    
    //Resets
    public override void Reset()
    {
        ResetDecorations();
        ResetDrawDatas();
        
        Notify_GraphicChanged();
        base.Reset();
    }
    public void ResetDrawDatas()
    {
        drawDatas = new Dictionary<DecorationDef, DecorationDrawData>();
        foreach (var drawData in originalDrawDatas)
        {
            var copy = new DecorationDrawData();
            copy.CopyFrom(drawData.Value);
            drawDatas.Add(drawData.Key, copy);
        }
    }
    public void ResetDrawData(DecorationDef decoDef)
    {   
        drawDatas.Remove(decoDef);
        var drawData = new DecorationDrawData();
        if (originalDrawDatas.TryGetValue(decoDef, out var data))
        {
            drawData.CopyFrom(data);
        }
        drawDatas.Add(decoDef, drawData);
        Notify_GraphicChanged();
    }
    public void ResetDrawData(DecorationDef decoDef, Rot4 rot4)
    {
        if (originalDrawDatas.TryGetValue(decoDef, out var data))
        {
            drawDatas[decoDef].GetData(rot4) = data.GetData(rot4).GetCopy();
        }
        else
        {
            drawDatas[decoDef].GetData(rot4) = new DecorationDrawData.RotationalData(rot4);
        }
        Notify_GraphicChanged();
    }
    public void ResetDecorations()
    {
        decorations = new Dictionary<DecorationDef, DecorationSettings>();
        foreach (var decoration in originalDecorations)
        {
            decorations.Add(decoration.Key, new DecorationSettings(decoration.Value));
        }
    }
    
    //Deferred changes
    private Dictionary<DecorationDef, DecorationSettings> pendingDecorations;
    private Dictionary<DecorationDef, DecorationDrawData> pendingDrawDatas;
    private List<DecorationDef> pendingAdded = [];
    private List<DecorationDef> pendingRemoved = [];
    private List<DecorationDef> pendingUnlock = [];
    private bool pendingAppearance;
    private bool hasPendingChange;

    private void ComputeDiff(out List<DecorationDef> added, out List<DecorationDef> removed, out bool appearanceChanged)
    {
        added = [];
        removed = [];
        appearanceChanged = false;

        foreach (var decoration in decorations)
        {
            if (!originalDecorations.ContainsKey(decoration.Key))
            {
                added.Add(decoration.Key);
                continue;
            }

            var original = originalDecorations[decoration.Key];
            var current = decoration.Value;
            if (original.maskDef != current.maskDef
                || original.Color != current.Color
                || original.ColorTwo != current.ColorTwo
                || original.ColorThree != current.ColorThree
                || original.Flipped != current.Flipped)
            {
                appearanceChanged = true;
            }
        }

        foreach (var decoration in originalDecorations)
        {
            if (!decorations.ContainsKey(decoration.Key))
            {
                removed.Add(decoration.Key);
            }
        }
    }

    public override bool HasEdits
    {
        get
        {
            ComputeDiff(out var added, out var removed, out var appearanceChanged);
            return added.Count > 0 || removed.Count > 0 || appearanceChanged;
        }
    }

    public override bool HasAppearanceEdit
    {
        get
        {
            ComputeDiff(out _, out _, out var appearanceChanged);
            return appearanceChanged;
        }
    }

    public override float EditWork
    {
        get
        {
            ComputeDiff(out var added, out var removed, out _);
            return WorkFor(added, removed);
        }
    }

    public override void CollectEditCost(List<ThingDefCountClass> into)
    {
        if (!DecorationWorkUtility.CostEnabled)
        {
            return;
        }

        ComputeDiff(out var added, out _, out _);
        foreach (var decoration in added)
        {
            if (!IsUnlocked(decoration))
            {
                UpgradeCostUtility.AddCost(into, decoration.cost);
            }
        }
    }

    private static float WorkFor(List<DecorationDef> added, List<DecorationDef> removed)
    {
        var work = 0f;
        foreach (var decoration in added)
        {
            work += decoration.workAmount;
        }
        foreach (var decoration in removed)
        {
            work += decoration.RemovalWork;
        }
        return work;
    }

    public override bool HasPendingChange => hasPendingChange;

    public override bool PendingAppearanceChange => pendingAppearance;

    public override float PendingWork => hasPendingChange ? WorkFor(pendingAdded, pendingRemoved) : 0f;

    public override void CollectPendingCost(List<ThingDefCountClass> into)
    {
        if (!hasPendingChange || !DecorationWorkUtility.CostEnabled)
        {
            return;
        }

        foreach (var decoration in pendingUnlock)
        {
            UpgradeCostUtility.AddCost(into, decoration.cost);
        }
    }

    public override void CapturePending()
    {
        ComputeDiff(out var added, out var removed, out var appearanceChanged);
        if (added.Count == 0 && removed.Count == 0 && !appearanceChanged)
        {
            return;
        }

        pendingDecorations = new Dictionary<DecorationDef, DecorationSettings>();
        foreach (var decoration in decorations)
        {
            pendingDecorations.Add(decoration.Key, new DecorationSettings(decoration.Value));
        }

        pendingDrawDatas = new Dictionary<DecorationDef, DecorationDrawData>();
        foreach (var drawData in drawDatas)
        {
            var copy = new DecorationDrawData();
            copy.CopyFrom(drawData.Value);
            pendingDrawDatas.Add(drawData.Key, copy);
        }

        pendingAdded = added;
        pendingRemoved = removed;
        pendingAppearance = appearanceChanged;
        pendingUnlock = added.Where(def => !IsUnlocked(def)).ToList();
        hasPendingChange = true;

        foreach (var decoration in added)
        {
            RemoveAbilitiesOf(decoration);
        }
        foreach (var decoration in removed)
        {
            AddAbilitiesOf(decoration);
        }

        ResetDecorations();
        ResetDrawDatas();
        OnDecorationsChanged();
        Notify_GraphicChanged();
    }

    public override void CommitPending()
    {
        if (!hasPendingChange)
        {
            return;
        }

        foreach (var decoration in pendingAdded)
        {
            AddAbilitiesOf(decoration);
        }
        foreach (var decoration in pendingRemoved)
        {
            RemoveAbilitiesOf(decoration);
        }

        decorations = pendingDecorations;
        drawDatas = pendingDrawDatas;

        foreach (var decoration in pendingUnlock)
        {
            Unlock(decoration);
        }

        ClearPending();
        SetOriginals();
        OnDecorationsChanged();
        Notify_GraphicChanged();
    }

    public override void DiscardPending()
    {
        ClearPending();
    }

    private void ClearPending()
    {
        pendingDecorations = null;
        pendingDrawDatas = null;
        pendingAdded = [];
        pendingRemoved = [];
        pendingUnlock = [];
        pendingAppearance = false;
        hasPendingChange = false;
    }

    private void AddAbilitiesOf(DecorationDef decoration)
    {
        var pawn = Pawn;
        if (pawn == null)
        {
            return;
        }

        pawn.AddAbilities(decoration.givesAbilities, decoration.givesVFEAbilities);
        AddHediffsOf(decoration, pawn);
    }

    private void RemoveAbilitiesOf(DecorationDef decoration)
    {
        var pawn = Pawn;
        if (pawn == null)
        {
            return;
        }

        pawn.RemoveAbilities(decoration.givesAbilities, decoration.givesVFEAbilities);
        RemoveHediffsOf(decoration, pawn);
    }

    private static void AddHediffsOf(DecorationDef decoration, Pawn pawn)
    {
        if (decoration.givesHediffs.NullOrEmpty() || pawn.health == null)
        {
            return;
        }

        foreach (var hediffDef in decoration.givesHediffs)
        {
            pawn.health.AddHediff(hediffDef);
        }
    }

    private static void RemoveHediffsOf(DecorationDef decoration, Pawn pawn)
    {
        if (decoration.givesHediffs.NullOrEmpty() || pawn.health?.hediffSet == null)
        {
            return;
        }

        foreach (var hediffDef in decoration.givesHediffs)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }

    private void ApplyGrantsToPawn(Pawn pawn, bool add)
    {
        if (pawn == null)
        {
            return;
        }

        foreach (var decoration in Decorations)
        {
            if (decoration.Key == null)
            {
                continue;
            }

            if (add)
            {
                pawn.AddAbilities(decoration.Key.givesAbilities, decoration.Key.givesVFEAbilities);
                AddHediffsOf(decoration.Key, pawn);
            }
            else
            {
                pawn.RemoveAbilities(decoration.Key.givesAbilities, decoration.Key.givesVFEAbilities);
                RemoveHediffsOf(decoration.Key, pawn);
            }
        }
    }

    //DrawData for Rot
    public virtual Vector3 GetAdditionalOffsetForRot(Rot4 rot, DecorationDef decorationDef)
    {
        var offset = Vector3.zero;

        if (drawDatas.TryGetValue(decorationDef, out var data))
        {
            offset += data.GetData(rot).offset;
        }

        return offset;
    }
    public virtual float GetAdditionalLayerForRot(Rot4 rot, DecorationDef decorationDef)
    {
        var layer = 0f;
        
        if (drawDatas.TryGetValue(decorationDef, out var data))
        {
            layer += data.GetData(rot).layer;
        }

        return layer;
    }
    public virtual Vector3 GetAdditionalScaleForRot(Rot4 rot, DecorationDef decorationDef)
    {
        var scale = Vector3.one;
        
        if (drawDatas.TryGetValue(decorationDef, out var data))
        {
            scale *= data.GetData(rot).scale;
        }

        return scale;
    }
    
    //Notifi's
    public override void Notify_Equipped(Pawn pawn)
    {
        RemoveInvalidDecorations(pawn);
        
        ApplyGrantsToPawn(pawn, add: true);

        TryAddCachedStat(pawn);
        
        Notify_GraphicChanged();
        base.Notify_Equipped(pawn);
    }
    public override void Notify_Unequipped(Pawn pawn)
    {
        ApplyGrantsToPawn(pawn, add: false);

        if (pawn != null && CoreUtils != null && CoreUtils.cachedDecoratives.TryGetValue(pawn, out var decoratives))
        {
            decoratives.Remove(this);
            if (decoratives.IsEmpty)
            {
                CoreUtils.cachedDecoratives.Remove(pawn);
            }

            cachedStatOffset = new Dictionary<StatDef, float>();
            cachedStatFactor = new Dictionary<StatDef, float>();
        }
        
        base.Notify_Unequipped(pawn);
    }
    private void TryAddCachedStat(Pawn pawn)
    {
        if (pawn == null || CoreUtils == null)
        {
            return;
        }

        cachedStatOffset = new Dictionary<StatDef, float>();
        cachedStatFactor = new Dictionary<StatDef, float>();

        if (!CoreUtils.cachedDecoratives.TryGetValue(pawn, out var decoratives))
        {
            decoratives = new GameComponent_CoreUtils.CachedDecoratives();
            CoreUtils.cachedDecoratives.Add(pawn, decoratives);
        }

        decoratives.Add(this);
    }
    
    //Stat Related
    public override float GetStatOffset(StatDef stat)
    {
        var num = 0f;
        if (CachedStatOffset == null || stat == null)
        {
            return num;
        }
        if (CachedStatOffset.TryGetValue(stat, out var cachedStatOffsetOut))
        {
            num += cachedStatOffsetOut;
        }
        else
        {
            var resNum = 0f;
            foreach (var decoration in Decorations)
            {
                if (decoration.Key?.statOffsets == null)
                {   
                    continue;
                }
                if (!decoration.Key.statOffsets.NullOrEmpty())
                {
                    resNum += decoration.Key.statOffsets.GetStatOffsetFromList(stat);
                }
            }
            CachedStatOffset.Add(stat, resNum);
            num += resNum;
        }
        return num;
    }
    public override float GetStatFactor(StatDef stat)
    {
        var num = 1f;
        if (CachedStatFactor == null || stat == null)
        {
            return num;
        }
        if (CachedStatFactor.TryGetValue(stat, out var cachedStatFactorOut))
        {
            num *= cachedStatFactorOut;
        }
        else
        {
            var resNum = 1f;
                    
            foreach (var decoration in Decorations)
            {
                if (decoration.Key?.statFactors == null)
                {
                    continue;
                }
                if (!decoration.Key.statFactors.NullOrEmpty())
                {
                    resNum *= decoration.Key.statFactors.GetStatFactorFromList(stat);
                }
            }
                    
            CachedStatFactor.Add(stat, resNum);
            num *= resNum;
        }
        
        return num;
    }
    public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
    {
        if (Decorations.NullOrEmpty())
        {
            base.GetStatsExplanation(stat, sb, whitespace);
            return;
        }
        var external = new StringBuilder();
        var internals = new StringBuilder();
        
        foreach (var decoration in Decorations)
        {
            if (decoration.Key == null)
            {
                continue;
            }

            var stringBuilder = decoration.Key.isInternal ? internals : external;
            
            var statOffsetFromList = decoration.Key.statOffsets.GetStatOffsetFromList(stat);
            if (!Mathf.Approximately(statOffsetFromList, 0f))
            {
                stringBuilder.AppendLine(whitespace + "    " + decoration.Key.LabelCap + ": " + stat.Worker.ValueToString(statOffsetFromList, finalized: false, ToStringNumberSense.Offset));
            }
            var statFactorFromList = decoration.Key.statFactors.GetStatFactorFromList(stat);
            if (!Mathf.Approximately(statFactorFromList, 1f))
            {
                stringBuilder.AppendLine(whitespace + "    " + decoration.Key.LabelCap + ": " + stat.Worker.ValueToString(statFactorFromList, finalized: false, ToStringNumberSense.Factor));
            }
        }
        
        if (external.Length != 0)
        {
            sb.AppendLine(whitespace + "BEWH.Framework.StatReport.Decoration".Translate() + ":");
            sb.Append(external);
        }
        
        if (internals.Length != 0)
        {
            sb.AppendLine(whitespace + "BEWH.Framework.StatReport.Internal".Translate() + ":");
            sb.Append(internals);
        }
    }
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
    {
        var decorationEntry = FittedEntry(false, "BEWH.Framework.Customization.FittedDecorations", 90);
        if (decorationEntry != null)
        {
            yield return decorationEntry;
        }

        var upgradeEntry = FittedEntry(true, "BEWH.Framework.Customization.FittedUpgrades", 89);
        if (upgradeEntry != null)
        {
            yield return upgradeEntry;
        }

        if (TotalInternalSlots > 0)
        {
            yield return new StatDrawEntry(
                Core40kDefOf.BEWH_Customization,
                "BEWH.Framework.Customization.InternalSlots".Translate(),
                UsedInternalSlots + " / " + TotalInternalSlots,
                "BEWH.Framework.Customization.InternalSlotsDesc".Translate(),
                88);
        }

        foreach (var pair in GetStatModifiersFromDecorations(false))
        {
            yield return StatContributionEntry(Core40kDefOf.BEWH_DecorationOffsets, pair.Key, pair.Value, false);
        }
        
        foreach (var pair in GetStatModifiersFromDecorations(true))
        {
            yield return StatContributionEntry(Core40kDefOf.BEWH_DecorationFactors, pair.Key, pair.Value, true);
        }
    }

    private StatDrawEntry FittedEntry(bool internalUpgrades, string labelKey, int displayPriority)
    {
        var fitted = new List<DecorationDef>();
        foreach (var decoration in Decorations)
        {
            if (decoration.Key != null && decoration.Key.isInternal == internalUpgrades)
            {
                fitted.Add(decoration.Key);
            }
        }

        if (fitted.Count == 0)
        {
            return null;
        }

        fitted.SortBy(def => def.label);

        var report = new StringBuilder();
        foreach (var decoration in fitted)
        {
            report.AppendLine(decoration.LabelCap);

            foreach (var statOffset in decoration.statOffsets)
            {
                report.AppendLine("    " + statOffset.stat.LabelCap + ": " + statOffset.ValueToStringAsOffset);
            }
            foreach (var statFactor in decoration.statFactors)
            {
                report.AppendLine("    " + statFactor.stat.LabelCap + ": x" + statFactor.ValueToStringAsOffset);
            }
            foreach (var hediff in decoration.givesHediffs)
            {
                report.AppendLine("    " + hediff.LabelCap);
            }

            report.AppendLine();
        }

        return new StatDrawEntry(
            Core40kDefOf.BEWH_Customization,
            labelKey.Translate(),
            fitted.Count.ToString(),
            report.ToString().TrimEndNewlines(),
            displayPriority);
    }
    
    private Dictionary<StatDef, List<StatContribution>> GetStatModifiersFromDecorations(bool factors)
    {
        var dict = new Dictionary<StatDef, List<StatContribution>>();
        foreach (var decoration in decorations)
        {
            if (decoration.Key == null)
            {
                continue;
            }

            var statModifiers = factors ? decoration.Key.statFactors : decoration.Key.statOffsets;
            if (statModifiers.NullOrEmpty())
            {
                continue;
            }
            
            var groupKey = decoration.Key.isInternal
                ? "BEWH.Framework.StatReport.Internal"
                : "BEWH.Framework.StatReport.Decoration";
            var groupOrder = decoration.Key.isInternal ? 1 : 0;

            foreach (var statModifier in statModifiers)
            {
                var contribution = new StatContribution(decoration.Key.LabelCap, statModifier, groupKey, groupOrder);
                if (dict.TryGetValue(statModifier.stat, out var contributions))
                {
                    contributions.Add(contribution);
                }
                else
                {
                    dict.Add(statModifier.stat, [contribution]);
                }
            }
        }

        return dict;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        
        Scribe_Collections.Look(ref decorations, "decorations");

        Scribe_Collections.Look(ref drawDatas, "drawData");

        Scribe_Collections.Look(ref unlockedDecorations, "unlockedDecorations", LookMode.Def);

        Scribe_Values.Look(ref hasPendingChange, "hasPendingChange");
        Scribe_Values.Look(ref pendingAppearance, "pendingAppearance");
        Scribe_Collections.Look(ref pendingDecorations, "pendingDecorations");
        Scribe_Collections.Look(ref pendingDrawDatas, "pendingDrawDatas");
        Scribe_Collections.Look(ref pendingAdded, "pendingAdded", LookMode.Def);
        Scribe_Collections.Look(ref pendingRemoved, "pendingRemoved", LookMode.Def);
        Scribe_Collections.Look(ref pendingUnlock, "pendingUnlock", LookMode.Def);

        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }

        decorations ??= new Dictionary<DecorationDef, DecorationSettings>();
        drawDatas ??= new Dictionary<DecorationDef, DecorationDrawData>();
        unlockedDecorations ??= [];
        pendingAdded ??= [];
        pendingRemoved ??= [];
        pendingUnlock ??= [];

        foreach (var decoration in decorations)
        {
            if (decoration.Key != null)
            {
                Unlock(decoration.Key);
            }
        }

        SetOriginalDecorations();
        SetOriginalDrawDatas();

        if (hasPendingChange && (pendingDecorations == null || pendingDrawDatas == null))
        {
            ClearPending();
        }

        TryAddCachedStat(Pawn);
    }
}