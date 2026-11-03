using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

//Startup index of "what content applies to which ThingDef".
//Serves two purposes:
// 1. Customization tab resolution needs to know whether an item has any decorations/upgrades/
//    alternate forms at all, without scanning the def database per item.
// 2. The tab drawers used to rescan the whole def database every time a dialog opened, once per
//    comp. They now read from here instead.
public static class DecorationIndex
{
    private static Dictionary<ThingDef, List<DecorationDef>> decorationsByThing;
    private static Dictionary<ThingDef, List<AlternateBaseFormDef>> alternatesByThing;
    private static Dictionary<DecorationDef, List<MaskDef>> masksByDecoration;
    private static Dictionary<ThingDef, List<MaskDef>> masksByThing;
    private static HashSet<StatDef> pawnStatRelevant;
    private static HashSet<StatDef> wielderOffsetRelevant;
    private static bool built;

    private static readonly List<DecorationDef> EmptyDecorations = [];
    private static readonly List<AlternateBaseFormDef> EmptyAlternates = [];
    private static readonly List<MaskDef> EmptyMasks = [];

    private static void EnsureBuilt()
    {
        if (!built)
        {
            Build();
        }
    }

    public static void Build()
    {
        decorationsByThing = new Dictionary<ThingDef, List<DecorationDef>>();
        alternatesByThing = new Dictionary<ThingDef, List<AlternateBaseFormDef>>();
        masksByDecoration = new Dictionary<DecorationDef, List<MaskDef>>();
        masksByThing = new Dictionary<ThingDef, List<MaskDef>>();
        pawnStatRelevant = new HashSet<StatDef>();
        wielderOffsetRelevant = new HashSet<StatDef>();

        //Only things that can actually carry a decoration are considered, so an appliesToAll
        //decoration does not attach itself to every ThingDef in the game.
        var decorables = DefDatabase<ThingDef>.AllDefs
            .Where(thingDef => thingDef.HasCompAssignable(typeof(CompDecorativeBase)))
            .ToList();

        //Same query shape the tabs used before, so nothing changes about which defs are considered.
        var decorations = DefDatabase<DecorationDef>.AllDefs.Where(def => def is not AlternateBaseFormDef).ToList();

        foreach (var decoration in decorations)
        {
            if (decoration.appliesToAll)
            {
                foreach (var thingDef in decorables)
                {
                    AddTo(decorationsByThing, thingDef, decoration);
                }
                continue;
            }

            foreach (var name in decoration.appliesTo)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (thingDef == null)
                {
                    Log.Warning("[RimDark] " + decoration.defName + " appliesTo unknown ThingDef " + name);
                    continue;
                }
                AddTo(decorationsByThing, thingDef, decoration);
            }
        }

        //Alternate base forms deliberately do not honour appliesToAll.
        foreach (var alternate in DefDatabase<AlternateBaseFormDef>.AllDefs)
        {
            foreach (var name in alternate.appliesTo)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (thingDef == null)
                {
                    Log.Warning("[RimDark] " + alternate.defName + " appliesTo unknown ThingDef " + name);
                    continue;
                }
                AddTo(alternatesByThing, thingDef, alternate);
            }
        }

        foreach (var list in decorationsByThing.Values)
        {
            list.SortBy(def => def.sortOrder);
        }
        foreach (var list in alternatesByThing.Values)
        {
            list.SortBy(def => def.sortOrder);
        }

        BuildMasks(decorations, decorables);
        BuildPawnStatRelevant();

        built = true;
    }

    //Every DecorationDef, alternates included, since AlternateBaseFormDef inherits the pawn stat lists.
    private static void BuildPawnStatRelevant()
    {
        foreach (var decoration in DefDatabase<DecorationDef>.AllDefsListForReading)
        {
            AddStats(decoration.pawnStatOffsets);
            AddStats(decoration.pawnStatFactors);
        }

        //Artifact trait wielder factors ride the same cached route; offsets go through StatOffsetFromGear.
        foreach (var trait in DefDatabase<ArtifactTraitDef>.AllDefsListForReading)
        {
            AddStats(trait.wielderStatFactors);
            AddStats(trait.wielderStatOffsets, wielderOffsetRelevant);
        }
    }

    private static void AddStats(List<StatModifier> modifiers, HashSet<StatDef> into = null)
    {
        if (modifiers.NullOrEmpty())
        {
            return;
        }

        into ??= pawnStatRelevant;
        foreach (var modifier in modifiers)
        {
            if (modifier?.stat != null)
            {
                into.Add(modifier.stat);
            }
        }
    }

    private static void BuildMasks(List<DecorationDef> decorations, List<ThingDef> decorables)
    {
        var decorationMasks = DefDatabase<MaskDef>.AllDefs
            .Where(def => def.appliesToKind is AppliesToKind.ExtraDecoration or AppliesToKind.All)
            .ToList();

        foreach (var decoration in decorations)
        {
            var forDecoration = decorationMasks
                .Where(mask => mask.appliesTo.Contains(decoration.defName) || mask.appliesToKind == AppliesToKind.All)
                .ToList();

            if (forDecoration.Count == 0)
            {
                continue;
            }

            forDecoration.SortBy(def => def.sortOrder);
            masksByDecoration.Add(decoration, forDecoration);
        }

        var thingMasks = DefDatabase<MaskDef>.AllDefs
            .Where(def => def.appliesToKind is AppliesToKind.Thing or AppliesToKind.All)
            .ToList();

        //Coloring applies to anything with CompMultiColor, which is a wider set than the decorables.
        var colorables = DefDatabase<ThingDef>.AllDefs
            .Where(thingDef => thingDef.HasCompAssignable(typeof(CompMultiColor)))
            .ToList();

        foreach (var thingDef in colorables)
        {
            var forThing = thingMasks
                .Where(mask => mask.appliesTo.Contains(thingDef.defName) || mask.appliesToKind == AppliesToKind.All)
                .ToList();

            if (forThing.Count == 0)
            {
                continue;
            }

            forThing.SortBy(def => def.sortOrder);
            masksByThing.Add(thingDef, forThing);
        }
    }

    private static void AddTo<TKey, TValue>(Dictionary<TKey, List<TValue>> dict, TKey key, TValue value)
    {
        if (dict.TryGetValue(key, out var list))
        {
            list.Add(value);
            return;
        }

        dict.Add(key, [value]);
    }

    /// <summary>
    /// Whether any loaded DecorationDef or AlternateBaseFormDef can offset or factor this stat on the
    /// wearing pawn. Lets the StatWorker postfix skip stats the framework can never touch.
    /// </summary>
    public static bool AffectsPawnStat(StatDef stat)
    {
        return built && stat != null && pawnStatRelevant.Count != 0 && pawnStatRelevant.Contains(stat);
    }

    /// <summary>
    /// Whether any loaded ArtifactTraitDef has a wielderStatOffsets entry for this stat. Lets the
    /// StatOffsetFromGear postfix skip stats no artifact can offset.
    /// </summary>
    public static bool AffectsWielderStatOffset(StatDef stat)
    {
        return built && stat != null && wielderOffsetRelevant.Count != 0 && wielderOffsetRelevant.Contains(stat);
    }

    public static List<DecorationDef> DecorationsFor(ThingDef thingDef)
    {
        EnsureBuilt();
        if (thingDef == null)
        {
            return EmptyDecorations;
        }
        return decorationsByThing.TryGetValue(thingDef, out var list) ? list : EmptyDecorations;
    }

    //Split of the above by IsUpgrade, which is what separates the Decoration tab from the
    //Upgrades tab. Allocates, so callers cache it for the lifetime of a dialog.
    public static List<DecorationDef> DecorationsFor(ThingDef thingDef, bool upgrades)
    {
        var all = DecorationsFor(thingDef);
        var result = new List<DecorationDef>();
        foreach (var decoration in all)
        {
            if (decoration.showInDecorationTab && decoration.IsUpgrade == upgrades)
            {
                result.Add(decoration);
            }
        }
        return result;
    }

    public static bool HasDecorations(ThingDef thingDef, bool upgrades)
    {
        foreach (var decoration in DecorationsFor(thingDef))
        {
            if (decoration.showInDecorationTab && decoration.IsUpgrade == upgrades)
            {
                return true;
            }
        }
        return false;
    }

    public static List<AlternateBaseFormDef> AlternatesFor(ThingDef thingDef)
    {
        EnsureBuilt();
        if (thingDef == null)
        {
            return EmptyAlternates;
        }
        return alternatesByThing.TryGetValue(thingDef, out var list) ? list : EmptyAlternates;
    }

    public static List<MaskDef> MasksFor(DecorationDef decoration)
    {
        EnsureBuilt();
        if (decoration == null)
        {
            return EmptyMasks;
        }
        return masksByDecoration.TryGetValue(decoration, out var list) ? list : EmptyMasks;
    }

    public static List<MaskDef> MasksFor(ThingDef thingDef)
    {
        EnsureBuilt();
        if (thingDef == null)
        {
            return EmptyMasks;
        }
        return masksByThing.TryGetValue(thingDef, out var list) ? list : EmptyMasks;
    }

    //Customization tabs are detected automatically now. Anything still carrying the old extension
    //is dead weight, so say so once with the list of defs rather than once per def.
    internal static void WarnOnDeprecatedTabExtension()
    {
        var stale = DefDatabase<ThingDef>.AllDefs
            .Where(thingDef => thingDef.HasModExtension<DefModExtension_AvailableDrawerTabDefs>())
            .Select(thingDef => thingDef.defName)
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append("[RimDark] DefModExtension_AvailableDrawerTabDefs is ignored - customization tabs are now detected automatically from comps and applicable content. It can be deleted from ");
        builder.Append(stale.Count);
        builder.Append(" ThingDef(s): ");
        builder.Append(string.Join(", ", stale.Take(15)));
        if (stale.Count > 15)
        {
            builder.Append(", +" + (stale.Count - 15) + " more");
        }

        Log.Warning(builder.ToString());
    }
}

//Built from here rather than from a static constructor on DecorationIndex itself, so the index is
//never raised early by an off-main-thread caller and always sees the finished def database.
[StaticConstructorOnStartup]
internal static class DecorationIndexStartup
{
    static DecorationIndexStartup()
    {
        DecorationIndex.Build();
        DecorationIndex.WarnOnDeprecatedTabExtension();
    }
}
