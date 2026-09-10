using Verse;

namespace Core40k;

public class PersistentQuestState : IExposable
{
    public int nextEarliestFireTick = -1;
    public int trackedQuestId = -1;
    public int occurrences;
    public bool completed;

    public bool IsUntouched => !completed && occurrences == 0 && trackedQuestId < 0 && nextEarliestFireTick < 0;

    public void ExposeData()
    {
        Scribe_Values.Look(ref nextEarliestFireTick, "nextEarliestFireTick", -1);
        Scribe_Values.Look(ref trackedQuestId, "trackedQuestId", -1);
        Scribe_Values.Look(ref occurrences, "occurrences");
        Scribe_Values.Look(ref completed, "completed");
    }
}
