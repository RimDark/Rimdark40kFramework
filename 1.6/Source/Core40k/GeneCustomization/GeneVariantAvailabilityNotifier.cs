using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Periodic sweep over free colonists: strips gene variant choices that lost their requirements
/// and, when the setting is on, announces variants that have just become selectable.
/// </summary>
public static class GeneVariantAvailabilityNotifier
{
    private const int SweepIntervalTicks = 1250;

    private const int SweepSteps = 25;

    private const int StepIntervalTicks = SweepIntervalTicks / SweepSteps;

    private static int sweepIndex;

    private static readonly List<Pawn> sweepPawns = [];

    public static void Tick()
    {
        if (!ModsConfig.BiotechActive)
        {
            return;
        }

        if (Find.TickManager.TicksGame % StepIntervalTicks != 0)
        {
            return;
        }

        if (sweepIndex <= 0 || sweepIndex >= sweepPawns.Count)
        {
            sweepPawns.Clear();
            sweepPawns.AddRange(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep);
            sweepIndex = 0;
            if (sweepPawns.Count == 0)
            {
                return;
            }
        }

        var pawnsThisStep = (sweepPawns.Count + SweepSteps - 1) / SweepSteps;

        for (var i = 0; i < pawnsThisStep && sweepIndex < sweepPawns.Count; i++)
        {
            var pawn = sweepPawns[sweepIndex++];
            if (pawn == null || pawn.Dead || pawn.Destroyed)
            {
                continue;
            }

            CheckPawn(pawn, Core40kUtils.ModSettings.notifyOnGeneVariantAvailability);
        }
    }

    public static void SeedBaseline()
    {
        if (!ModsConfig.BiotechActive)
        {
            return;
        }

        foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
        {
            CheckPawn(pawn, false);
        }
    }

    private static void CheckPawn(Pawn pawn, bool announce)
    {
        if (pawn?.genes == null || pawn.Faction is not { IsPlayer: true })
        {
            return;
        }

        var comp = pawn.GetComp<CompGeneCustomization>();
        if (comp == null)
        {
            return;
        }

        if (!comp.IsEmpty)
        {
            comp.Reconcile();
        }

        List<GeneVariantDef> newlyAvailable = null;

        foreach (var geneDef in comp.CustomizableGenes())
        {
            var extension = geneDef.GetModExtension<DefModExtension_CustomizableGene>();
            if (extension == null || !extension.MeetsRequirements(pawn))
            {
                continue;
            }

            foreach (var variant in GeneVariantIndex.VariantsFor(geneDef))
            {
                if (comp.HasAnnounced(variant) || !variant.MeetsRequirements(pawn))
                {
                    continue;
                }

                comp.MarkAnnounced(variant);

                newlyAvailable ??= [];
                newlyAvailable.Add(variant);
            }
        }

        if (!announce || newlyAvailable.NullOrEmpty())
        {
            return;
        }

        var variantList = newlyAvailable
            .Select(variant => variant.label.CapitalizeFirst())
            .ToCommaList(useAnd: true);

        var text = newlyAvailable.Count == 1
            ? "BEWH.Framework.GeneCustomization.VariantAvailable".Translate(pawn.LabelShortCap, variantList)
            : "BEWH.Framework.GeneCustomization.VariantsAvailable".Translate(pawn.LabelShortCap, variantList);

        Messages.Message(text.Resolve(), new LookTargets(pawn), MessageTypeDefOf.PositiveEvent);
    }
}
