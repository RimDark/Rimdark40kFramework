using HarmonyLib;
using Verse;

namespace Core40k;

/// <summary>
/// Body-rescaling mods - Varied Body Sizes (Mlie.VariedBodySizes) and anything with the same shape -
/// postfix PawnRenderTree.TrySetupGraphIfNeeded and swap PawnRenderNode.primaryGraphic for a copy at a
/// new draw size, rebuilt through the GraphicDatabase.Get overload that takes no shaderParameters.
/// A Graphic does not carry those parameters, so they cannot know to preserve ours: the rebuilt
/// graphic keeps path, mask and colours one and two, but loses _DrawColor/_DrawColorTwo/
/// _DrawColorThree, and masked apparel then renders with the shader's defaults.
///
/// We run last on the same method and rebuild anything they replaced, keeping their draw size and
/// restoring our shader parameters. With no such mod present every node exits on the first check.
/// </summary>
[HarmonyPatch(typeof(PawnRenderTree), "TrySetupGraphIfNeeded")]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter("Mlie.VariedBodySizes")]
public static class RestoreMultiColorAfterRescalePatch
{
    private static readonly AccessTools.FieldRef<PawnRenderNode, Graphic> PrimaryGraphicRef =
        AccessTools.FieldRefAccess<PawnRenderNode, Graphic>("primaryGraphic");

    public static void Postfix(PawnRenderTree __instance)
    {
        if (__instance?.rootNode == null)
        {
            return;
        }

        RestoreNode(__instance.rootNode);
    }

    private static void RestoreNode(PawnRenderNode node)
    {
        if (node == null)
        {
            return;
        }

        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                RestoreNode(child);
            }
        }

        var apparel = node.apparel;
        //A painted pawn body (CompMultiColorPawn) gets the same treatment as painted apparel.
        var bodyMultiColor = apparel == null && node is PawnRenderNode_AnimalPart && Core40kUtils.DefHasComp<CompMultiColorPawn>(node.tree?.pawn?.def)
            ? node.tree.pawn.GetComp<CompMultiColorPawn>()
            : null;
        if (apparel == null && bodyMultiColor == null)
        {
            return;
        }

        var current = PrimaryGraphicRef(node);
        
        if (current == null || MultiColorUtils.IsOwnGraphic(current))
        {
            return;
        }

        //Rotting and dessicated bodies stay vanilla.
        if (bodyMultiColor != null && current.Shader != Core40kDefOf.BEWH_CutoutThreeColor.Shader)
        {
            return;
        }

        var multiColor = bodyMultiColor ?? apparel.GetComp<CompMultiColor>();
        var alternateTexture = apparel?.GetComp<CompAlternateTexture>();
        if (multiColor == null && alternateTexture == null)
        {
            return;
        }

        //Colours one and two come off the graphic in front of us, not off the comp. A node is free
        //to build its own graphic with colours of its own - the shoulder and rank icons in Mankind's
        //Finest do - and the comp only knows the apparel's colours, so reading them from there
        //repaints those nodes in the armour's colours. A rescaling mod preserves path, mask, colour
        //and colourTwo and only drops the shader parameters, so the graphic itself is the better
        //source of truth. Colour three has no field on Graphic and is genuinely lost, so that one
        //falls back to the comp; nodes not using a three-colour shader ignore it anyway.
        var rebuilt = MultiColorUtils.GetGraphic<Graphic_Multi>(
            current.path,
            current.Shader,
            current.drawSize,
            current.color,
            current.colorTwo,
            multiColor?.DrawColorThree ?? current.colorTwo,
            null,
            current.maskPath);

        if (rebuilt != null && rebuilt != current)
        {
            PrimaryGraphicRef(node) = rebuilt;
        }
    }
}
