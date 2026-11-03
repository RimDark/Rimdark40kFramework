using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// What a pawn's worn apparel means for the render patches (hidden body, hidden head, alternate form skip flags),
/// worked out once and kept until the apparel or an item's alternate form changes. The patches run per render
/// node per frame, so they read this instead of looking every worn item up each time.
/// Safe to call from the parallel render phase.
/// </summary>
public static class WornApparelRenderCache
{
    public readonly struct HeadHider
    {
        public readonly DefModExtension_HidesHead extension;
        public readonly bool isHeadgear;

        public HeadHider(DefModExtension_HidesHead extension, bool isHeadgear)
        {
            this.extension = extension;
            this.isHeadgear = isHeadgear;
        }
    }

    public sealed class Entry
    {
        //The worn apparel and alternate forms this entry was built from; a mismatch means rebuild.
        internal Apparel[] apparel = [];
        internal CompAlternateTexture[] alternateComps = [];
        internal AlternateBaseFormDef[] alternateForms = [];

        public bool hidesBody;
        public HeadHider[] headHiders = [];
        public bool anyFormSkipFlags;
    }

    private static readonly Entry Empty = new();

    private static readonly ConditionalWeakTable<Pawn, Entry> entries = new();

    public static Entry For(Pawn pawn)
    {
        var worn = pawn?.apparel?.WornApparel;
        if (worn == null || worn.Count == 0)
        {
            return Empty;
        }

        if (entries.TryGetValue(pawn, out var entry) && Matches(entry, worn))
        {
            return entry;
        }

        entry = Build(worn);
        entries.AddOrUpdate(pawn, entry);
        return entry;
    }

    private static bool Matches(Entry entry, List<Apparel> worn)
    {
        if (entry.apparel.Length != worn.Count)
        {
            return false;
        }

        for (var i = 0; i < worn.Count; i++)
        {
            if (entry.apparel[i] != worn[i])
            {
                return false;
            }
        }

        for (var i = 0; i < entry.alternateComps.Length; i++)
        {
            if (entry.alternateComps[i].CurrentAlternateBaseForm != entry.alternateForms[i])
            {
                return false;
            }
        }

        return true;
    }

    private static Entry Build(List<Apparel> worn)
    {
        var entry = new Entry { apparel = worn.ToArray() };
        List<HeadHider> headHiders = null;
        List<CompAlternateTexture> alternateComps = null;

        foreach (var apparel in entry.apparel)
        {
            var def = apparel.def;

            if (def.GetModExtension<DefModExtension_ForcesBodyType>()?.hideBodyGraphic ?? false)
            {
                entry.hidesBody = true;
            }

            var hidesHead = def.GetModExtension<DefModExtension_HidesHead>();
            if (hidesHead != null)
            {
                (headHiders ??= []).Add(new HeadHider(hidesHead, IsHeadgear(def)));
            }

            if (Core40kUtils.DefHasComp<CompAlternateTexture>(def))
            {
                var alternateTexture = apparel.GetComp<CompAlternateTexture>();
                if (alternateTexture != null)
                {
                    (alternateComps ??= []).Add(alternateTexture);
                    if (alternateTexture.CurrentAlternateBaseForm?.renderSkipFlags != null)
                    {
                        entry.anyFormSkipFlags = true;
                    }
                }
            }
        }

        if (headHiders != null)
        {
            entry.headHiders = headHiders.ToArray();
        }

        if (alternateComps != null)
        {
            entry.alternateComps = alternateComps.ToArray();
            entry.alternateForms = Array.ConvertAll(entry.alternateComps, comp => comp.CurrentAlternateBaseForm);
        }

        return entry;
    }

    /// <summary>
    /// Mirrors how vanilla decides whether apparel is drawn as headgear.
    /// </summary>
    private static bool IsHeadgear(ThingDef def)
    {
        var props = def.apparel;
        if (props == null)
        {
            return false;
        }
        var lastLayer = props.LastLayer;
        return props.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead || lastLayer == ApparelLayerDefOf.Overhead || lastLayer == ApparelLayerDefOf.EyeCover;
    }
}
