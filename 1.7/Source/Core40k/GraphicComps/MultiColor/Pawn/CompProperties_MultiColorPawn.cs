using Verse;

namespace Core40k;

/// <summary>
/// Three-colour body painting for non-humanlike pawns (mechs, animals, xenos creatures). Goes on the race ThingDef.
/// The body texture comes from the pawn kind's bodyGraphicData as usual; its maskPath (or a picked MaskDef) is the
/// three-channel mask.
/// </summary>
public class CompProperties_MultiColorPawn : CompProperties_MultiColor
{
    //Start new pawns in their faction's mech colour (Biotech) when no defaultPrimaryColor is set.
    public bool useAllegianceColorAsDefault = true;

    //Show the paint gizmo on pawns of the player's faction.
    public bool showGizmo = true;

    public CompProperties_MultiColorPawn()
    {
        compClass = typeof(CompMultiColorPawn);
    }
}
