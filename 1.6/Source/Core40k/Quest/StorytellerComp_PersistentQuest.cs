using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class StorytellerComp_PersistentQuest : StorytellerComp
{
    private StorytellerCompProperties_PersistentQuest Props => (StorytellerCompProperties_PersistentQuest)props;

    public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
    {
        var incident = Props.incident;
        if (incident == null)
        {
            yield break;
        }

        var registry = GameComponent_PersistentQuests.Instance;
        if (registry == null)
        {
            yield break;
        }

        var state = registry.StateFor(incident.defName);
        if (state.completed)
        {
            yield break;
        }

        if (Props.maxOccurrences > 0 && state.occurrences >= Props.maxOccurrences)
        {
            yield break;
        }

        var ticksGame = Find.TickManager.TicksGame;
        if (state.nextEarliestFireTick > 0 && ticksGame < state.nextEarliestFireTick)
        {
            yield break;
        }

        if (registry.QuestActiveFor(incident))
        {
            yield break;
        }

        var homeMap = Find.AnyPlayerHomeMap;
        if (Props.requireHomeMap && homeMap == null)
        {
            yield break;
        }

        if (Props.skipIfOnExtremeBiome && (homeMap == null || homeMap.Biome.isExtremeBiome))
        {
            yield break;
        }

        if (!Props.requiredResearch.NullOrEmpty())
        {
            foreach (var project in Props.requiredResearch)
            {
                if (project != null && !project.IsFinished)
                {
                    yield break;
                }
            }
        }

        if (Props.minTechLevel != TechLevel.Undefined && registry.PlayerTechLevel < Props.minTechLevel)
        {
            yield break;
        }

        if (!incident.TargetAllowed(target))
        {
            yield break;
        }

        registry.Notify_FireAttempted(incident.defName, Props.failedFireRetryDaysRange);

        yield return new FiringIncident(incident, this, GenerateParms(incident.category, target));
    }
}
