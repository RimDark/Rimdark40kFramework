using System.Collections.Generic;
using Verse;

namespace Core40k;

/// <summary>
/// Base for requirements on a list of things: the pawn needs all of them, with oneAmong at least one,
/// or with minCount at least that many.
/// </summary>
public abstract class PawnRequirement_List<T> : PawnRequirement
{
    public bool oneAmong = false;

    //At least this many of the items; 0 leaves it to oneAmong. Takes precedence over oneAmong.
    public int minCount = 0;

    protected abstract List<T> Items { get; }

    protected abstract string HeaderAllKey { get; }

    protected abstract string HeaderOneAmongKey { get; }

    /// <summary>
    /// Whether the pawn has this item. Only called with a pawn; without one every item counts as missing.
    /// </summary>
    protected abstract bool Has(RequirementContext context, T item);

    protected abstract string LabelFor(RequirementContext context, T item);

    public override bool IsEmpty => Items.NullOrEmpty();

    /// <summary>
    /// How many of count items the pawn needs.
    /// </summary>
    protected int NeededOf(int count)
    {
        if (minCount > 0)
        {
            return minCount;
        }

        return oneAmong ? 1 : count;
    }

    protected string HeaderText()
    {
        if (minCount > 0)
        {
            return "BEWH.Framework.Requirements.AtLeastCount".Translate(minCount);
        }

        return (oneAmong ? HeaderOneAmongKey : HeaderAllKey).Translate();
    }

    protected bool HasItem(RequirementContext context, T item)
    {
        return (context.pawn != null || !DependsOnPawn) && Has(context, item);
    }

    public override bool Describe(RequirementContext context, RequirementReport report)
    {
        var items = Items;
        if (items.NullOrEmpty())
        {
            return true;
        }

        report.Header(HeaderText());

        var metCount = 0;
        foreach (var item in items)
        {
            var has = HasItem(context, item);
            if (has)
            {
                metCount++;
            }
            report.Line(LabelFor(context, item), has);
        }

        return metCount >= NeededOf(items.Count);
    }

    public override bool IsMet(RequirementContext context)
    {
        var items = Items;
        if (items.NullOrEmpty())
        {
            return true;
        }

        var needed = NeededOf(items.Count);
        var metCount = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (HasItem(context, items[i]))
            {
                metCount++;
                if (metCount >= needed)
                {
                    return true;
                }
            }
            else if (items.Count - i - 1 + metCount < needed)
            {
                //Too few items left to reach needed.
                return false;
            }
        }

        return metCount >= needed;
    }

    public override IEnumerable<string> ConfigErrors(Def parent)
    {
        if (Items.NullOrEmpty())
        {
            yield return $"{parent.defName} has a {GetType().Name} with nothing listed.";
        }
        else if (minCount > Items.Count)
        {
            yield return $"{parent.defName} has a {GetType().Name} with minCount {minCount} but only {Items.Count} entries, so it can never be met.";
        }
    }
}
