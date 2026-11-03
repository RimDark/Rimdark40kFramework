using System.Xml;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// A skill with the levels it must be within. In XML either the short form, named after the skill:
/// &lt;Shooting&gt;6&lt;/Shooting&gt; (at least 6), &lt;Shooting&gt;(6, 10)&lt;/Shooting&gt; (6 to 10) or &lt;Shooting/&gt; (any level),
/// or the long form &lt;li&gt;&lt;skill&gt;Shooting&lt;/skill&gt;&lt;level&gt;6&lt;/level&gt;&lt;maxLevel&gt;10&lt;/maxLevel&gt;&lt;/li&gt;.
/// </summary>
public class SkillLevelRange
{
    public SkillDef skill;

    //Minimum level; 0 means none.
    public int level = 0;

    //Maximum level; -1 means none.
    public int maxLevel = -1;

    public SkillLevelRange()
    {
    }

    public SkillLevelRange(SkillDef skill, int level, int maxLevel = -1)
    {
        this.skill = skill;
        this.level = level;
        this.maxLevel = maxLevel;
    }

    public bool InRange(int value)
    {
        return value >= level && (maxLevel < 0 || value <= maxLevel);
    }

    /// <summary>
    /// The levels as shown in requirement lines: "6", "6-10" or "≤ 10". Empty when any level will do.
    /// </summary>
    public string RangeText => FormatRange(level, maxLevel);

    public static string FormatRange(int min, int max)
    {
        if (max < 0)
        {
            return min > 0 ? min.ToString() : string.Empty;
        }

        return min > 0 ? min + "-" + max : "≤ " + max;
    }

    /// <summary>
    /// Reads "6", "(6, 10)", "6~10" or "" into a minimum and maximum (-1 for no maximum).
    /// </summary>
    public static void ParseRange(string text, out int min, out int max)
    {
        min = 0;
        max = -1;
        text = text?.Trim().TrimStart('(').TrimEnd(')');
        if (text.NullOrEmpty())
        {
            return;
        }

        var parts = text.Split(',', '~');
        min = ParseHelper.FromString<int>(parts[0].Trim());
        if (parts.Length > 1)
        {
            max = ParseHelper.FromString<int>(parts[1].Trim());
        }
    }

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        if (xmlRoot.Name != "li")
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "skill", xmlRoot.Name, null, null, typeof(SkillDef));
            ParseRange(xmlRoot.InnerText, out level, out maxLevel);
            return;
        }

        foreach (XmlNode child in xmlRoot.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (child.Name)
            {
                case "skill":
                    DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "skill", child.InnerText.Trim(), null, null, typeof(SkillDef));
                    break;
                case "level":
                    //A range here works too: <level>(6, 10)</level>.
                    ParseRange(child.InnerText, out level, out var max);
                    if (max >= 0)
                    {
                        maxLevel = max;
                    }
                    break;
                case "maxLevel":
                    maxLevel = ParseHelper.FromString<int>(child.InnerText.Trim());
                    break;
                default:
                    Log.Error($"[Core40k] Unknown field <{child.Name}> in a skill requirement entry; expected skill, level or maxLevel.");
                    break;
            }
        }
    }
}
