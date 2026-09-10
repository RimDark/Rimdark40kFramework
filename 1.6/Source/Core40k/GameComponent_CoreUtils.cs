using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class GameComponent_CoreUtils : GameComponent
{
    public Dictionary<Pawn, CachedDecoratives> cachedDecoratives = new ();
    
    public Dictionary<Pawn, CachedDecoratives> cachedAlternateTexture = new ();

    public Dictionary<(Pawn, Thing), bool> cachedGizmoToggles = new();

    private List<Pawn> gizmoTogglePawns;
    private List<Thing> gizmoToggleThings;
    private List<bool> gizmoToggleValues;
    
    private bool geneVariantBaselineDone;

    public GameComponent_CoreUtils(Game game)
    {
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();
        GeneVariantAvailabilityNotifier.Tick();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        VoidfaringUtility.ClearCache();

        if (geneVariantBaselineDone)
        {
            return;
        }

        GeneVariantAvailabilityNotifier.SeedBaseline();
        geneVariantBaselineDone = true;
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        VoidfaringUtility.ClearCache();
        geneVariantBaselineDone = true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref geneVariantBaselineDone, "geneVariantBaselineDone", false);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            gizmoTogglePawns = [];
            gizmoToggleThings = [];
            gizmoToggleValues = [];

            foreach (var toggle in cachedGizmoToggles)
            {
                var pawn = toggle.Key.Item1;
                var thing = toggle.Key.Item2;
                
                if (pawn == null || thing == null || pawn.Destroyed || thing.Destroyed)
                {
                    continue;
                }

                gizmoTogglePawns.Add(pawn);
                gizmoToggleThings.Add(thing);
                gizmoToggleValues.Add(toggle.Value);
            }
        }

        Scribe_Collections.Look(ref gizmoTogglePawns, "gizmoTogglePawns", LookMode.Reference);
        Scribe_Collections.Look(ref gizmoToggleThings, "gizmoToggleThings", LookMode.Reference);
        Scribe_Collections.Look(ref gizmoToggleValues, "gizmoToggleValues", LookMode.Value);

        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }

        cachedGizmoToggles = new Dictionary<(Pawn, Thing), bool>();

        if (gizmoTogglePawns == null || gizmoToggleThings == null || gizmoToggleValues == null)
        {
            return;
        }

        var count = Math.Min(gizmoTogglePawns.Count, Math.Min(gizmoToggleThings.Count, gizmoToggleValues.Count));
        for (var i = 0; i < count; i++)
        {
            if (gizmoTogglePawns[i] == null || gizmoToggleThings[i] == null)
            {
                continue;
            }

            cachedGizmoToggles[(gizmoTogglePawns[i], gizmoToggleThings[i])] = gizmoToggleValues[i];
        }

        gizmoTogglePawns = null;
        gizmoToggleThings = null;
        gizmoToggleValues = null;
    }
    
    /// <summary>
    /// The framework comps currently contributing stats to a pawn, held as comp references so the
    /// StatWorker postfix never has to scan a comp list.
    /// </summary>
    public class CachedDecoratives
    {
        public List<ThingComp> apparelComps = [];
        public ThingComp weaponComp;

        public bool IsEmpty => apparelComps.Count == 0 && weaponComp == null;

        public void Add(ThingComp comp)
        {
            if (comp.parent is Apparel)
            {
                if (!apparelComps.Contains(comp))
                {
                    apparelComps.Add(comp);
                }
            }
            else
            {
                weaponComp = comp;
            }
        }

        public void Remove(ThingComp comp)
        {
            if (comp.parent is Apparel)
            {
                apparelComps.Remove(comp);
            }
            else if (weaponComp == comp)
            {
                weaponComp = null;
            }
        }
    }
}
