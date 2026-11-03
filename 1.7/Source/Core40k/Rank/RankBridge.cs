using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace Core40k;

public enum RankBridgeRole
{
    //The tree continues in the other category: the rank's next ranks there are shown beside it.
    Portal,
    //The rank can also be reached from the other category: its prerequisites there are shown beside it.
    Entrance,
}

/// <summary>
/// Links a rank placed in two categories. Written inside a rank's entry in a category as
/// &lt;bridges&gt;&lt;OtherCategoryDef&gt;Portal&lt;/OtherCategoryDef&gt;&lt;/bridges&gt;, optionally followed by where the
/// ghost icons go relative to the rank, in cells: "Entrance (-1.5, 0)".
/// </summary>
public class RankBridge
{
    public RankCategoryDef category;

    public RankBridgeRole role;

    public Vector2? ghostOffset;

    //Added for the other side of a bridge that was only declared on one side.
    public bool implied;

    public RankBridgeRole CounterpartRole => role == RankBridgeRole.Portal ? RankBridgeRole.Entrance : RankBridgeRole.Portal;

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, nameof(category), xmlRoot.Name, null, null, typeof(RankCategoryDef));

        var text = xmlRoot.FirstChild?.Value?.Trim();
        if (text.NullOrEmpty())
        {
            return;
        }

        var offsetStart = text.IndexOf('(');
        if (offsetStart >= 0)
        {
            var offsetText = text.Substring(offsetStart).Trim();
            text = text.Substring(0, offsetStart).Trim();
            try
            {
                ghostOffset = ParseHelper.FromString<Vector2>(offsetText);
            }
            catch (Exception e)
            {
                Log.Error($"<bridges><{xmlRoot.Name}>: can't read the offset \"{offsetText}\" ({e.Message}); expected \"(x, y)\". Placing the ghosts automatically.");
            }
        }

        if (!text.NullOrEmpty() && !Enum.TryParse(text, true, out role))
        {
            Log.Error($"<bridges><{xmlRoot.Name}>: unknown role \"{text}\"; use Portal or Entrance. Using Portal.");
            role = RankBridgeRole.Portal;
        }
    }
}

public static class RankBridgeUtility
{
    private static readonly List<RankBridge> NoBridges = [];

    //Bridges per placed rank, including the implied other side of one-sided bridges. Built on first use, after all defs resolved.
    private static Dictionary<(RankCategoryDef, RankDef), List<RankBridge>> bridges;

    /// <summary>
    /// The bridges of a rank in a category: those declared on it there, and the other side of bridges declared on it in
    /// another category that this category doesn't declare back.
    /// </summary>
    public static List<RankBridge> BridgesOf(RankCategoryDef category, RankDef rank)
    {
        if (category == null || rank == null)
        {
            return NoBridges;
        }

        bridges ??= Build();
        return bridges.TryGetValue((category, rank), out var list) ? list : NoBridges;
    }

    private static Dictionary<(RankCategoryDef, RankDef), List<RankBridge>> Build()
    {
        var result = new Dictionary<(RankCategoryDef, RankDef), List<RankBridge>>();
        foreach (var category in DefDatabase<RankCategoryDef>.AllDefsListForReading)
        {
            foreach (var data in category.ranks)
            {
                if (data?.rankDef == null || data.bridges.NullOrEmpty())
                {
                    continue;
                }

                foreach (var bridge in data.bridges)
                {
                    if (IsUsable(category, data.rankDef, bridge))
                    {
                        Add(result, category, data.rankDef, bridge);
                    }
                }
            }
        }

        //Declaring a bridge on one side is enough; the other side gets the opposite role.
        foreach (var category in DefDatabase<RankCategoryDef>.AllDefsListForReading)
        {
            foreach (var data in category.ranks)
            {
                if (data?.rankDef == null || data.bridges.NullOrEmpty())
                {
                    continue;
                }

                foreach (var bridge in data.bridges)
                {
                    if (!IsUsable(category, data.rankDef, bridge) || Declares(bridge.category, data.rankDef, category))
                    {
                        continue;
                    }

                    Add(result, bridge.category, data.rankDef, new RankBridge { category = category, role = bridge.CounterpartRole, implied = true });
                }
            }
        }

        return result;
    }

    private static bool IsUsable(RankCategoryDef owner, RankDef rank, RankBridge bridge)
    {
        return bridge?.category != null && bridge.category != owner && bridge.category.rankDict.ContainsKey(rank);
    }

    private static bool Declares(RankCategoryDef category, RankDef rank, RankCategoryDef target)
    {
        if (!category.rankDict.TryGetValue(rank, out var data) || data.bridges.NullOrEmpty())
        {
            return false;
        }

        foreach (var bridge in data.bridges)
        {
            if (bridge?.category == target)
            {
                return true;
            }
        }

        return false;
    }

    private static void Add(Dictionary<(RankCategoryDef, RankDef), List<RankBridge>> result, RankCategoryDef category, RankDef rank, RankBridge bridge)
    {
        if (!result.TryGetValue((category, rank), out var list))
        {
            list = [];
            result[(category, rank)] = list;
        }

        list.Add(bridge);
    }

    /// <summary>
    /// Bad bridges on one rank entry of a category, for ConfigErrors.
    /// </summary>
    public static IEnumerable<string> ConfigErrors(RankCategoryDef owner, RankCategorySpecificData data)
    {
        if (data?.rankDef == null || data.bridges.NullOrEmpty())
        {
            yield break;
        }

        var where = $"bridge on {data.rankDef.defName}";
        foreach (var bridge in data.bridges)
        {
            if (bridge?.category == null)
            {
                continue; //A missing category is already reported by the cross-ref loader.
            }

            if (bridge.category == owner)
            {
                yield return $"{where} points to its own category.";
            }
            else if (!bridge.category.rankDict.ContainsKey(data.rankDef))
            {
                yield return $"{where} points to {bridge.category.defName}, which doesn't place {data.rankDef.defName}.";
            }
            //Checked from one side only, so a clash is reported once.
            else if (string.CompareOrdinal(owner.defName, bridge.category.defName) < 0 && bridge.category.rankDict[data.rankDef].bridges is { } backList)
            {
                foreach (var back in backList)
                {
                    if (back?.category == owner && back.role == bridge.role)
                    {
                        yield return $"{where} is a {bridge.role} on both sides ({owner.defName} and {bridge.category.defName}); one side should be the {bridge.CounterpartRole}.";
                    }
                }
            }
        }
    }
}
