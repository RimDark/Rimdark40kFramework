using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Lets starting pawns generated from a pawn kind with its own xenotype set use that set instead of being forced to Baseliner.
/// Only takes effect in scenarios that include this part.
/// </summary>
public class ScenPart_StartingPawnKindXenotype : ScenPart
{
    public static bool ActiveInCurrentScenario => Find.Scenario?.AllParts.Any(part => part is ScenPart_StartingPawnKindXenotype) ?? false;
}

//Scenarios with a fixed pawn kind list fill the extra choice slots with the scenario's leftBehindPawnKind, but vanilla forces
//those pawns to Baseliner. Let a kind with its own xenotype set use it, the same way vanilla already does for the listed kinds.
[HarmonyPatch(typeof(StartingPawnUtility), "DefaultStartingPawnRequest", MethodType.Getter)]
public static class HarmonyPatch_StartingPawnKindXenotype
{
    public static void Postfix(ref PawnGenerationRequest __result)
    {
        if (!ModsConfig.BiotechActive || Find.GameInitData?.startingPawnKind == null)
        {
            return;
        }

        if (__result.ForcedXenotype != XenotypeDefOf.Baseliner || __result.KindDef?.xenotypeSet == null)
        {
            return;
        }

        if (ScenPart_StartingPawnKindXenotype.ActiveInCurrentScenario)
        {
            __result.ForcedXenotype = null;
        }
    }
}
