using UnityEngine;
using Verse;

namespace Core40k;

//Not used by the framework. Kept because RimDark 40k - MorePowerArmor (3567575968) was built against it for its
//outfit stand patch, and fails to load without it. Same layout as vanilla's private Building_OutfitStand.CachedGraphicRenderInfo.
public struct CachedGraphicRenderInfo
{
    public Graphic graphic;

    public int layer;

    public Vector3 scale;

    public Vector3 positionOffset;

    public CachedGraphicRenderInfo(Graphic graphic, int layer, Vector3 scale, Vector3 positionOffset)
    {
        this.graphic = graphic;
        this.layer = layer;
        this.scale = scale;
        this.positionOffset = positionOffset;
    }
}
