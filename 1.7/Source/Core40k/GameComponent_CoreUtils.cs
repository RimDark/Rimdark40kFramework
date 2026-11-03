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
        ArtifactWeaponUtility.ClearTicking();
        ShieldBashUtility.ClearQueue();
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();
        GeneVariantAvailabilityNotifier.Tick();
        ArtifactWeaponUtility.Tick();
        ShieldBashUtility.Tick();

        if (Find.TickManager.TicksGame % PruneIntervalTicks == 0)
        {
            PruneDestroyed();
        }
    }

    private const int PruneIntervalTicks = GenDate.TicksPerDay;

    /// <summary>
    /// Drops cache entries for destroyed or discarded pawns and items. Unequipping already removes most of them,
    /// but a pawn that is discarded with its gear on never unequips it, so the entry would otherwise stay for the
    /// rest of the session.
    /// </summary>
    private void PruneDestroyed()
    {
        PruneDestroyed(cachedDecoratives);
        PruneDestroyed(cachedAlternateTexture);

        List<(Pawn, Thing)> deadToggles = null;
        foreach (var toggle in cachedGizmoToggles)
        {
            var (pawn, thing) = toggle.Key;
            if (pawn == null || thing == null || pawn.Destroyed || thing.Destroyed || !toggle.Value)
            {
                (deadToggles ??= []).Add(toggle.Key);
            }
        }

        if (deadToggles == null)
        {
            return;
        }

        foreach (var key in deadToggles)
        {
            cachedGizmoToggles.Remove(key);
        }
    }

    private static void PruneDestroyed(Dictionary<Pawn, CachedDecoratives> cache)
    {
        List<Pawn> deadPawns = null;
        foreach (var entry in cache)
        {
            var decoratives = entry.Value;
            if (decoratives != null)
            {
                decoratives.apparelComps.RemoveAll(comp => comp?.parent == null || comp.parent.Destroyed);
                decoratives.weaponComps.RemoveAll(comp => comp?.parent == null || comp.parent.Destroyed);
            }

            if (entry.Key == null || entry.Key.Destroyed || decoratives == null || decoratives.IsEmpty)
            {
                (deadPawns ??= []).Add(entry.Key);
            }
        }

        if (deadPawns == null)
        {
            return;
        }

        foreach (var pawn in deadPawns)
        {
            cache.Remove(pawn);
        }
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
        public List<ThingComp> weaponComps = [];

        public bool IsEmpty => apparelComps.Count == 0 && weaponComps.Count == 0;

        public void Add(ThingComp comp)
        {
            var list = comp.parent is Apparel ? apparelComps : weaponComps;
            if (!list.Contains(comp))
            {
                list.Add(comp);
            }
        }

        public void Remove(ThingComp comp)
        {
            if (comp.parent is Apparel)
            {
                apparelComps.Remove(comp);
            }
            else
            {
                weaponComps.Remove(comp);
            }
        }
    }
}
