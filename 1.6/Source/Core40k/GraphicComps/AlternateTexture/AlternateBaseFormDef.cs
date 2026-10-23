using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace Core40k;

public class AlternateBaseFormDef : DecorationDef
{
    public List<MaskDef> incompatibleMaskDefs = [];

    public Color? newPrimaryColor;
    public Color? newSecondaryColor;
    public Color? newTertiaryColor;

    public Vector2? newDrawSize;

    public List<RenderSkipFlagDef> renderSkipFlags;
}