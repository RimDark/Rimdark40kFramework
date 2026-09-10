using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

//Startup index of which variants fit which customizable gene, which sets a variant belongs to, and
//everything the system can ever grant, so reconciliation never has to scan the def database.
[StaticConstructorOnStartup]
public static class GeneVariantIndex
{
    private static Dictionary<GeneDef, List<GeneVariantDef>> variantsByGene;
    private static Dictionary<GeneVariantDef, List<GeneVariantSetDef>> setsByVariant;
    private static Dictionary<GeneVariantDef, List<MaskDef>> masksByVariant;
    private static HashSet<GeneDef> customizableGenes;
    private static HashSet<HediffDef> grantableHediffs;
    private static HashSet<AbilityDef> grantableAbilities;
    private static HashSet<VEF.Abilities.AbilityDef> grantableVFEAbilities;

    private static readonly List<GeneVariantDef> EmptyVariants = [];
    private static readonly List<GeneVariantSetDef> EmptySets = [];
    private static readonly List<MaskDef> EmptyMasks = [];

    static GeneVariantIndex()
    {
        Build();
    }

    private static void EnsureBuilt()
    {
        if (variantsByGene == null)
        {
            Build();
        }
    }

    public static void Build()
    {
        variantsByGene = new Dictionary<GeneDef, List<GeneVariantDef>>();
        setsByVariant = new Dictionary<GeneVariantDef, List<GeneVariantSetDef>>();
        masksByVariant = new Dictionary<GeneVariantDef, List<MaskDef>>();
        customizableGenes = [];
        grantableHediffs = [];
        grantableAbilities = [];
        grantableVFEAbilities = [];

        foreach (var geneDef in DefDatabase<GeneDef>.AllDefsListForReading)
        {
            if (geneDef.HasModExtension<DefModExtension_CustomizableGene>())
            {
                customizableGenes.Add(geneDef);
            }
        }

        foreach (var variant in DefDatabase<GeneVariantDef>.AllDefsListForReading)
        {
            foreach (var geneDef in customizableGenes)
            {
                if (variant.AppliesToGene(geneDef))
                {
                    AddTo(variantsByGene, geneDef, variant);
                }
            }

            foreach (var geneDef in variant.appliesTo)
            {
                if (geneDef != null && !customizableGenes.Contains(geneDef))
                {
                    Log.Warning("[RimDark] " + variant.defName + " applies to " + geneDef.defName + " which has no DefModExtension_CustomizableGene");
                }
            }

            AddGrants(variant.givesHediffs, variant.givesAbilities, variant.givesVFEAbilities);
        }

        foreach (var set in DefDatabase<GeneVariantSetDef>.AllDefsListForReading)
        {
            foreach (var variant in set.variants)
            {
                if (variant != null)
                {
                    AddTo(setsByVariant, variant, set);
                }
            }

            foreach (var tier in set.tiers)
            {
                AddGrants(tier.givesHediffs, tier.givesAbilities, tier.givesVFEAbilities);
            }
        }

        foreach (var list in variantsByGene.Values)
        {
            list.SortBy(def => def.sortOrder);
        }

        foreach (var list in setsByVariant.Values)
        {
            list.SortBy(def => def.sortOrder);
        }

        var masks = DefDatabase<MaskDef>.AllDefsListForReading;
        foreach (var variant in DefDatabase<GeneVariantDef>.AllDefsListForReading)
        {
            List<MaskDef> forVariant = null;
            foreach (var mask in masks)
            {
                if (mask.appliesToKind is not (AppliesToKind.ExtraDecoration or AppliesToKind.All))
                {
                    continue;
                }

                if (mask.appliesToKind == AppliesToKind.All || mask.appliesTo.Contains(variant.defName))
                {
                    forVariant ??= [];
                    forVariant.Add(mask);
                }
            }

            if (forVariant == null)
            {
                continue;
            }

            forVariant.SortBy(def => def.sortOrder);
            masksByVariant.Add(variant, forVariant);
        }
    }

    private static void AddGrants(List<HediffDef> hediffs, List<AbilityDef> abilities, List<VEF.Abilities.AbilityDef> vfeAbilities)
    {
        if (!hediffs.NullOrEmpty())
        {
            grantableHediffs.AddRange(hediffs);
        }
        if (!abilities.NullOrEmpty())
        {
            grantableAbilities.AddRange(abilities);
        }
        if (!vfeAbilities.NullOrEmpty())
        {
            grantableVFEAbilities.AddRange(vfeAbilities);
        }
    }

    private static void AddTo<TKey, TValue>(Dictionary<TKey, List<TValue>> dict, TKey key, TValue value)
    {
        if (dict.TryGetValue(key, out var list))
        {
            if (!list.Contains(value))
            {
                list.Add(value);
            }
            return;
        }

        dict.Add(key, [value]);
    }

    public static bool IsCustomizable(GeneDef geneDef)
    {
        EnsureBuilt();
        return geneDef != null && customizableGenes.Contains(geneDef);
    }

    public static List<GeneVariantDef> VariantsFor(GeneDef geneDef)
    {
        EnsureBuilt();
        if (geneDef == null)
        {
            return EmptyVariants;
        }
        return variantsByGene.TryGetValue(geneDef, out var list) ? list : EmptyVariants;
    }

    public static List<GeneVariantSetDef> SetsFor(GeneVariantDef variant)
    {
        EnsureBuilt();
        if (variant == null)
        {
            return EmptySets;
        }
        return setsByVariant.TryGetValue(variant, out var list) ? list : EmptySets;
    }

    public static List<MaskDef> MasksFor(GeneVariantDef variant)
    {
        EnsureBuilt();
        if (variant == null)
        {
            return EmptyMasks;
        }
        return masksByVariant.TryGetValue(variant, out var list) ? list : EmptyMasks;
    }

    public static bool IsGrantable(HediffDef hediffDef)
    {
        EnsureBuilt();
        return hediffDef != null && grantableHediffs.Contains(hediffDef);
    }

    public static bool IsGrantable(AbilityDef abilityDef)
    {
        EnsureBuilt();
        return abilityDef != null && grantableAbilities.Contains(abilityDef);
    }

    public static bool IsGrantable(VEF.Abilities.AbilityDef abilityDef)
    {
        EnsureBuilt();
        return abilityDef != null && grantableVFEAbilities.Contains(abilityDef);
    }
}
