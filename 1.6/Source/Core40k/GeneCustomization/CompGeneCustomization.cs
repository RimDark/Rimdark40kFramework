using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using VEF.Abilities;
using Verse;
using AbilityDef = RimWorld.AbilityDef;

namespace Core40k;

public class CompGeneCustomization : ThingComp
{
    private List<GeneVariantChoice> choices = [];

    private HashSet<GeneVariantDef> announcedVariants = [];

    [Unsaved]
    private Dictionary<GeneDef, GeneVariantChoice> byGene = new();

    [Unsaved]
    private List<GeneVariantChoice> activeChoices = [];

    [Unsaved]
    private List<GeneVariantSetDef> activeSets = [];

    [Unsaved]
    private List<GeneSetTier> activeTiers = [];

    [Unsaved]
    private HashSet<GeneVariantDef> activeVariants = [];

    [Unsaved]
    private bool anyHidesBaseGraphic;

    [Unsaved]
    private bool dirty = true;

    [Unsaved]
    private readonly Dictionary<StatDef, float> cachedStatOffset = new();

    [Unsaved]
    private readonly Dictionary<StatDef, float> cachedStatFactor = new();

    public CompProperties_GeneCustomization Props => (CompProperties_GeneCustomization)props;

    public Pawn Pawn => parent as Pawn;

    public List<GeneVariantChoice> Choices => choices;

    public bool IsEmpty => choices.Count == 0;

    //Caches

    private void Invalidate()
    {
        dirty = true;
        cachedStatOffset.Clear();
        cachedStatFactor.Clear();
    }

    private void EnsureCaches()
    {
        if (!dirty)
        {
            return;
        }

        byGene.Clear();
        activeChoices.Clear();
        activeSets.Clear();
        activeTiers.Clear();
        activeVariants.Clear();
        anyHidesBaseGraphic = false;

        var pawn = Pawn;
        foreach (var choice in choices)
        {
            if (choice?.gene == null || choice.variant == null)
            {
                continue;
            }

            byGene[choice.gene] = choice;

            if (pawn?.genes == null || !pawn.genes.HasActiveGene(choice.gene))
            {
                continue;
            }

            activeChoices.Add(choice);
            activeVariants.Add(choice.variant);
            if (choice.variant.hidesBaseGraphic)
            {
                anyHidesBaseGraphic = true;
            }
        }

        var seenSets = new HashSet<GeneVariantSetDef>();
        foreach (var variant in activeVariants)
        {
            foreach (var set in GeneVariantIndex.SetsFor(variant))
            {
                if (!seenSets.Add(set))
                {
                    continue;
                }

                var count = set.CountIn(activeVariants);
                var anyTier = false;
                foreach (var tier in set.tiers)
                {
                    if (count >= tier.requiredCount)
                    {
                        activeTiers.Add(tier);
                        anyTier = true;
                    }
                }

                if (anyTier)
                {
                    activeSets.Add(set);
                }
            }
        }

        activeSets.SortBy(set => set.sortOrder);
        dirty = false;
    }

    //Queries

    public GeneVariantChoice ChoiceFor(GeneDef geneDef)
    {
        EnsureCaches();
        return geneDef != null && byGene.TryGetValue(geneDef, out var choice) ? choice : null;
    }

    public GeneVariantDef VariantFor(GeneDef geneDef)
    {
        return ChoiceFor(geneDef)?.variant;
    }

    public DecorationSettings SettingsFor(GeneDef geneDef)
    {
        return ChoiceFor(geneDef)?.settings;
    }

    public List<GeneVariantChoice> ActiveChoices
    {
        get
        {
            EnsureCaches();
            return activeChoices;
        }
    }

    public List<GeneVariantSetDef> ActiveSets
    {
        get
        {
            EnsureCaches();
            return activeSets;
        }
    }

    public List<GeneSetTier> ActiveTiers
    {
        get
        {
            EnsureCaches();
            return activeTiers;
        }
    }

    public HashSet<GeneVariantDef> ActiveVariants
    {
        get
        {
            EnsureCaches();
            return activeVariants;
        }
    }

    public bool AnyHidesBaseGraphic
    {
        get
        {
            EnsureCaches();
            return anyHidesBaseGraphic;
        }
    }

    public bool HidesBaseGraphic(GeneDef geneDef)
    {
        EnsureCaches();
        if (!anyHidesBaseGraphic || geneDef == null)
        {
            return false;
        }

        foreach (var choice in activeChoices)
        {
            if (choice.gene == geneDef)
            {
                return choice.variant.hidesBaseGraphic;
            }
        }

        return false;
    }

    public int SetCount(GeneVariantSetDef set)
    {
        EnsureCaches();
        return set?.CountIn(activeVariants) ?? 0;
    }

    public bool IsTierActive(GeneSetTier tier)
    {
        EnsureCaches();
        return activeTiers.Contains(tier);
    }

    /// <summary>
    /// Every active gene on the pawn that is a customization slot, ordered by category then slot.
    /// </summary>
    public List<GeneDef> CustomizableGenes()
    {
        var result = new List<GeneDef>();
        var pawn = Pawn;
        if (pawn?.genes == null)
        {
            return result;
        }

        foreach (var gene in pawn.genes.GenesListForReading)
        {
            if (gene.Active && GeneVariantIndex.IsCustomizable(gene.def) && !result.Contains(gene.def))
            {
                result.Add(gene.def);
            }
        }

        result.SortBy(geneDef =>
        {
            var extension = geneDef.GetModExtension<DefModExtension_CustomizableGene>();
            var category = extension?.category ?? Core40kDefOf.BEWH_GeneSlot_Undefined;
            return (category.sortOrder, extension?.sortOrder ?? 0f, geneDef.label ?? string.Empty);
        });

        return result;
    }

    public bool HasCustomizableGenes()
    {
        var pawn = Pawn;
        if (pawn?.genes == null)
        {
            return false;
        }

        foreach (var gene in pawn.genes.GenesListForReading)
        {
            if (gene.Active && GeneVariantIndex.IsCustomizable(gene.def))
            {
                return true;
            }
        }

        return false;
    }

    //Edits

    public void SetVariant(GeneDef geneDef, GeneVariantDef variant)
    {
        if (geneDef == null)
        {
            return;
        }

        var choice = ChoiceFor(geneDef);
        if (variant == null)
        {
            if (choice == null)
            {
                return;
            }

            choices.Remove(choice);
        }
        else if (choice == null)
        {
            choice = new GeneVariantChoice(geneDef, variant);
            choices.Add(choice);
            ApplyDefaultColours(choice);
        }
        else if (choice.variant != variant)
        {
            choice.variant = variant;
            ApplyDefaultColours(choice);
        }
        else
        {
            return;
        }

        OnChanged();
    }

    public Color SourceColour(GeneVariantColourSource source, Color fallback)
    {
        var story = Pawn?.story;
        if (story == null)
        {
            return fallback;
        }

        return source switch
        {
            GeneVariantColourSource.Skin => story.SkinColor,
            GeneVariantColourSource.Hair => story.HairColor,
            _ => fallback,
        };
    }

    public void ApplyDefaultColours(GeneVariantChoice choice)
    {
        var variant = choice?.variant;
        if (variant == null)
        {
            return;
        }

        var sourced = SourceColour(variant.defaultColourSource, Color.white);
        choice.settings.Color = variant.defaultColour ?? sourced;
        choice.settings.ColorTwo = variant.defaultColourTwo ?? sourced;
        choice.settings.ColorThree = variant.defaultColourThree ?? sourced;
        choice.settings.maskDef = variant.defaultMask;
    }

    public void SetDefaultColours(GeneDef geneDef)
    {
        var choice = ChoiceFor(geneDef);
        if (choice == null)
        {
            return;
        }

        ApplyDefaultColours(choice);
        Notify_GraphicChanged();
    }

    public void SetColour(GeneDef geneDef, int colourIndex, Color colour)
    {
        var choice = ChoiceFor(geneDef);
        if (choice == null)
        {
            return;
        }

        switch (colourIndex)
        {
            case 1:
                choice.settings.Color = colour;
                break;
            case 2:
                choice.settings.ColorTwo = colour;
                break;
            case 3:
                choice.settings.ColorThree = colour;
                break;
            default:
                return;
        }

        Notify_GraphicChanged();
    }

    public void SetColours(GeneDef geneDef, Color colour, Color colourTwo, Color colourThree)
    {
        var choice = ChoiceFor(geneDef);
        if (choice == null)
        {
            return;
        }

        choice.settings.Color = colour;
        choice.settings.ColorTwo = colourTwo;
        choice.settings.ColorThree = colourThree;
        Notify_GraphicChanged();
    }

    public void SetMask(GeneDef geneDef, MaskDef maskDef)
    {
        var choice = ChoiceFor(geneDef);
        if (choice == null)
        {
            return;
        }

        choice.settings.maskDef = maskDef;
        Notify_GraphicChanged();
    }

    public void ClearAll()
    {
        if (choices.Count == 0)
        {
            return;
        }

        choices.Clear();
        OnChanged();
    }

    public List<GeneVariantChoice> Snapshot()
    {
        return choices.Select(choice => new GeneVariantChoice(choice)).ToList();
    }

    public void Restore(List<GeneVariantChoice> snapshot)
    {
        choices = snapshot?.Select(choice => new GeneVariantChoice(choice)).ToList() ?? [];
        OnChanged();
    }

    private void OnChanged()
    {
        Invalidate();
        ReconcileGrants();
        Notify_GraphicChanged();
    }

    public void Notify_GraphicChanged()
    {
        var pawn = Pawn;
        if (pawn == null)
        {
            return;
        }

        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(pawn);
    }

    //Validation

    /// <summary>
    /// Strips every choice whose variant no longer applies or whose slot or variant requirements
    /// are no longer met, the same way decorations are stripped from armour on equip. Choices for
    /// genes the pawn no longer carries are kept, inert, so re-adding the gene restores them.
    /// </summary>
    public bool RemoveInvalidChoices()
    {
        var pawn = Pawn;
        List<GeneVariantChoice> toRemove = null;

        foreach (var choice in choices)
        {
            if (choice?.gene == null || choice.variant == null)
            {
                toRemove ??= [];
                toRemove.Add(choice);
                continue;
            }

            if (pawn?.genes == null || !pawn.genes.HasActiveGene(choice.gene))
            {
                continue;
            }

            var extension = choice.gene.GetModExtension<DefModExtension_CustomizableGene>();
            if (extension == null
                || !choice.variant.AppliesToGene(choice.gene)
                || !extension.MeetsRequirements(pawn)
                || !choice.variant.MeetsRequirements(pawn))
            {
                toRemove ??= [];
                toRemove.Add(choice);
            }
        }

        if (toRemove == null)
        {
            return false;
        }

        foreach (var choice in toRemove)
        {
            choices.Remove(choice);
        }

        return true;
    }

    /// <summary>
    /// Re-validates every choice and brings granted hediffs and abilities back in line with the
    /// active variants and set tiers. Safe to call repeatedly.
    /// </summary>
    public void Reconcile()
    {
        var changed = RemoveInvalidChoices();
        Invalidate();
        ReconcileGrants();
        if (changed)
        {
            Notify_GraphicChanged();
        }
    }

    private void ReconcileGrants()
    {
        var pawn = Pawn;
        if (pawn == null || pawn.Dead)
        {
            return;
        }

        EnsureCaches();

        var expectedHediffs = new HashSet<HediffDef>();
        var expectedAbilities = new HashSet<AbilityDef>();
        var expectedVFEAbilities = new HashSet<VEF.Abilities.AbilityDef>();

        foreach (var choice in activeChoices)
        {
            expectedHediffs.AddRange(choice.variant.givesHediffs);
            expectedAbilities.AddRange(choice.variant.givesAbilities);
            expectedVFEAbilities.AddRange(choice.variant.givesVFEAbilities);
        }

        foreach (var tier in activeTiers)
        {
            expectedHediffs.AddRange(tier.givesHediffs);
            expectedAbilities.AddRange(tier.givesAbilities);
            expectedVFEAbilities.AddRange(tier.givesVFEAbilities);
        }

        if (pawn.health?.hediffSet != null)
        {
            var hediffs = pawn.health.hediffSet.hediffs;
            for (var i = hediffs.Count - 1; i >= 0; i--)
            {
                var hediff = hediffs[i];
                if (GeneVariantIndex.IsGrantable(hediff.def) && !expectedHediffs.Contains(hediff.def))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }

            foreach (var hediffDef in expectedHediffs)
            {
                if (!pawn.health.hediffSet.HasHediff(hediffDef))
                {
                    pawn.health.AddHediff(hediffDef);
                }
            }
        }

        if (pawn.abilities != null)
        {
            List<AbilityDef> toRemove = null;
            foreach (var ability in pawn.abilities.abilities)
            {
                if (GeneVariantIndex.IsGrantable(ability.def) && !expectedAbilities.Contains(ability.def))
                {
                    toRemove ??= [];
                    toRemove.Add(ability.def);
                }
            }

            List<AbilityDef> toAdd = null;
            foreach (var abilityDef in expectedAbilities)
            {
                if (pawn.abilities.GetAbility(abilityDef, false) == null)
                {
                    toAdd ??= [];
                    toAdd.Add(abilityDef);
                }
            }

            if (toRemove != null)
            {
                pawn.RemoveAbilities(toRemove, null);
            }
            if (toAdd != null)
            {
                pawn.AddAbilities(toAdd, null);
            }
        }

        var vefComp = pawn.GetComp<CompAbilities>();
        if (vefComp != null)
        {
            List<VEF.Abilities.AbilityDef> toRemove = null;
            foreach (var ability in vefComp.LearnedAbilities)
            {
                if (GeneVariantIndex.IsGrantable(ability.def) && !expectedVFEAbilities.Contains(ability.def))
                {
                    toRemove ??= [];
                    toRemove.Add(ability.def);
                }
            }

            List<VEF.Abilities.AbilityDef> toAdd = null;
            foreach (var abilityDef in expectedVFEAbilities)
            {
                if (!vefComp.LearnedAbilities.Any(ability => ability.def == abilityDef))
                {
                    toAdd ??= [];
                    toAdd.Add(abilityDef);
                }
            }

            if (toRemove != null)
            {
                pawn.RemoveAbilities(null, toRemove);
            }
            if (toAdd != null)
            {
                pawn.AddAbilities(null, toAdd);
            }
        }
    }

    //Availability notifications

    public bool HasAnnounced(GeneVariantDef variant)
    {
        return announcedVariants != null && announcedVariants.Contains(variant);
    }

    public void MarkAnnounced(GeneVariantDef variant)
    {
        announcedVariants ??= [];
        announcedVariants.Add(variant);
    }

    //Stats

    public override float GetStatOffset(StatDef stat)
    {
        if (cachedStatOffset.TryGetValue(stat, out var cached))
        {
            return cached;
        }

        var num = 0f;
        foreach (var choice in ActiveChoices)
        {
            var variant = choice.variant;
            if (!variant.statOffsets.NullOrEmpty())
            {
                num += variant.statOffsets.GetStatOffsetFromList(stat);
            }

            if (variant.statMode == GeneStatMode.Replace && !choice.gene.statOffsets.NullOrEmpty())
            {
                num -= choice.gene.statOffsets.GetStatOffsetFromList(stat);
            }
        }

        foreach (var tier in ActiveTiers)
        {
            if (!tier.statOffsets.NullOrEmpty())
            {
                num += tier.statOffsets.GetStatOffsetFromList(stat);
            }
        }

        cachedStatOffset[stat] = num;
        return num;
    }

    public override float GetStatFactor(StatDef stat)
    {
        if (cachedStatFactor.TryGetValue(stat, out var cached))
        {
            return cached;
        }

        var num = 1f;
        foreach (var choice in ActiveChoices)
        {
            var variant = choice.variant;
            if (!variant.statFactors.NullOrEmpty())
            {
                num *= variant.statFactors.GetStatFactorFromList(stat);
            }

            if (variant.statMode == GeneStatMode.Replace && !choice.gene.statFactors.NullOrEmpty())
            {
                var baseFactor = choice.gene.statFactors.GetStatFactorFromList(stat);
                if (!Mathf.Approximately(baseFactor, 0f))
                {
                    num /= baseFactor;
                }
            }
        }

        foreach (var tier in ActiveTiers)
        {
            if (!tier.statFactors.NullOrEmpty())
            {
                num *= tier.statFactors.GetStatFactorFromList(stat);
            }
        }

        cachedStatFactor[stat] = num;
        return num;
    }

    public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
    {
        var active = ActiveChoices;
        if (active.Count == 0 && ActiveTiers.Count == 0)
        {
            base.GetStatsExplanation(stat, sb, whitespace);
            return;
        }

        var stringBuilder = new StringBuilder();

        foreach (var choice in active)
        {
            var offset = GeneVariantStatUtility.NetOffset(choice.gene, choice.variant, stat) - GeneVariantStatUtility.NetOffset(choice.gene, null, stat);
            if (!Mathf.Approximately(offset, 0f))
            {
                stringBuilder.AppendLine(whitespace + "    " + choice.variant.LabelCap + ": " + stat.Worker.ValueToString(offset, finalized: false, ToStringNumberSense.Offset));
            }

            var baseFactor = GeneVariantStatUtility.NetFactor(choice.gene, null, stat);
            var factor = Mathf.Approximately(baseFactor, 0f) ? 1f : GeneVariantStatUtility.NetFactor(choice.gene, choice.variant, stat) / baseFactor;
            if (!Mathf.Approximately(factor, 1f))
            {
                stringBuilder.AppendLine(whitespace + "    " + choice.variant.LabelCap + ": " + stat.Worker.ValueToString(factor, finalized: false, ToStringNumberSense.Factor));
            }
        }

        foreach (var set in ActiveSets)
        {
            foreach (var tier in set.tiers)
            {
                if (!IsTierActive(tier))
                {
                    continue;
                }

                var label = set.LabelCap + (tier.label.NullOrEmpty() ? string.Empty : " (" + tier.label + ")");
                var offset = tier.statOffsets.NullOrEmpty() ? 0f : tier.statOffsets.GetStatOffsetFromList(stat);
                if (!Mathf.Approximately(offset, 0f))
                {
                    stringBuilder.AppendLine(whitespace + "    " + label + ": " + stat.Worker.ValueToString(offset, finalized: false, ToStringNumberSense.Offset));
                }

                var factor = tier.statFactors.NullOrEmpty() ? 1f : tier.statFactors.GetStatFactorFromList(stat);
                if (!Mathf.Approximately(factor, 1f))
                {
                    stringBuilder.AppendLine(whitespace + "    " + label + ": " + stat.Worker.ValueToString(factor, finalized: false, ToStringNumberSense.Factor));
                }
            }
        }

        if (stringBuilder.Length != 0)
        {
            sb.AppendLine(whitespace + "BEWH.Framework.StatReport.GeneCustomization".Translate() + ":");
            sb.Append(stringBuilder);
        }
    }

    //Gizmo

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        var pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.Faction is not { IsPlayer: true } || !HasCustomizableGenes())
        {
            yield break;
        }

        var command = new Command_Action
        {
            defaultLabel = "BEWH.Framework.GeneCustomization.GizmoLabel".Translate(),
            defaultDesc = "BEWH.Framework.GeneCustomization.GizmoDesc".Translate(pawn.LabelShortCap),
            icon = GeneCustomizationTex.GizmoIcon,
            action = () => Find.WindowStack.Add(new Dialog_GeneCustomization(pawn)),
        };

        yield return command;
    }

    //Lifecycle

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (!IsEmpty)
        {
            Reconcile();
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref choices, "choices", LookMode.Deep);
        Scribe_Collections.Look(ref announcedVariants, "announcedVariants", LookMode.Def);

        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }

        choices ??= [];
        announcedVariants ??= [];
        announcedVariants.Remove(null);

        var dropped = choices.RemoveAll(choice => choice?.gene == null || choice.variant == null);
        if (dropped > 0)
        {
            Log.Warning("[RimDark] Dropped " + dropped + " gene variant choice(s) with missing defs from " + parent);
        }

        Invalidate();
    }
}
