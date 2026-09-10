using System.Collections.Generic;
using Verse;

namespace Core40k;

/// <summary>
/// Marks a GeneDef as a customization slot. Any pawn carrying the gene can fill the slot with a
/// GeneVariantDef that applies to it, subject to the requirements below.
/// </summary>
public class DefModExtension_CustomizableGene : DefModExtension
{
    public GeneSlotCategoryDef category;

    public float sortOrder = 0f;

    public string slotLabel;

    public bool allowNone = true;

    public List<RankDef> mustHaveRank = null;
    public List<GeneDef> mustHaveGene = null;
    public List<TraitData> mustHaveTrait = null;
    public List<HediffDef> mustHaveHediff = null;
    public List<ResearchProjectDef> mustHaveResearch = null;

    public bool MeetsRequirements(Pawn pawn)
    {
        return RequirementUtility.MeetsRequirements(pawn, mustHaveRank, mustHaveGene, mustHaveTrait, mustHaveHediff, mustHaveResearch);
    }

    public bool HasRequirements(Pawn pawn, out string lockedReason)
    {
        return RequirementUtility.HasRequirements(pawn, mustHaveRank, mustHaveGene, mustHaveTrait, mustHaveHediff, mustHaveResearch, out lockedReason);
    }

    public override void ResolveReferences(Def parentDef)
    {
        base.ResolveReferences(parentDef);
        category ??= Core40kDefOf.BEWH_GeneSlot_Undefined;
    }
}
