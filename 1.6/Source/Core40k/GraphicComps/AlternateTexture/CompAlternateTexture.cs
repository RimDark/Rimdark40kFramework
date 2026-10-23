using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class CompAlternateTexture : CompGraphicParent
{
    private CompMultiColor MultiColor => parent?.GetComp<CompMultiColor>();
    
    private AlternateBaseFormDef originalCurrentAlternateBaseForm = null;
    private AlternateBaseFormDef currentAlternateBaseForm = null;
    public AlternateBaseFormDef CurrentAlternateBaseForm => currentAlternateBaseForm;

    public void SetAlternateBaseForm(AlternateBaseFormDef alternateBaseFormDef, bool isForApparel)
    {
        if (isForApparel)
        {
            var compArmorDeco = Thing?.TryGetComp<CompDecorative>();
            compArmorDeco?.RemoveDecorationsIncompatibleWithAlternate(alternateBaseFormDef);
        }
        else
        {
            var compWeaponDeco = Thing?.TryGetComp<CompWeaponDecoration>();
            compWeaponDeco?.RemoveDecorationsIncompatibleWithAlternate(alternateBaseFormDef);
        }
        
        if (MultiColor != null)
        {
            if (alternateBaseFormDef != null)
            {
                MultiColor?.ResetFieldsByAlternateTexture(alternateBaseFormDef);
            }
            else
            {
                MultiColor?.SetDefaultColors(); 
            }
        }

        SwapCurrentAlternateBaseForm(alternateBaseFormDef);
        
        Notify_GraphicChanged();
    }

    /// <summary>
    /// Changes the current alternate form, moving the abilities and hediffs it grants from the old form to the new one.
    /// </summary>
    private void SwapCurrentAlternateBaseForm(AlternateBaseFormDef alternateBaseFormDef)
    {
        if (currentAlternateBaseForm == alternateBaseFormDef)
        {
            return;
        }

        var pawn = Pawn;
        RemoveGrants(pawn, currentAlternateBaseForm);
        currentAlternateBaseForm = alternateBaseFormDef;
        AddGrants(pawn, currentAlternateBaseForm);
    }

    /// <summary>
    /// Gives the pawn the abilities and hediffs of an alternate form.
    /// </summary>
    private static void AddGrants(Pawn pawn, AlternateBaseFormDef alternateBaseFormDef)
    {
        if (pawn == null || alternateBaseFormDef == null)
        {
            return;
        }

        pawn.AddAbilities(alternateBaseFormDef.givesAbilities, alternateBaseFormDef.givesVFEAbilities);

        if (alternateBaseFormDef.givesHediffs.NullOrEmpty() || pawn.health == null)
        {
            return;
        }

        foreach (var hediffDef in alternateBaseFormDef.givesHediffs)
        {
            pawn.health.AddHediff(hediffDef);
        }
    }

    /// <summary>
    /// Takes away the abilities and hediffs an alternate form gave the pawn.
    /// </summary>
    private static void RemoveGrants(Pawn pawn, AlternateBaseFormDef alternateBaseFormDef)
    {
        if (pawn == null || alternateBaseFormDef == null)
        {
            return;
        }

        pawn.RemoveAbilities(alternateBaseFormDef.givesAbilities, alternateBaseFormDef.givesVFEAbilities);

        if (alternateBaseFormDef.givesHediffs.NullOrEmpty() || pawn.health?.hediffSet == null)
        {
            return;
        }

        foreach (var hediffDef in alternateBaseFormDef.givesHediffs)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
    
    
    private bool recacheMultiGraphics = true;
    public bool RecacheMultiGraphics => recacheMultiGraphics;
    private Graphic_Multi cachedGraphicMulti;
    public Graphic_Multi CachedGraphicMulti
    {
        get => cachedGraphicMulti;
        set
        {
            cachedGraphicMulti = value;
            recacheMultiGraphics = false;
            if (IsApparel)
            {
                apparelGraphicRecord = new ApparelGraphicRecord(cachedGraphicMulti, parent as Apparel);
            }
        }
    }
    private ApparelGraphicRecord? apparelGraphicRecord;
    public ApparelGraphicRecord ApparelGraphicRecord
    {
        get
        {
            if (!IsApparel)
            {
                return new ApparelGraphicRecord(null, null);
            }
            apparelGraphicRecord ??= new ApparelGraphicRecord(CachedGraphicMulti, parent as Apparel);
            return apparelGraphicRecord.Value;
        }
    }
    
    private bool recacheSingleGraphics = true;
    public bool RecacheSingleGraphics => recacheSingleGraphics;
    private Graphic cachedGraphic;
    private Graphic cachedDefaultGraphic;
    public Graphic GetSingleGraphic(bool onlyDefaultGraphic = false)
    {
        if (onlyDefaultGraphic)
        {
            if (cachedDefaultGraphic != null)
            {
                return cachedDefaultGraphic;
            }
        }
        else
        {
            if (cachedGraphic != null)
            {
                return cachedGraphic;
            }
        }
        
        SetSingleGraphic(onlyDefaultGraphic);
        return GetSingleGraphic(onlyDefaultGraphic);
    }
    public void SetSingleGraphic(bool onlyDefaultGraphic = false)
    {
        recacheSingleGraphics = false;
        var path = onlyDefaultGraphic ? ThingDef.graphicData.texPath : CurrentAlternateBaseForm?.drawnTextureIconPath ?? ThingDef.graphicData.texPath;
        var drawSize = CurrentAlternateBaseForm?.newDrawSize ?? ThingDef.graphicData.drawSize;
        var shader = Core40kDefOf.BEWH_CutoutThreeColor.Shader;
        var drawMult = IsApparel ? 0.9f : 1f;
        var graphic = MultiColorUtils.GetGraphic<Graphic_Single>(path, shader, drawSize*drawMult, MultiColor?.DrawColor ?? parent.DrawColor, MultiColor?.DrawColorTwo ?? parent.DrawColorTwo, MultiColor?.DrawColorThree ?? parent.DrawColorTwo, null, MultiColor?.MaskDef?.maskPath);
        if (onlyDefaultGraphic)
        {
            cachedDefaultGraphic = new Graphic_RandomRotated(graphic, 35f);
        }
        else
        {
            cachedGraphic = new Graphic_RandomRotated(graphic, 35f);
        }
    }
    
    
    public override void InitialSetup()
    {
        base.InitialSetup();
        recacheMultiGraphics = true;
    }
    
    public override void Notify_GraphicChanged()
    {
        recacheMultiGraphics = true;
        recacheSingleGraphics = true;
        base.Notify_GraphicChanged();
    }
    
    public override void Notify_Equipped(Pawn pawn)
    {
        AddGrants(pawn, currentAlternateBaseForm);
        TryAddCachedStat(pawn);

        Notify_GraphicChanged();
        base.Notify_Equipped(pawn);
    }
    
    public override void Notify_Unequipped(Pawn pawn)
    {
        RemoveGrants(pawn, currentAlternateBaseForm);

        if (pawn != null && CoreUtils != null && CoreUtils.cachedAlternateTexture.TryGetValue(pawn, out var alternateTexture))
        {
            alternateTexture.Remove(this);
            if (alternateTexture.IsEmpty)
            {
                CoreUtils.cachedAlternateTexture.Remove(pawn);
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

        if (!CoreUtils.cachedAlternateTexture.TryGetValue(pawn, out var alternateTexture))
        {
            alternateTexture = new GameComponent_CoreUtils.CachedDecoratives();
            CoreUtils.cachedAlternateTexture.Add(pawn, alternateTexture);
        }

        alternateTexture.Add(this);
    }
    
    public override void SetOriginals()
    {
        originalCurrentAlternateBaseForm = currentAlternateBaseForm;
        Notify_GraphicChanged();
    }
    public override void Reset()
    {
        SwapCurrentAlternateBaseForm(originalCurrentAlternateBaseForm);
        Notify_GraphicChanged();
    }
    
    private AlternateBaseFormDef pendingAlternateBaseForm;
    private bool hasPendingChange;

    public override bool HasEdits => HasAppearanceEdit;

    public override bool HasAppearanceEdit => currentAlternateBaseForm != originalCurrentAlternateBaseForm;

    public override bool HasPendingChange => hasPendingChange;

    public override bool PendingAppearanceChange => hasPendingChange;

    public override void CapturePending()
    {
        if (!HasAppearanceEdit)
        {
            return;
        }

        pendingAlternateBaseForm = currentAlternateBaseForm;
        hasPendingChange = true;

        Reset();
    }

    public override void CommitPending()
    {
        if (!hasPendingChange)
        {
            return;
        }

        SwapCurrentAlternateBaseForm(pendingAlternateBaseForm);
        pendingAlternateBaseForm = null;
        hasPendingChange = false;

        SetOriginals();
        Notify_GraphicChanged();
    }

    public override void DiscardPending()
    {
        pendingAlternateBaseForm = null;
        hasPendingChange = false;
    }
    
    //Stat Related
    public override float GetStatOffset(StatDef stat)
    {
        var num = 0f;

        if (CurrentAlternateBaseForm == null)
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
            
            resNum += CurrentAlternateBaseForm.statOffsets.GetStatOffsetFromList(stat);

            CachedStatOffset.Add(stat, resNum);
            num += resNum;
        }
        
        return num;
    }
    public override float GetPawnStatOffset(StatDef stat)
    {
        if (CurrentAlternateBaseForm == null || stat == null)
        {
            return 0f;
        }

        if (CachedPawnStatOffset.TryGetValue(stat, out var cached))
        {
            return cached;
        }

        var resNum = CurrentAlternateBaseForm.pawnStatOffsets.GetStatOffsetFromList(stat);
        CachedPawnStatOffset.Add(stat, resNum);
        return resNum;
    }

    public override float GetPawnStatFactor(StatDef stat)
    {
        if (CurrentAlternateBaseForm == null || stat == null)
        {
            return 1f;
        }

        if (CachedPawnStatFactor.TryGetValue(stat, out var cached))
        {
            return cached;
        }

        var resNum = CurrentAlternateBaseForm.pawnStatFactors.GetStatFactorFromList(stat);
        CachedPawnStatFactor.Add(stat, resNum);
        return resNum;
    }

    public override float GetStatFactor(StatDef stat)
    {
        var num = 1f;
        
        if (CurrentAlternateBaseForm == null)
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
                    
            resNum *= CurrentAlternateBaseForm.statFactors.GetStatFactorFromList(stat);
                    
            CachedStatFactor.Add(stat, resNum);
            num *= resNum;
        }
        
        return num;
    }
    public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
    {
        if (CurrentAlternateBaseForm == null)
        {
            base.GetStatsExplanation(stat, sb, whitespace);
            return;
        }
        var stringBuilder = new StringBuilder();
        
        var statOffsetFromList = CurrentAlternateBaseForm.statOffsets.GetStatOffsetFromList(stat);
        if (!Mathf.Approximately(statOffsetFromList, 0f))
        {
            stringBuilder.AppendLine(whitespace + "    " + CurrentAlternateBaseForm.LabelCap + ": " + stat.Worker.ValueToString(statOffsetFromList, finalized: false, ToStringNumberSense.Offset));
        }
        var statFactorFromList = CurrentAlternateBaseForm.statFactors.GetStatFactorFromList(stat);
        if (!Mathf.Approximately(statFactorFromList, 1f))
        {
            stringBuilder.AppendLine(whitespace + "    " + CurrentAlternateBaseForm.LabelCap + ": " + stat.Worker.ValueToString(statFactorFromList, finalized: false, ToStringNumberSense.Factor));
        }
        
        if (stringBuilder.Length != 0)
        {
            sb.AppendLine(whitespace + "BEWH.Framework.StatReport.AlternateTexture".Translate() + ":");
            sb.Append(stringBuilder);
        }
    }
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
    {
        if (CurrentAlternateBaseForm == null)
        {
            yield break;
        }
        
        foreach (var pair in GetStatModifiersFromAlternateForm(false))
        {
            yield return StatContributionEntry(Core40kDefOf.BEWH_AlternateTextureOffsets, pair.Key, pair.Value, false);
        }
        
        foreach (var pair in GetStatModifiersFromAlternateForm(true))
        {
            yield return StatContributionEntry(Core40kDefOf.BEWH_AlternateTextureFactors, pair.Key, pair.Value, true);
        }
    }

    private Dictionary<StatDef, List<StatContribution>> GetStatModifiersFromAlternateForm(bool factors)
    {
        var dict = new Dictionary<StatDef, List<StatContribution>>();
        var statModifiers = factors ? CurrentAlternateBaseForm.statFactors : CurrentAlternateBaseForm.statOffsets;
        if (statModifiers.NullOrEmpty())
        {
            return dict;
        }

        foreach (var statModifier in statModifiers)
        {
            var contribution = new StatContribution(CurrentAlternateBaseForm.LabelCap, statModifier, "BEWH.Framework.StatReport.AlternateTexture");
            if (dict.TryGetValue(statModifier.stat, out var contributions))
            {
                contributions.Add(contribution);
            }
            else
            {
                dict.Add(statModifier.stat, [contribution]);
            }
        }

        return dict;
    }
    
    public override void PostExposeData()
    {
        Scribe_Defs.Look(ref originalCurrentAlternateBaseForm, "originalCurrentAlternateBaseForm");
        Scribe_Defs.Look(ref currentAlternateBaseForm, "currentAlternateBaseForm");
        Scribe_Values.Look(ref hasPendingChange, "hasPendingChange");
        Scribe_Defs.Look(ref pendingAlternateBaseForm, "pendingAlternateBaseForm");
        
        base.PostExposeData();
        
        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }
        
        TryAddCachedStat(Pawn);
        Notify_GraphicChanged();
    }
}