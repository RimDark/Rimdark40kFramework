using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Shared rank / gene / trait / hediff / research requirement checks, used by every def that gates
/// content on a pawn. A null list means "no requirement of that kind".
/// </summary>
public static class RequirementUtility
{
    public static bool MeetsRequirements(Pawn pawn, List<RankDef> ranks, List<GeneDef> genes, List<TraitData> traits, List<HediffDef> hediffs, List<ResearchProjectDef> research = null)
    {
        if (pawn == null)
        {
            return ranks == null && genes == null && traits == null && hediffs == null && research == null;
        }

        if (ranks != null)
        {
            var comp = pawn.GetComp<CompRankInfo>();
            if (comp == null)
            {
                return false;
            }
            foreach (var rank in ranks)
            {
                if (!comp.HasRank(rank))
                {
                    return false;
                }
            }
        }

        if (genes != null)
        {
            if (pawn.genes == null)
            {
                return false;
            }
            foreach (var gene in genes)
            {
                if (!pawn.genes.HasActiveGene(gene))
                {
                    return false;
                }
            }
        }

        if (traits != null)
        {
            if (pawn.story?.traits == null)
            {
                return false;
            }
            foreach (var trait in traits)
            {
                if (!pawn.story.traits.HasTrait(trait.traitDef, trait.degree))
                {
                    return false;
                }
            }
        }

        if (hediffs != null)
        {
            if (pawn.health?.hediffSet == null)
            {
                return false;
            }
            foreach (var hediff in hediffs)
            {
                if (!pawn.health.hediffSet.HasHediff(hediff))
                {
                    return false;
                }
            }
        }

        if (research != null)
        {
            foreach (var project in research)
            {
                if (project != null && !project.IsFinished)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool HasRequirements(Pawn pawn, List<RankDef> ranks, List<GeneDef> genes, List<TraitData> traits, List<HediffDef> hediffs, List<ResearchProjectDef> research, out string lockedReason)
    {
        var reason = new StringBuilder();
        var requirementFulfilled = true;

        //Nothing to check against. Anything with a requirement is locked, anything without is free.
        if (pawn == null)
        {
            lockedReason = string.Empty;
            return ranks == null && genes == null && traits == null && hediffs == null && research == null;
        }

        if (ranks != null)
        {
            var comp = pawn.GetComp<CompRankInfo>();
            if (comp == null)
            {
                reason.AppendLine("BEWH.Framework.Customization.MissingRanks".Translate());
                foreach (var rank in ranks)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(rank.label.CapitalizeFirst()));
                }
                lockedReason = reason.ToString();
                return false;
            }
            var missingRanks = (from rank in ranks where !comp.HasRank(rank) select rank.label.CapitalizeFirst()).ToList();
            if (missingRanks.Count > 0)
            {
                requirementFulfilled = false;
                reason.AppendLine("BEWH.Framework.Customization.MissingRanks".Translate());
                foreach (var rank in missingRanks)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(rank));
                }
            }
        }

        if (genes != null)
        {
            if (pawn.genes == null)
            {
                reason.AppendLine("BEWH.Framework.Customization.MissingGenes".Translate());
                foreach (var gene in genes)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(gene.label.CapitalizeFirst()));
                }
                lockedReason = reason.ToString();
                return false;
            }

            var missingGenes = (from gene in genes where !pawn.genes.HasActiveGene(gene) select gene.label.CapitalizeFirst()).ToList();
            if (missingGenes.Count > 0)
            {
                requirementFulfilled = false;
                reason.AppendLine("BEWH.Framework.Customization.MissingGenes".Translate());
                foreach (var gene in missingGenes)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(gene));
                }
            }
        }

        if (traits != null)
        {
            if (pawn.story?.traits == null)
            {
                reason.AppendLine("BEWH.Framework.Customization.MissingTraits".Translate());
                foreach (var trait in traits)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(trait.traitDef.label.CapitalizeFirst()));
                }
                lockedReason = reason.ToString();
                return false;
            }

            var missingTraits = (from trait in traits where !pawn.story.traits.HasTrait(trait.traitDef, trait.degree) select trait.traitDef.label.CapitalizeFirst()).ToList();
            if (missingTraits.Count > 0)
            {
                requirementFulfilled = false;
                reason.AppendLine("BEWH.Framework.Customization.MissingTraits".Translate());
                foreach (var trait in missingTraits)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(trait));
                }
            }
        }

        if (hediffs != null)
        {
            if (pawn.health?.hediffSet == null)
            {
                reason.AppendLine("BEWH.Framework.Customization.MissingHediffs".Translate());
                foreach (var hediff in hediffs)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(hediff.label.CapitalizeFirst()));
                }
                lockedReason = reason.ToString();
                return false;
            }

            var missingHediffs = (from hediff in hediffs where !pawn.health.hediffSet.HasHediff(hediff) select hediff.label.CapitalizeFirst()).ToList();
            if (missingHediffs.Count > 0)
            {
                requirementFulfilled = false;
                reason.AppendLine("BEWH.Framework.Customization.MissingHediffs".Translate());
                foreach (var hediff in missingHediffs)
                {
                    reason.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(hediff));
                }
            }
        }

        if (research != null)
        {
            var missingResearch = (from project in research where project != null && !project.IsFinished select project.label.CapitalizeFirst()).ToList();
            if (missingResearch.Count > 0)
            {
                requirementFulfilled = false;
                reason.AppendLine("BEWH.Framework.Common.MissingResearch".Translate(missingResearch.ToCommaList()));
            }
        }

        lockedReason = reason.ToString();
        return requirementFulfilled;
    }
}
