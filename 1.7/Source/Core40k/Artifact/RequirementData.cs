using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// A requirement block as one XML node, so artifact kinds and traits do not repeat the fields: the mustHave lists,
/// plus &lt;extra&gt; for any PawnRequirement classes.
/// </summary>
public class RequirementData
{
    public List<RankDef> mustHaveRank = null;
    public List<GeneDef> mustHaveGene = null;
    public List<GeneDef> mustHaveGeneOneAmong = null;
    public List<TraitData> mustHaveTrait = null;
    public List<HediffDef> mustHaveHediff = null;
    public List<ResearchProjectDef> mustHaveResearch = null;

    public List<PawnRequirement> extra = null;

    [Unsaved]
    private List<PawnRequirement> allRequirements;

    /// <summary>
    /// Every requirement in this block: the ones built from the mustHave fields, then &lt;extra&gt;.
    /// </summary>
    public List<PawnRequirement> AllRequirements
    {
        get
        {
            if (allRequirements != null)
            {
                return allRequirements;
            }

            var list = new List<PawnRequirement>();
            RequirementUtility.AddLegacy(list, mustHaveRank, mustHaveGene, mustHaveGeneOneAmong, mustHaveTrait, mustHaveHediff, mustHaveResearch);
            RequirementUtility.AddListed(list, extra);
            allRequirements = list;
            return list;
        }
    }

    public bool IsEmpty => AllRequirements.Count == 0;

    public bool Meets(Pawn pawn)
    {
        return RequirementUtility.AllMet(AllRequirements, new RequirementContext(pawn));
    }

    public bool Has(Pawn pawn, out string lockedReason)
    {
        if (IsEmpty)
        {
            lockedReason = null;
            return true;
        }

        return RequirementUtility.Report(AllRequirements, new RequirementContext(pawn), out lockedReason);
    }

    public IEnumerable<string> ConfigErrors(Def parent, string fieldName)
    {
        return RequirementUtility.ConfigErrors(parent, extra, fieldName + ".extra");
    }

    public static bool IsNullOrEmpty(RequirementData data)
    {
        return data == null || data.IsEmpty;
    }
}
