using System;
using System.Collections.Generic;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace Core40k;

public static class DebugActions_PersistentQuest
{
    [DebugAction("RimDark", "Persistent quests: log state", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing, displayPriority = -900)]
    private static void LogPersistentQuestState()
    {
        var registry = GameComponent_PersistentQuests.Instance;
        if (registry == null)
        {
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("Persistent quests (player tech level: " + registry.PlayerTechLevel + ")");

        var any = false;
        foreach (var questProps in GameComponent_PersistentQuests.RegisteredQuests())
        {
            any = true;
            report.AppendLine(Describe(questProps, registry.StateFor(questProps.incident.defName)));
        }

        if (!any)
        {
            report.AppendLine("  none registered on the current storyteller");
        }

        Log.Message(report.ToString());
    }

    [DebugAction("RimDark", "Persistent quests: offer now", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing, displayPriority = -900)]
    private static void OfferPersistentQuestNow()
    {
        OpenQuestMenu("Clear retry delay", state => state.nextEarliestFireTick = -1);
    }

    [DebugAction("RimDark", "Persistent quests: mark completed", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing, displayPriority = -900)]
    private static void CompletePersistentQuest()
    {
        OpenQuestMenu("Mark completed", state =>
        {
            state.completed = true;
            state.trackedQuestId = -1;
        });
    }

    [DebugAction("RimDark", "Persistent quests: reset", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing, displayPriority = -900)]
    private static void ResetPersistentQuest()
    {
        OpenQuestMenu("Reset", state =>
        {
            state.completed = false;
            state.occurrences = 0;
            state.trackedQuestId = -1;
            state.nextEarliestFireTick = -1;
        });
    }

    private static void OpenQuestMenu(string header, Action<PersistentQuestState> action)
    {
        var registry = GameComponent_PersistentQuests.Instance;
        if (registry == null)
        {
            return;
        }

        var options = new List<DebugMenuOption>();
        foreach (var questProps in GameComponent_PersistentQuests.RegisteredQuests())
        {
            var incidentDefName = questProps.incident.defName;
            var label = Describe(questProps, registry.StateFor(incidentDefName));
            options.Add(new DebugMenuOption(label, DebugMenuOptionMode.Action, () => action(registry.StateFor(incidentDefName))));
        }

        if (options.Count == 0)
        {
            Messages.Message("No persistent quests registered on the current storyteller.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, header));
    }

    private static string Describe(StorytellerCompProperties_PersistentQuest questProps, PersistentQuestState state)
    {
        var description = new StringBuilder(questProps.incident.defName);
        description.Append(state.completed ? " | completed" : " | pending");
        description.Append(" | offers: ").Append(state.occurrences);

        if (questProps.maxOccurrences > 0)
        {
            description.Append('/').Append(questProps.maxOccurrences);
        }

        if (state.trackedQuestId >= 0)
        {
            description.Append(" | active quest ").Append(state.trackedQuestId);
        }

        if (!state.completed)
        {
            var remaining = state.nextEarliestFireTick - Find.TickManager.TicksGame;
            description.Append(" | next: ").Append(remaining > 0 ? remaining.ToStringTicksToPeriod() : "now");
        }

        return description.ToString();
    }
}
