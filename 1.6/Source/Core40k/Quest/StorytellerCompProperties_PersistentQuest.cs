using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class StorytellerCompProperties_PersistentQuest : StorytellerCompProperties
{
    public IncidentDef incident;

    public FloatRange retryDelayDaysRange = new FloatRange(25f, 40f);
    public FloatRange failedFireRetryDaysRange = new FloatRange(1f, 3f);

    public TechLevel minTechLevel = TechLevel.Undefined;
    public List<ResearchProjectDef> requiredResearch;

    public bool requireHomeMap = true;
    public bool skipIfOnExtremeBiome;

    public int maxOccurrences = -1;
    public QuestCompletionMode completionMode = QuestCompletionMode.QuestSuccess;

    public StorytellerCompProperties_PersistentQuest()
    {
        compClass = typeof(StorytellerComp_PersistentQuest);
    }

    public override IEnumerable<string> ConfigErrors(StorytellerDef parentDef)
    {
        foreach (var error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }

        if (incident == null)
        {
            yield return "StorytellerCompProperties_PersistentQuest has no incident set.";
        }
        else if (incident.questScriptDef == null)
        {
            yield return "StorytellerCompProperties_PersistentQuest incident " + incident.defName + " has no questScriptDef.";
        }

        if (retryDelayDaysRange.min < 0f || retryDelayDaysRange.max < retryDelayDaysRange.min)
        {
            yield return "StorytellerCompProperties_PersistentQuest has an invalid retryDelayDaysRange.";
        }

        if (failedFireRetryDaysRange.min < 0f || failedFireRetryDaysRange.max < failedFireRetryDaysRange.min)
        {
            yield return "StorytellerCompProperties_PersistentQuest has an invalid failedFireRetryDaysRange.";
        }

        if (maxOccurrences == 0)
        {
            yield return "StorytellerCompProperties_PersistentQuest has maxOccurrences 0, so it can never fire.";
        }
    }
}
