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
    public List<GeneDef> mustHaveGeneOneAmong = null;
    public List<TraitData> mustHaveTrait = null;
    public List<HediffDef> mustHaveHediff = null;
    public List<ResearchProjectDef> mustHaveResearch = null;

    //Extra requirements, checked after the ones built from the mustHave fields above.
    public List<PawnRequirement> requirements = [];

    [Unsaved]
    private List<PawnRequirement> allRequirements;

    /// <summary>
    /// Every requirement to use this slot: the ones built from the mustHave fields, then &lt;requirements&gt;.
    /// </summary>
    public List<PawnRequirement> AllRequirements => allRequirements ??= BuildRequirementList();

    private List<PawnRequirement> BuildRequirementList()
    {
        var list = new List<PawnRequirement>();
        RequirementUtility.AddLegacy(list, mustHaveRank, mustHaveGene, mustHaveGeneOneAmong, mustHaveTrait, mustHaveHediff, mustHaveResearch);
        RequirementUtility.AddListed(list, requirements);
        return list;
    }

    public bool MeetsRequirements(Pawn pawn)
    {
        return RequirementUtility.AllMet(AllRequirements, new RequirementContext(pawn));
    }

    public bool HasRequirements(Pawn pawn, out string lockedReason)
    {
        return RequirementUtility.Report(AllRequirements, new RequirementContext(pawn), out lockedReason);
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
        {
            yield return error;
        }

        if (requirements == null)
        {
            yield break;
        }

        foreach (var requirement in requirements)
        {
            if (requirement == null)
            {
                yield return "DefModExtension_CustomizableGene has an empty entry in requirements.";
            }
        }
    }

    public override void ResolveReferences(Def parentDef)
    {
        base.ResolveReferences(parentDef);
        category ??= Core40kDefOf.BEWH_GeneSlot_Undefined;
    }
}
