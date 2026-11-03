using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

public class CompGraphicParent : ThingComp, IPawnStatSource
{
    private static Game cachedGameForCoreUtils;
    private static GameComponent_CoreUtils coreUtils;

    protected static GameComponent_CoreUtils CoreUtils => CoreUtilsFor();

    private static GameComponent_CoreUtils CoreUtilsFor()
    {
        if (coreUtils != null && cachedGameForCoreUtils == Current.Game)
        {
            return coreUtils;
        }

        cachedGameForCoreUtils = Current.Game;
        coreUtils = cachedGameForCoreUtils?.GetComponent<GameComponent_CoreUtils>();

        return coreUtils;
    }
    
    protected Dictionary<StatDef, float> cachedStatOffset = new();
    public Dictionary<StatDef, float> CachedStatOffset => cachedStatOffset ??= new Dictionary<StatDef, float>();
    protected Dictionary<StatDef, float> cachedStatFactor = new();
    public Dictionary<StatDef, float> CachedStatFactor => cachedStatFactor ??= new Dictionary<StatDef, float>();
    
    protected Dictionary<StatDef, float> cachedPawnStatOffset = new();
    public Dictionary<StatDef, float> CachedPawnStatOffset => cachedPawnStatOffset ??= new Dictionary<StatDef, float>();
    protected Dictionary<StatDef, float> cachedPawnStatFactor = new();
    public Dictionary<StatDef, float> CachedPawnStatFactor => cachedPawnStatFactor ??= new Dictionary<StatDef, float>();

    /// <summary>
    /// Stat offset this comp gives the pawn wearing or holding the parent. ThingComp.GetStatOffset
    /// only reaches the parent item's own stats, so wearer-level offsets come through here instead.
    /// Applied by GetValueUnfinalizedFromVariousFactorPatch.
    /// </summary>
    public virtual float GetPawnStatOffset(StatDef stat)
    {
        return 0f;
    }

    /// <summary>
    /// Stat factor this comp gives the pawn wearing or holding the parent. See GetPawnStatOffset.
    /// </summary>
    public virtual float GetPawnStatFactor(StatDef stat)
    {
        return 1f;
    }
    
    protected ThingDef ThingDef => parent.def;
    protected Thing Thing => parent;
    
    protected bool IsApparel => parent is Apparel;

    protected Pawn Wearer => ParentHolder is not Pawn_ApparelTracker pawn_ApparelTracker ? null : pawn_ApparelTracker.pawn;
    protected Pawn Holder => ParentHolder is not Pawn_EquipmentTracker pawn_EquipmentTracker ? null : pawn_EquipmentTracker.pawn;

    protected Pawn Pawn => Wearer ?? Holder;

    private bool initialSet;

    public bool InitialSet
    {
        get => initialSet;
        set => initialSet = value;
    }
    
    public virtual void Notify_GraphicChanged()
    {
        cachedStatOffset = new Dictionary<StatDef, float>();
        cachedStatFactor = new Dictionary<StatDef, float>();
        cachedPawnStatOffset = new Dictionary<StatDef, float>();
        cachedPawnStatFactor = new Dictionary<StatDef, float>();
        parent.GetComp<Comp_ConversionField>()?.Notify_FieldSourceChanged();
        parent.Notify_ColorChanged();
        OutfitStandDecorationRenderer.Notify_ItemGraphicChanged(parent);
    }
    
    public readonly struct StatContribution
    {
        public readonly string label;
        public readonly StatModifier modifier;
        public readonly string groupKey;
        public readonly int groupOrder;

        public StatContribution(string label, StatModifier modifier, string groupKey, int groupOrder = 0)
        {
            this.label = label;
            this.modifier = modifier;
            this.groupKey = groupKey;
            this.groupOrder = groupOrder;
        }
    }

    protected static StatDrawEntry StatContributionEntry(StatCategoryDef category, StatDef stat, List<StatContribution> contributions, bool isFactor)
    {
        var numberSense = isFactor ? ToStringNumberSense.Factor : ToStringNumberSense.Offset;
        var total = isFactor ? 1f : 0f;
        
        var groupKeys = new List<string>();
        var groupOrders = new Dictionary<string, int>();
        var groups = new Dictionary<string, List<StatContribution>>();

        foreach (var contribution in contributions)
        {
            if (isFactor)
            {
                total *= contribution.modifier.value;
            }
            else
            {
                total += contribution.modifier.value;
            }

            if (!groups.TryGetValue(contribution.groupKey, out var group))
            {
                group = [];
                groups.Add(contribution.groupKey, group);
                groupOrders.Add(contribution.groupKey, contribution.groupOrder);
                groupKeys.Add(contribution.groupKey);
            }

            group.Add(contribution);
        }

        groupKeys.Sort((first, second) => groupOrders[first].CompareTo(groupOrders[second]));

        var report = new StringBuilder();
        if (!stat.description.NullOrEmpty())
        {
            report.AppendLine(stat.description);
            report.AppendLine();
        }

        foreach (var groupKey in groupKeys)
        {
            report.AppendLine(groupKey.Translate() + ":");
            foreach (var contribution in groups[groupKey])
            {
                report.AppendLine("    " + contribution.label + ": " + stat.Worker.ValueToString(contribution.modifier.value, false, numberSense));
            }
        }

        var totalString = stat.Worker.ValueToString(total, false, numberSense);

        if (contributions.Count > 1)
        {
            report.AppendLine();
            report.AppendLine("BEWH.Framework.StatReport.Total".Translate() + ": " + totalString);
        }

        return new StatDrawEntry(
            category,
            stat.LabelCap,
            totalString,
            report.ToString().TrimEndNewlines(),
            stat.displayPriorityInCategory);
    }

    public virtual void SetOriginals()
    {
    }

    public virtual void Reset()
    {
    }

    public virtual bool HasEdits => false;

    //Live colour / mask / alternate form differs. Drives the flat per item appearance charge.
    public virtual bool HasAppearanceEdit => false;

    //Structural work (things added and removed) for the live edits. Excludes the appearance charge.
    public virtual float EditWork => 0f;

    public virtual void CollectEditCost(List<ThingDefCountClass> into)
    {
    }

    public virtual bool HasPendingChange => false;

    public virtual bool PendingAppearanceChange => false;

    public virtual float PendingWork => 0f;

    public virtual void CollectPendingCost(List<ThingDefCountClass> into)
    {
    }

    //Snapshot the live state and roll back to the committed state.
    public virtual void CapturePending()
    {
    }

    //Apply the snapshot for real.
    public virtual void CommitPending()
    {
    }

    //Throw the snapshot away, leaving the committed state untouched.
    public virtual void DiscardPending()
    {
    }
    
    public virtual void InitialSetup()
    {
        SetOriginals();
        initialSet = true;
    }
    
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (initialSet)
        {
            return;
        }
        InitialSetup();
    }
    
    public override void PostExposeData()
    {
        Scribe_Values.Look(ref initialSet, "initialColourSet");
        base.PostExposeData();
    }
}