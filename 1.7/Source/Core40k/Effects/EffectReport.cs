using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Effect text for tooltips and info panels (ranks, decorations, gene variants, set bonuses), written as sections of lines.
/// Lines written under a header that already exists join that section, so several effects can share "Stats" or "Abilities".
/// Rendered as the header followed by its lines indented, with a blank line between sections.
/// </summary>
public class EffectReport
{
    public const string Indent = "    ";

    private sealed class Section
    {
        public string header;
        public readonly List<string> lines = [];
    }

    private readonly List<Section> sections = [];
    private Section current;

    public bool Empty
    {
        get
        {
            foreach (var section in sections)
            {
                if (section.lines.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Writes the following lines under this header. A header used before continues its section. Null or empty means lines without a header.
    /// </summary>
    public EffectReport Header(string header)
    {
        header ??= string.Empty;
        foreach (var section in sections)
        {
            if (section.header == header)
            {
                current = section;
                return this;
            }
        }

        current = new Section { header = header };
        sections.Add(current);
        return this;
    }

    public EffectReport Line(string text)
    {
        if (text.NullOrEmpty())
        {
            return this;
        }

        if (current == null)
        {
            Header(null);
        }

        current.lines.Add(text);
        return this;
    }

    public EffectReport Lines(IEnumerable<string> texts)
    {
        if (texts == null)
        {
            return this;
        }

        foreach (var text in texts)
        {
            Line(text);
        }

        return this;
    }

    public EffectReport Line(string header, string text)
    {
        if (text.NullOrEmpty())
        {
            return this;
        }

        return Header(header).Line(text);
    }

    public EffectReport StatOffset(StatDef stat, float value)
    {
        return Line(stat.LabelCap + ": " + Core40kUtils.ValueToString(stat, value, finalized: false, ToStringNumberSense.Offset));
    }

    public EffectReport StatFactor(StatDef stat, float value)
    {
        return Line(stat.LabelCap + ": " + Core40kUtils.ValueToString(stat, value, finalized: false, ToStringNumberSense.Factor));
    }

    /// <summary>
    /// Stat offsets then factors under one header ("Stats" when null).
    /// </summary>
    public EffectReport Stats(List<StatModifier> offsets, List<StatModifier> factors, string header = null)
    {
        if (offsets.NullOrEmpty() && factors.NullOrEmpty())
        {
            return this;
        }

        Header(header ?? StatsHeader);
        if (offsets != null)
        {
            foreach (var offset in offsets)
            {
                if (offset?.stat != null)
                {
                    StatOffset(offset.stat, offset.value);
                }
            }
        }
        if (factors != null)
        {
            foreach (var factor in factors)
            {
                if (factor?.stat != null)
                {
                    StatFactor(factor.stat, factor.value);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// One line per def under the header, by label.
    /// </summary>
    public EffectReport Defs<T>(string header, IEnumerable<T> defs) where T : Def
    {
        if (defs == null)
        {
            return this;
        }

        foreach (var def in defs)
        {
            if (def != null)
            {
                Line(header, def.LabelCap);
            }
        }

        return this;
    }

    /// <summary>
    /// Granted abilities (vanilla and VEF) under "Abilities" and hediffs under "Hediffs".
    /// </summary>
    public EffectReport Grants(IEnumerable<AbilityDef> abilities, IEnumerable<VEF.Abilities.AbilityDef> vfeAbilities, IEnumerable<HediffDef> hediffs)
    {
        Defs(AbilitiesHeader, abilities);
        Defs(AbilitiesHeader, vfeAbilities);
        Defs(HediffsHeader, hediffs);
        return this;
    }

    public static string StatsHeader => "BEWH.Framework.RankSystem.Stats".Translate();
    public static string AbilitiesHeader => "BEWH.Framework.RankSystem.Abilities".Translate();
    public static string HediffsHeader => "BEWH.Framework.RankSystem.Hediffs".Translate();
    public static string OtherEffectsHeader => "BEWH.Framework.RankSystem.OtherEffects".Translate();

    /// <summary>
    /// Writes the sections into builder. With leadingGap a blank line comes first, for text that follows a label or description.
    /// </summary>
    public void AppendTo(StringBuilder builder, bool leadingGap = true)
    {
        var first = true;
        foreach (var section in sections)
        {
            if (section.lines.Count == 0)
            {
                continue;
            }

            if (!first || leadingGap)
            {
                builder.AppendLine();
            }
            first = false;

            var indent = string.Empty;
            if (!section.header.NullOrEmpty())
            {
                builder.AppendLine(section.header);
                indent = Indent;
            }

            foreach (var line in section.lines)
            {
                builder.AppendLine(indent + line);
            }
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        AppendTo(builder, leadingGap: false);
        return builder.ToString().TrimEndNewlines();
    }
}
