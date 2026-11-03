using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// What a set of PawnRequirements is checked against: the pawn, and for rank checks also the rank and category.
/// </summary>
public class RequirementContext
{
    public readonly Pawn pawn;

    //Only set for rank checks.
    public readonly RankDef rank;
    public readonly RankCategoryDef category;

    private CompRankInfo rankComp;
    private bool rankCompResolved;
    private float? rankLearningFactor;

    public RequirementContext(Pawn pawn)
    {
        this.pawn = pawn;
    }

    public RequirementContext(Pawn pawn, CompRankInfo rankComp, RankDef rank, RankCategoryDef category)
    {
        this.pawn = pawn;
        this.rankComp = rankComp;
        rankCompResolved = rankComp != null;
        this.rank = rank;
        this.category = category;
    }

    /// <summary>
    /// The pawn's rank comp, or null. Looked up on first use.
    /// </summary>
    public CompRankInfo RankComp
    {
        get
        {
            if (!rankCompResolved)
            {
                rankComp = pawn?.GetComp<CompRankInfo>();
                rankCompResolved = true;
            }

            return rankComp;
        }
    }

    /// <summary>
    /// The rank's entry in the category being checked (its prerequisite ranks), or null outside rank checks.
    /// </summary>
    public RankCategorySpecificData CategoryData => category != null && rank != null && category.rankDict.TryGetValue(rank, out var data) ? data : null;

    /// <summary>
    /// The pawn's BEWH_RankLearningFactor, which scales the days a rank must be held. A full stat evaluation, so it is
    /// only computed once something needs it. 1 without a pawn.
    /// </summary>
    public float RankLearningFactor => rankLearningFactor ??= pawn?.GetStatValue(Core40kDefOf.BEWH_RankLearningFactor) ?? 1f;
}
