using System.Collections.Generic;
using System.Text;
using Verse;

namespace Core40k;

/// <summary>
/// Checks lists of PawnRequirements, and turns the older mustHave fields into them.
/// </summary>
public static class RequirementUtility
{
    /// <summary>
    /// Whether every requirement is met. With pawnOnly, requirements that don't look at the pawn (research) are skipped.
    /// </summary>
    public static bool AllMet(List<PawnRequirement> requirements, RequirementContext context, bool pawnOnly = false)
    {
        if (requirements.NullOrEmpty())
        {
            return true;
        }

        foreach (var requirement in requirements)
        {
            if (pawnOnly && !requirement.DependsOnPawn)
            {
                continue;
            }

            if (!requirement.IsMet(context))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether any requirement is met. For lock lists, where meeting one of them is what locks.
    /// </summary>
    public static bool AnyMet(List<PawnRequirement> requirements, RequirementContext context)
    {
        if (requirements.NullOrEmpty())
        {
            return false;
        }

        foreach (var requirement in requirements)
        {
            if (!requirement.IsEmpty && requirement.IsMet(context))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Describes every requirement into the report, marking each header ✓ / ×, and returns whether all are met.
    /// </summary>
    public static bool DescribeAll(List<PawnRequirement> requirements, RequirementContext context, RequirementReport report)
    {
        var met = true;
        if (requirements.NullOrEmpty())
        {
            return met;
        }

        foreach (var requirement in requirements)
        {
            var requirementMet = requirement.Describe(context, report);
            report.MarkHeaders(requirementMet);
            met &= requirementMet;
        }

        return met;
    }

    /// <summary>
    /// The requirement text for a tooltip: every requirement with its lines coloured. Returns whether all are met.
    /// </summary>
    public static bool Report(List<PawnRequirement> requirements, RequirementContext context, out string text)
    {
        var stringBuilder = new StringBuilder();
        var met = DescribeAll(requirements, context, new RequirementReport(stringBuilder));
        text = RequirementReport.Finish(stringBuilder);
        return met;
    }

    /// <summary>
    /// Adds requirements built from the older mustHave fields to list. Empty fields add nothing.
    /// </summary>
    public static void AddLegacy(List<PawnRequirement> list, List<RankDef> ranks = null, List<GeneDef> genes = null, List<GeneDef> genesOneAmong = null,
        List<TraitData> traits = null, List<HediffDef> hediffs = null, List<ResearchProjectDef> research = null)
    {
        if (!ranks.NullOrEmpty())
        {
            var rankData = new List<RankData>();
            foreach (var rank in ranks)
            {
                if (rank != null)
                {
                    rankData.Add(new RankData { rankDef = rank });
                }
            }
            list.Add(new PawnRequirement_Ranks { ranks = rankData });
        }
        if (!genes.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Genes { genes = genes });
        }
        if (!genesOneAmong.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Genes { genes = genesOneAmong, oneAmong = true });
        }
        if (!traits.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Traits { traits = traits });
        }
        if (!hediffs.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Hediffs { hediffs = hediffs });
        }
        if (!research.NullOrEmpty())
        {
            list.Add(new PawnRequirement_Research { projects = research });
        }
    }

    /// <summary>
    /// Adds the non-null entries of an XML &lt;requirements&gt; list.
    /// </summary>
    public static void AddListed(List<PawnRequirement> list, List<PawnRequirement> listed)
    {
        if (listed.NullOrEmpty())
        {
            return;
        }

        foreach (var requirement in listed)
        {
            if (requirement != null)
            {
                list.Add(requirement);
            }
        }
    }

    /// <summary>
    /// Config errors for an XML &lt;requirements&gt; list.
    /// </summary>
    public static IEnumerable<string> ConfigErrors(Def parent, List<PawnRequirement> listed, string fieldName = "requirements")
    {
        if (listed == null)
        {
            yield break;
        }

        foreach (var requirement in listed)
        {
            if (requirement == null)
            {
                yield return $"{parent.defName} has an empty entry in {fieldName}.";
                continue;
            }

            foreach (var error in requirement.ConfigErrors(parent))
            {
                yield return error;
            }
        }
    }

    //Kept for code that still calls the old checks with separate lists.

    public static bool MeetsRequirements(Pawn pawn, List<RankDef> ranks, List<GeneDef> genes, List<TraitData> traits, List<HediffDef> hediffs, List<ResearchProjectDef> research = null, List<GeneDef> genesOneAmong = null)
    {
        var list = new List<PawnRequirement>();
        AddLegacy(list, ranks, genes, genesOneAmong, traits, hediffs, research);
        return AllMet(list, new RequirementContext(pawn));
    }

    public static bool HasRequirements(Pawn pawn, List<RankDef> ranks, List<GeneDef> genes, List<TraitData> traits, List<HediffDef> hediffs, List<ResearchProjectDef> research, out string lockedReason, List<GeneDef> genesOneAmong = null)
    {
        var list = new List<PawnRequirement>();
        AddLegacy(list, ranks, genes, genesOneAmong, traits, hediffs, research);
        return Report(list, new RequirementContext(pawn), out lockedReason);
    }
}
