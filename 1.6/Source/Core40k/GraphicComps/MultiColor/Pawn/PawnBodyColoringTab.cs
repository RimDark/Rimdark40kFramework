using UnityEngine;
using Verse;

namespace Core40k;

public class PawnBodyColoringTab : ColoringBaseTab
{
    protected override void SetupHook(Pawn pawn)
    {
        var multiColor = pawn?.GetComp<CompMultiColorPawn>();
        if (multiColor != null)
        {
            multiColorComps.Add(multiColor);
        }
    }

    protected override Graphic MaskPreviewGraphic(CompMultiColor multiColor, MaskDef mask, Pawn pawn)
    {
        var pawnMultiColor = (CompMultiColorPawn)multiColor;
        return pawnMultiColor.BuildBodyGraphic(CompMultiColorPawn.VanillaBodyGraphic(pawnMultiColor.ParentPawn), mask);
    }

    protected override (Color col1, Color col2, Color col3) DefaultColorsFor(CompMultiColor multiColor)
    {
        return ((CompMultiColorPawn)multiColor).DefaultColors();
    }
}
