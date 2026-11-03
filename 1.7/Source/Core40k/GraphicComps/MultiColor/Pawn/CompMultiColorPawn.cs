using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

[StaticConstructorOnStartup]
public class CompMultiColorPawn : CompMultiColor
{
    private static readonly Texture2D PaintIcon = ContentFinder<Texture2D>.Get("UI/Misc/PaintableIcon");

    public CompProperties_MultiColorPawn PawnProps => (CompProperties_MultiColorPawn)props;

    public Pawn ParentPawn => parent as Pawn;

    //Vanilla body graphic -> painted copy. GraphicFor can run on the parallel render threads, hence the lock.
    private readonly Dictionary<Graphic, Graphic> bodyGraphics = new();

    /// <summary>
    /// The body graphic vanilla would use for this pawn right now (life stage, alternate graphic, female variant),
    /// before any colouring.
    /// </summary>
    public static Graphic VanillaBodyGraphic(Pawn pawn)
    {
        var lifeStage = pawn?.ageTracker?.CurKindLifeStage;
        if (lifeStage?.bodyGraphicData == null)
        {
            return null;
        }

        if (pawn.TryGetAlternate(out var alternate, out _))
        {
            return alternate.GetGraphic(lifeStage.bodyGraphicData.Graphic);
        }

        return pawn.gender == Gender.Female && lifeStage.femaleGraphicData != null
            ? lifeStage.femaleGraphicData.Graphic
            : lifeStage.bodyGraphicData.Graphic;
    }

    /// <summary>
    /// The painted version of a vanilla body graphic, using the current colours and mask.
    /// </summary>
    public Graphic BodyGraphicFor(Graphic source)
    {
        if (source == null)
        {
            return null;
        }

        lock (bodyGraphics)
        {
            if (!bodyGraphics.TryGetValue(source, out var graphic))
            {
                graphic = BuildBodyGraphic(source, MaskDef);
                bodyGraphics.Add(source, graphic);
            }

            return graphic;
        }
    }

    /// <summary>
    /// Builds a painted body graphic for the given mask. A mask without a path (the default mask) keeps the
    /// bodyGraphicData's own maskPath.
    /// </summary>
    public Graphic BuildBodyGraphic(Graphic source, MaskDef mask)
    {
        if (source == null)
        {
            return null;
        }

        var maskPath = mask?.maskPath ?? source.maskPath;
        return MultiColorUtils.GetGraphic<Graphic_Multi>(source.path, Core40kDefOf.BEWH_CutoutThreeColor.Shader, source.drawSize, DrawColor, DrawColorTwo, DrawColorThree, source.data, maskPath);
    }

    public (Color one, Color two, Color three) DefaultColors()
    {
        var one = Props.defaultPrimaryColor
                  ?? (PawnProps.useAllegianceColorAsDefault && parent.Faction != null && ModsConfig.BiotechActive
                      ? parent.Faction.AllegianceColor
                      : Color.white);
        return (one, Props.defaultSecondaryColor ?? Color.white, Props.defaultTertiaryColor ?? Color.white);
    }

    public override void InitialColors()
    {
        var (one, two, three) = DefaultColors();
        SetColors(one, two, three);
    }

    public override void Notify_GraphicChanged()
    {
        lock (bodyGraphics)
        {
            bodyGraphics.Clear();
        }

        base.Notify_GraphicChanged();

        //Not while loading: the render tree is built fresh once the pawn spawns.
        if (Scribe.mode == LoadSaveMode.Inactive && ParentPawn is { SpawnedOrAnyParentSpawned: true } pawn)
        {
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        var pawn = ParentPawn;
        if (!PawnProps.showGizmo || pawn == null || pawn.Dead || pawn.Faction != Faction.OfPlayer)
        {
            yield break;
        }

        yield return new Command_Action
        {
            defaultLabel = "BEWH.Framework.Customization.PaintPawn".Translate(),
            defaultDesc = "BEWH.Framework.Customization.PaintPawnDesc".Translate(pawn.Named("PAWN")),
            icon = PaintIcon,
            action = () => Find.WindowStack.Add(new Dialog_CustomizePawnBody(pawn)),
        };
    }
}
