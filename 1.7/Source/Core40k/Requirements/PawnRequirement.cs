using System.Collections.Generic;
using Verse;

namespace Core40k;

/// <summary>
/// One requirement a pawn must meet. Listed with &lt;li Class="..."&gt; in a &lt;requirements&gt; list on ranks, decorations,
/// gene variants, artifacts and rank categories; each of those also turns its older mustHave/required fields into these
/// at load time. Requirements only say what each line is and whether it is met: colours, indentation and the
/// header ✓ / × are handled by RequirementReport.
/// </summary>
public abstract class PawnRequirement
{
    /// <summary>
    /// Writes this requirement's header and lines into the report and returns whether it is met.
    /// The report may be RequirementReport.NoText, so only build text when report.BuildingText is true if it is costly.
    /// context.pawn can be null when there is nobody to judge; a requirement that needs a pawn is then not met.
    /// </summary>
    public abstract bool Describe(RequirementContext context, RequirementReport report);

    /// <summary>
    /// Whether the requirement is met, without any text. Called often (every rank of every colonist, every fitted
    /// decoration on equip), so override it when Describe does work that only the text needs.
    /// </summary>
    public virtual bool IsMet(RequirementContext context)
    {
        return Describe(context, RequirementReport.NoText);
    }

    /// <summary>
    /// False for requirements that don't look at the pawn at all (research). Fitted decorations are re-checked
    /// against only the pawn-dependent ones when the item is equipped.
    /// </summary>
    public virtual bool DependsOnPawn => true;

    /// <summary>
    /// True when there is nothing to check (an empty list, titles while Royalty is off). An empty requirement counts as met,
    /// so lock lists skip it rather than locking.
    /// </summary>
    public virtual bool IsEmpty => false;

    public virtual IEnumerable<string> ConfigErrors(Def parent)
    {
        yield break;
    }
}
