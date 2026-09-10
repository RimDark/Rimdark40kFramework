using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class GameComponent_PersistentQuests : GameComponent
{
    private const int PollIntervalTicks = 2500;
    private const int TechLevelCacheTicks = 2500;

    private Dictionary<string, PersistentQuestState> states = new();

    private List<string> stateKeysWorking;
    private List<PersistentQuestState> stateValuesWorking;

    private TechLevel cachedTechLevel = TechLevel.Undefined;
    private int techLevelCacheTick = -1;

    public GameComponent_PersistentQuests(Game game)
    {
    }

    public static GameComponent_PersistentQuests Instance => Current.Game?.GetComponent<GameComponent_PersistentQuests>();

    public Dictionary<string, PersistentQuestState> States => states;

    /// <summary>
    /// Highest tech level the colony has actually reached: the player faction's baseline, raised
    /// by any finished research above it. Cached because it walks every research project.
    /// </summary>
    public TechLevel PlayerTechLevel
    {
        get
        {
            var ticksGame = Find.TickManager.TicksGame;
            if (techLevelCacheTick >= 0 && ticksGame >= techLevelCacheTick && ticksGame - techLevelCacheTick < TechLevelCacheTicks)
            {
                return cachedTechLevel;
            }

            var best = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Undefined;
            var projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project.techLevel > best && project.IsFinished)
                {
                    best = project.techLevel;
                }
            }

            cachedTechLevel = best;
            techLevelCacheTick = ticksGame;
            return best;
        }
    }

    /// <summary>
    /// Every persistent quest the active storyteller currently carries.
    /// </summary>
    public static IEnumerable<StorytellerCompProperties_PersistentQuest> RegisteredQuests()
    {
        var comps = Find.Storyteller?.storytellerComps;
        if (comps == null)
        {
            yield break;
        }

        for (var i = 0; i < comps.Count; i++)
        {
            if (comps[i].props is StorytellerCompProperties_PersistentQuest questProps && questProps.incident != null)
            {
                yield return questProps;
            }
        }
    }

    public PersistentQuestState StateFor(string incidentDefName)
    {
        states ??= new Dictionary<string, PersistentQuestState>();

        if (states.TryGetValue(incidentDefName, out var state))
        {
            return state;
        }

        state = new PersistentQuestState();
        states[incidentDefName] = state;
        return state;
    }

    public bool QuestActiveFor(IncidentDef incident)
    {
        return FindActiveQuest(incident?.questScriptDef) != null;
    }

    public void Notify_FireAttempted(string incidentDefName, FloatRange failedFireRetryDaysRange)
    {
        StateFor(incidentDefName).nextEarliestFireTick = Find.TickManager.TicksGame + DaysToTicks(failedFireRetryDaysRange);
    }

    /// <summary>
    /// Latches a quest as resolved so it is never offered again. Used by quests whose success is
    /// not expressible as a vanilla quest outcome.
    /// </summary>
    public static void MarkCompleted(string incidentDefName)
    {
        if (incidentDefName.NullOrEmpty())
        {
            return;
        }

        var instance = Instance;
        if (instance == null)
        {
            return;
        }

        var state = instance.StateFor(incidentDefName);
        state.completed = true;
        state.trackedQuestId = -1;
    }

    public static bool IsCompleted(string incidentDefName)
    {
        var instance = Instance;
        return instance != null && !incidentDefName.NullOrEmpty() && instance.StateFor(incidentDefName).completed;
    }

    public static void ResetQuest(string incidentDefName)
    {
        var instance = Instance;
        if (instance == null || incidentDefName.NullOrEmpty())
        {
            return;
        }

        instance.states?.Remove(incidentDefName);
    }

    /// <summary>
    /// Writes state carried over from a mod's own pre-framework tracking. Ignored once the quest
    /// has state of its own, so a migration can never clobber a live game.
    /// </summary>
    public static void SeedState(string incidentDefName, bool completed, int nextEarliestFireTick, int trackedQuestId, int occurrences)
    {
        var instance = Instance;
        if (instance == null || incidentDefName.NullOrEmpty())
        {
            return;
        }

        var state = instance.StateFor(incidentDefName);
        if (!state.IsUntouched)
        {
            return;
        }

        state.completed = completed;
        state.nextEarliestFireTick = nextEarliestFireTick;
        state.trackedQuestId = trackedQuestId;
        state.occurrences = occurrences;
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();

        if (Find.TickManager.TicksGame % PollIntervalTicks != 0)
        {
            return;
        }

        foreach (var questProps in RegisteredQuests())
        {
            UpdateState(questProps);
        }
    }

    private void UpdateState(StorytellerCompProperties_PersistentQuest questProps)
    {
        var state = StateFor(questProps.incident.defName);
        if (state.completed)
        {
            return;
        }

        var ticksGame = Find.TickManager.TicksGame;
        var quest = FindActiveQuest(questProps.incident.questScriptDef);

        if (quest != null)
        {
            if (state.trackedQuestId != quest.id)
            {
                state.trackedQuestId = quest.id;
                state.occurrences++;
                state.nextEarliestFireTick = ticksGame + DaysToTicks(questProps.retryDelayDaysRange);
            }

            return;
        }

        if (state.trackedQuestId < 0)
        {
            return;
        }

        if (questProps.completionMode == QuestCompletionMode.QuestSuccess && EndedSuccessfully(state.trackedQuestId))
        {
            state.completed = true;
        }

        state.trackedQuestId = -1;
        state.nextEarliestFireTick = ticksGame + DaysToTicks(questProps.retryDelayDaysRange);
    }

    private static Quest FindActiveQuest(QuestScriptDef script)
    {
        if (script == null)
        {
            return null;
        }

        var quests = Find.QuestManager?.QuestsListForReading;
        if (quests == null)
        {
            return null;
        }

        for (var i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest.root == script && quest.State is QuestState.NotYetAccepted or QuestState.Ongoing)
            {
                return quest;
            }
        }

        return null;
    }

    private static bool EndedSuccessfully(int questId)
    {
        var quests = Find.QuestManager?.QuestsListForReading;
        if (quests == null)
        {
            return false;
        }

        for (var i = 0; i < quests.Count; i++)
        {
            if (quests[i].id == questId)
            {
                return quests[i].State == QuestState.EndedSuccess;
            }
        }

        return false;
    }

    private static int DaysToTicks(FloatRange daysRange)
    {
        var days = Mathf.Max(0f, daysRange.RandomInRange);
        return Mathf.RoundToInt(days * GenDate.TicksPerDay);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref states, "persistentQuestStates", LookMode.Value, LookMode.Deep, ref stateKeysWorking, ref stateValuesWorking);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            states ??= new Dictionary<string, PersistentQuestState>();
        }
    }
}
