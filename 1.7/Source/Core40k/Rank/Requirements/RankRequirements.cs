using Verse;

namespace Core40k;

/// <summary>
/// The rank's colonyLimitOfRank. Built in only: reads the limit from the rank being checked, so it does nothing outside rank checks.
/// </summary>
public class RankRequirement_ColonyLimit : PawnRequirement
{
    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        var rank = context.rank;
        var gameComp = RankDef.GameCompRankInfo;
        if (rank == null || !RankUtils.IsLimited(rank) || gameComp == null)
        {
            return true;
        }

        var (allowed, allowedAmount, currentAmount) = gameComp.CanHaveMoreOfRankWithInfo(rank);

        report.Gap();
        if (rank.colonyLimitOfRank.y == 0)
        {
            report.Line("BEWH.Framework.RankSystem.RequirementsLimitOnlyOneEver".Translate(allowedAmount, currentAmount), allowed, indent: false);
        }
        else
        {
            var limitIncreaseAmount = rank.colonyLimitOfRank.y;
            var text = " " + limitIncreaseAmount;
            switch (limitIncreaseAmount)
            {
                case 1:
                    text = "";
                    break;
                case 2:
                    text += "nd";
                    break;
                case 3:
                    text += "rd";
                    break;
                default:
                    text += "th";
                    break;
            }
            report.Line("BEWH.Framework.RankSystem.RequirementsLimit".Translate(allowedAmount, currentAmount, text), allowed, indent: false);
        }

        return allowed;
    }

    public override bool IsMet(RequirementContext context)
    {
        return context.rank == null || !RankUtils.IsLimited(context.rank) || (RankDef.GameCompRankInfo?.CanHaveMoreOfRank(context.rank) ?? true);
    }
}

/// <summary>
/// The rank's prerequisite ranks from the category being checked (rankRequirements / rankRequirementsOneAmong).
/// Built in only; does nothing outside rank checks.
/// </summary>
public class RankRequirement_CategoryRanks : PawnRequirement
{
    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        var data = context.CategoryData;
        if (data == null)
        {
            return true;
        }

        var allMet = data.RanksAllRequirement.Describe(context, report);
        report.MarkHeaders(allMet);
        var oneAmongMet = data.RanksOneAmongRequirement.Describe(context, report);
        report.MarkHeaders(oneAmongMet);
        return allMet && oneAmongMet;
    }

    public override bool IsMet(RequirementContext context)
    {
        var data = context.CategoryData;
        return data == null || (data.RanksAllRequirement.IsMet(context) && data.RanksOneAmongRequirement.IsMet(context));
    }
}
