using HarmonyLib;
using Verse;

namespace Core40k;

/// <summary>
/// Paints the body of a non-humanlike pawn that carries CompMultiColorPawn. PawnRenderNode_AnimalPart is the body
/// node for animals and mechs; GraphicFor runs when the render tree is (re)built, not every frame.
/// Rotting and dessicated bodies keep the vanilla graphic.
/// </summary>
[HarmonyPatch(typeof(PawnRenderNode_AnimalPart), nameof(PawnRenderNode_AnimalPart.GraphicFor))]
public static class PawnBodyMultiColorPatch
{
    public static void Postfix(Pawn pawn, ref Graphic __result)
    {
        if (__result == null || pawn == null || !Core40kUtils.DefHasComp<CompMultiColorPawn>(pawn.def))
        {
            return;
        }

        if (pawn.Drawer.renderer.CurRotDrawMode != RotDrawMode.Fresh)
        {
            return;
        }

        var multiColor = pawn.GetComp<CompMultiColorPawn>();
        if (multiColor == null || !multiColor.InitialSet)
        {
            return;
        }

        __result = multiColor.BodyGraphicFor(__result) ?? __result;
    }
}
