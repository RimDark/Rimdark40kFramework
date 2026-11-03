using System.Collections.Generic;
using System.Text;
using Verse;

namespace Core40k;

/// <summary>
/// Collects the requirement text shown in tooltips and the rank tab. Requirements only say what each line is and whether
/// it is met; spacing, indentation and colour are handled here.
/// </summary>
public class RequirementReport
{
    private const string Indent = "    ";

    /// <summary>
    /// A report that writes nothing, for checks that only need the result.
    /// </summary>
    public static readonly RequirementReport NoText = new(null);

    private const string MetMark = " ✓";
    private const string NotMetMark = " ×";

    private readonly StringBuilder stringBuilder;

    //Where the ✓ / × goes for each header that has not been marked yet.
    private readonly List<int> unmarkedHeaderEnds = [];

    public RequirementReport(StringBuilder stringBuilder)
    {
        this.stringBuilder = stringBuilder;
    }

    /// <summary>
    /// For lists of things the pawn must not have (rank category locks): a met requirement is shown as the bad outcome.
    /// Requirements still return whether they are met; only the colours and marks flip.
    /// </summary>
    public bool Inverted { get; init; }

    public bool BuildingText => stringBuilder != null;

    /// <summary>
    /// Starts a new section: a blank line, then the header. The header gets its ✓ / × from MarkHeaders.
    /// </summary>
    public void Header(string header)
    {
        if (stringBuilder == null)
        {
            return;
        }

        stringBuilder.Append("\n");
        stringBuilder.Append(header);
        unmarkedHeaderEnds.Add(stringBuilder.Length);
        stringBuilder.AppendLine();
    }

    /// <summary>
    /// Puts a ✓ (met) or × (not met) after every header written since the last call.
    /// The framework calls this after each requirement; a requirement with several sections can call it after each one.
    /// </summary>
    public void MarkHeaders(bool met)
    {
        if (stringBuilder == null || unmarkedHeaderEnds.Count == 0)
        {
            return;
        }

        var good = met != Inverted;
        var mark = FormatLine(good ? MetMark : NotMetMark, good, indent: false);
        //Back to front, so inserting does not move the positions still to be marked.
        for (var i = unmarkedHeaderEnds.Count - 1; i >= 0; i--)
        {
            stringBuilder.Insert(unmarkedHeaderEnds[i], mark);
        }

        unmarkedHeaderEnds.Clear();
    }

    /// <summary>
    /// One requirement line, coloured by whether it is met. Indented under the current header unless indent is false.
    /// </summary>
    public void Line(string text, bool met, bool indent = true)
    {
        stringBuilder?.AppendLine(FormatLine(text, met != Inverted, indent));
    }

    /// <summary>
    /// A blank line, for a line that stands on its own without a header.
    /// </summary>
    public void Gap()
    {
        stringBuilder?.Append("\n");
    }

    public static string FormatLine(string text, bool met, bool indent = true)
    {
        var requirementColour = met ? Core40kUtils.RequirementMetColour : Core40kUtils.RequirementNotMetColour;
        return ((indent ? Indent : "") + text).Colorize(requirementColour);
    }

    /// <summary>
    /// Turns the built-up text into the shown requirement text: outer blank lines trimmed, "None" when empty.
    /// </summary>
    public static string Finish(StringBuilder stringBuilder)
    {
        var requirementText = stringBuilder.ToString().TrimEnd('\r', '\n').TrimStart('\r', '\n');
        if (requirementText.NullOrEmpty())
        {
            requirementText = Indent + "BEWH.Framework.CommonKeyword.None".Translate();
        }

        return requirementText;
    }
}
