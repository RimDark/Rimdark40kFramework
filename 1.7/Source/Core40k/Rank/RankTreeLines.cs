using UnityEngine;
using Verse;

namespace Core40k;

/// <summary>
/// Line drawing for the rank tree. Dashes are drawn as plain Widgets.DrawLine segments, only along the part of the
/// line inside the view, so a long line mostly off screen costs only its visible dashes.
/// </summary>
public static class RankTreeLines
{
    //Dash pattern in pixels at 1x: DashOn drawn, the rest of DashPeriod empty.
    private const float DashPeriod = 10f;
    private const float DashOn = 6f;

    /// <summary>
    /// Whether a line from start to end can cross the rect; a cheap bounding-box test to skip lines off screen.
    /// </summary>
    public static bool MayCross(Vector2 start, Vector2 end, Rect rect, float width)
    {
        return Mathf.Max(start.x, end.x) + width >= rect.xMin && Mathf.Min(start.x, end.x) - width <= rect.xMax
            && Mathf.Max(start.y, end.y) + width >= rect.yMin && Mathf.Min(start.y, end.y) - width <= rect.yMax;
    }

    /// <summary>
    /// Draws a dashed line clipped to the view; scale stretches the dashes (pass the tree zoom). The dash pattern is
    /// anchored at start, so it doesn't crawl along the line as the view pans.
    /// </summary>
    public static void DrawDashedLine(Vector2 start, Vector2 end, Color color, float width, Rect view, float scale = 1f)
    {
        var line = end - start;
        var length = line.magnitude;
        if (length < 0.01f || !ClipToRect(start, end, view.ExpandedBy(width), out var from, out var to))
        {
            return;
        }

        var direction = line / length;
        var period = DashPeriod * Mathf.Max(0.1f, scale);
        var on = DashOn * Mathf.Max(0.1f, scale);
        var visibleFrom = from * length;
        var visibleTo = to * length;

        for (var dash = Mathf.Floor(visibleFrom / period) * period; dash < visibleTo; dash += period)
        {
            var dashStart = Mathf.Max(dash, visibleFrom);
            var dashEnd = Mathf.Min(dash + on, visibleTo);
            if (dashEnd > dashStart)
            {
                Widgets.DrawLine(start + direction * dashStart, start + direction * dashEnd, color, width);
            }
        }
    }

    //Liang-Barsky: the part of start..end inside rect, as fractions of the line.
    private static bool ClipToRect(Vector2 start, Vector2 end, Rect rect, out float from, out float to)
    {
        from = 0f;
        to = 1f;
        var delta = end - start;
        return Clip(-delta.x, start.x - rect.xMin, ref from, ref to)
            && Clip(delta.x, rect.xMax - start.x, ref from, ref to)
            && Clip(-delta.y, start.y - rect.yMin, ref from, ref to)
            && Clip(delta.y, rect.yMax - start.y, ref from, ref to);
    }

    private static bool Clip(float p, float q, ref float from, ref float to)
    {
        if (p == 0f)
        {
            return q >= 0f;
        }

        var r = q / p;
        if (p < 0f)
        {
            if (r > to)
            {
                return false;
            }
            from = Mathf.Max(from, r);
        }
        else
        {
            if (r < from)
            {
                return false;
            }
            to = Mathf.Min(to, r);
        }

        return true;
    }
}
