using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

/// <summary>
/// Renders framework armour that is not being worn: printed into the map mesh when it lies on the
/// ground or a shelf, drawn per frame while carried. By default the item shows its worn south
/// texture with its decorations (and optionally its backpack / shoulder pad render nodes) laid out
/// exactly as on a pawn, scaled from the 1.5 pawn mesh to CompProperties_Decorative.groundDrawSize.
/// With useIconOnGround the vanilla item icon is kept and decorations are mapped onto it
/// proportionally instead.
/// </summary>
public static class GroundDecorationRenderer
{
    private const float PawnMeshSize = 1.5f;
    private const float ApparelIconDrawMult = 0.9f;
    private const float BodyNodeLayer = 20f;
    private const float HeadNodeLayer = 70f;
    private const float AltitudePerLayer = 1f / 2600f;
    private const float MaxLayer = 99f;

    public readonly struct GroundDraw
    {
        public readonly Material material;
        public readonly Vector3 offset;
        public readonly Vector3 scale;
        public readonly float angle;
        public readonly float layer;
        public readonly bool flip;

        public GroundDraw(Material material, Vector3 offset, Vector3 scale, float angle, float layer, bool flip)
        {
            this.material = material;
            this.offset = offset;
            this.scale = scale;
            this.angle = angle;
            this.layer = layer;
            this.flip = flip;
        }
    }

    public class GroundCache
    {
        public readonly List<GroundDraw> draws = [];
        public float groundAngle;
        //True when the worn look is drawn, so the item's own icon graphic must not be.
        public bool replacesParent;
        //Extent along z of the quad everything else is layered over: the worn base or the icon.
        public float baseLength;
    }

    private static Core40kModSettings modSettings;
    private static Core40kModSettings ModSettings => modSettings ??= LoadedModManager.GetMod<Core40kMod>().GetSettings<Core40kModSettings>();

    public static bool Enabled => ModSettings.showDecorationsOnGround;

    private static bool loggedFailure;

    public static void Draw(CompDecorative comp, Vector3 drawLoc)
    {
        try
        {
            DrawInner(comp, drawLoc);
        }
        catch (Exception exception)
        {
            LogFailure(comp, exception);
        }
    }

    public static void Print(CompDecorative comp, SectionLayer layer)
    {
        try
        {
            PrintInner(comp, layer);
        }
        catch (Exception exception)
        {
            LogFailure(comp, exception);
        }
    }

    private static void LogFailure(CompDecorative comp, Exception exception)
    {
        if (loggedFailure)
        {
            return;
        }

        loggedFailure = true;
        Log.Error("[Core40k] Failed to render ground decorations on " + (comp?.parent?.def?.defName ?? "null") + ", further failures will not be logged: " + exception);
    }

    private static void DrawInner(CompDecorative comp, Vector3 drawLoc)
    {
        var cache = comp.GroundCache;
        if (cache == null || cache.draws.Count == 0)
        {
            return;
        }

        var groundRotation = Quaternion.AngleAxis(cache.groundAngle, Vector3.up);
        foreach (var draw in cache.draws)
        {
            var position = drawLoc + groundRotation * draw.offset;
            position.y += Math.Clamp(draw.layer, -MaxLayer, MaxLayer) * AltitudePerLayer;

            var rotation = draw.angle == 0f ? groundRotation : groundRotation * Quaternion.AngleAxis(draw.angle, Vector3.up);
            var mesh = draw.flip ? MeshPool.plane10Flip : MeshPool.plane10;
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, rotation, draw.scale), draw.material, 0);
        }
    }

    private static void PrintInner(CompDecorative comp, SectionLayer layer)
    {
        var cache = comp.GroundCache;
        if (cache == null || cache.draws.Count == 0)
        {
            return;
        }

        var groundRotation = Quaternion.AngleAxis(cache.groundAngle, Vector3.up);
        var sizeMult = comp.parent.MultipleItemsPerCellDrawn() ? 0.8f : 1f;
        var drawPos = comp.parent.DrawPos;
        var baseLength = cache.baseLength * sizeMult;

        foreach (var draw in cache.draws)
        {
            var size = new Vector2(draw.scale.x * sizeMult, draw.scale.z * sizeMult);
            PrintOverBase(layer, drawPos, baseLength, cache.groundAngle, groundRotation, draw.offset * sizeMult, size, draw.material, draw.angle, draw.flip, draw.layer);
        }
    }

    //Vanilla prints an item quad tilted, south edge at its altitude and north edge BaseAltitudeBias
    //higher, so map mesh items sort against each other. A quad printed over it has to follow the
    //same slope or it dips under the base wherever the two planes cross.
    private const float BaseAltitudeBias = 0.01f;

    /// <summary>
    /// Prints a quad parallel to the base item quad it sits on, a layer step above or below it.
    /// localOffset is in the item's unrotated frame; baseLength is the base quad's extent along z.
    /// </summary>
    public static void PrintOverBase(SectionLayer layer, Vector3 basePos, float baseLength, float groundAngle, Quaternion groundRotation, Vector3 localOffset, Vector2 size, Material material, float extraAngle, bool flip, float decorationLayer)
    {
        var slope = baseLength > 0f ? BaseAltitudeBias / baseLength : 0f;
        var southEdgeLocalZ = localOffset.z - size.y / 2f;

        var center = basePos + groundRotation * localOffset;
        center.y = basePos.y
                   + slope * (southEdgeLocalZ + baseLength / 2f)
                   + Math.Clamp(decorationLayer, -MaxLayer, MaxLayer) * AltitudePerLayer;

        Printer_Plane.PrintPlane(layer, center, size, material, groundAngle + extraAngle, flip, topVerticesAltitudeBias: slope * size.y);
    }

    public static GroundCache Build(CompDecorative comp)
    {
        var cache = new GroundCache
        {
            groundAngle = GroundRotationUtility.GroundAngleFor(comp.parent)
        };

        var props = comp.Props;
        if (props == null || comp.parent is not Apparel apparel)
        {
            return cache;
        }

        var apparelBodyType = apparel.def.GetModExtension<DefModExtension_ForcesBodyType>()?.forcedBodyType ?? BodyTypeDefOf.Male;
        var bodySizeFactor = (apparelBodyType.bodyGraphicScale.x + apparelBodyType.bodyGraphicScale.y) / 2f;

        Material baseMaterial = null;
        if (!props.useIconOnGround)
        {
            baseMaterial = WornBaseMaterial(apparel, apparelBodyType);
        }

        Vector3 offsetFactor;
        Vector3 sizeFactor;
        if (baseMaterial != null)
        {
            //Pawn space, uniformly scaled: the worn texture, the nodes and the decorations all keep
            //the positions they have on a pawn.
            cache.replacesParent = true;
            cache.baseLength = props.groundDrawSize;
            var factor = props.groundDrawSize / PawnMeshSize;
            offsetFactor = new Vector3(factor, 1f, factor);
            sizeFactor = new Vector3(props.groundDrawSize, 1f, props.groundDrawSize);

            cache.draws.Add(new GroundDraw(baseMaterial, Vector3.zero, sizeFactor, 0f, 0f, false));

            if (props.drawRenderNodesOnGround)
            {
                CollectNodes(cache, apparel, bodySizeFactor, offsetFactor, sizeFactor);
            }
        }
        else
        {
            //The icon is a different drawing from the worn texture, so this is only proportional.
            var iconDrawSize = comp.parent.Graphic?.drawSize ?? apparel.def.graphicData?.drawSize ?? Vector2.one;
            if (comp.parent.Graphic == null)
            {
                iconDrawSize *= ApparelIconDrawMult;
            }
            cache.baseLength = iconDrawSize.y;
            offsetFactor = new Vector3(iconDrawSize.x / PawnMeshSize, 1f, iconDrawSize.y / PawnMeshSize) * props.groundDecorationScale;
            sizeFactor = new Vector3(iconDrawSize.x, 1f, iconDrawSize.y) * props.groundDecorationScale;
        }

        if (props.drawDecorationsOnGround && comp.Decorations.Count > 0)
        {
            CollectDecorations(cache, comp, props, apparelBodyType, bodySizeFactor, offsetFactor, sizeFactor);
        }

        cache.draws.SortBy(draw => draw.layer);
        return cache;
    }

    /// <summary>
    /// The south face of the apparel as worn, in the item's colours. The framework's own builder is
    /// used directly for painted apparel so the comps' worn graphic caches are left alone.
    /// </summary>
    private static Material WornBaseMaterial(Apparel apparel, BodyTypeDef bodyType)
    {
        if (apparel.WornGraphicPath.NullOrEmpty())
        {
            return null;
        }

        var multiColor = apparel.GetComp<CompMultiColor>();
        var alternateTexture = apparel.GetComp<CompAlternateTexture>();

        Graphic graphic;
        if (multiColor != null || alternateTexture != null)
        {
            graphic = ApparelGraphicPatch.TryGetGraphicApparel(apparel, multiColor, alternateTexture, bodyType);
        }
        else
        {
            graphic = ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, bodyType, false, out var rec) ? rec.graphic : null;
        }

        return graphic?.MatAt(Rot4.South);
    }

    private static void CollectNodes(GroundCache cache, Apparel apparel, float bodySizeFactor, Vector3 offsetFactor, Vector3 sizeFactor)
    {
        var multiColor = apparel.GetComp<CompMultiColor>();
        if (multiColor == null)
        {
            return;
        }

        var nodeProperties = apparel.def.apparel?.RenderNodeProperties;
        if (nodeProperties.NullOrEmpty())
        {
            return;
        }

        var textureFlags = apparel.def.GetModExtension<DefModExtension_TextureFlags>();

        foreach (var props in nodeProperties)
        {
            //The shoulder pad worker hides the pad facing south.
            if (typeof(PawnRenderNodeWorker_AttachmentShoulderPad).IsAssignableFrom(props.workerClass))
            {
                continue;
            }
            if (!typeof(PawnRenderNodeWorker_AttachmentBackpack).IsAssignableFrom(props.workerClass))
            {
                continue;
            }

            if (props.texPath.NullOrEmpty())
            {
                continue;
            }

            var material = OutfitStandDrawProvider_MultiColorAttachments.BuildGraphic(apparel, multiColor, textureFlags, props)?.MatAt(Rot4.South);
            if (material == null)
            {
                continue;
            }

            var drawData = props.drawData;

            var offset = drawData?.OffsetForRot(Rot4.South) ?? Vector3.zero;
            if (drawData is { scaleOffsetByBodySize: true })
            {
                offset *= bodySizeFactor;
            }
            offset = Vector3.Scale(offset, offsetFactor);
            offset.y = 0f;

            var baseScale = drawData?.scale ?? 1f;
            var scale = new Vector3(props.drawSize.x * baseScale * sizeFactor.x, 1f, props.drawSize.y * baseScale * sizeFactor.z);

            var layer = (drawData?.LayerForRot(Rot4.South, props.baseLayer) ?? props.baseLayer) - BodyNodeLayer;

            var flip = drawData?.FlipForRot(Rot4.South) ?? false;
            var angle = drawData?.RotationOffsetForRot(Rot4.South) ?? 0f;
            if (flip)
            {
                angle *= -1f;
            }

            cache.draws.Add(new GroundDraw(material, offset, scale, angle, layer, flip));
        }
    }

    private static void CollectDecorations(GroundCache cache, CompDecorative comp, CompProperties_Decorative props, BodyTypeDef apparelBodyType, float bodySizeFactor, Vector3 offsetFactor, Vector3 sizeFactor)
    {
        var bodyComp = props.decorativeType == DecorativeType.Body;

        foreach (var pair in comp.Decorations)
        {
            if (pair.Key is not ExtraDecorationDef decoration || !decoration.HasVisual || !decoration.showOnGround)
            {
                continue;
            }

            var settings = pair.Value;
            if (settings == null)
            {
                continue;
            }

            //A hood or halo on a body armour has no head to sit on.
            if (bodyComp && decoration.drawInHeadSpace)
            {
                continue;
            }

            if (decoration.ShowRotation(settings.Flipped) is { } showRotation && !showRotation.Contains(Rot4.South))
            {
                continue;
            }

            var useBodyType = false;
            var graphicBodyType = apparelBodyType;
            if (bodyComp && BodyTypeUtils.MatchesAny(apparelBodyType, decoration.appliesToBodyTypes, out var matched))
            {
                useBodyType = true;
                graphicBodyType = matched ?? apparelBodyType;
            }

            var material = ArmorDecorationGraphicUtility.BuildGraphic(decoration, settings, useBodyType, graphicBodyType)?.MatAt(Rot4.South);
            if (material == null)
            {
                continue;
            }

            var drawData = decoration.drawData;
            var nodeLayer = bodyComp ? BodyNodeLayer : HeadNodeLayer;

            var offset = drawData?.OffsetForRot(Rot4.South) ?? Vector3.zero;
            if (drawData is { scaleOffsetByBodySize: true })
            {
                offset *= bodySizeFactor;
            }
            offset += comp.GetAdditionalOffsetForRot(Rot4.South, decoration);
            offset = Vector3.Scale(offset, offsetFactor);
            offset += props.groundDecorationOffset;
            offset.y = 0f;

            var baseScale = drawData?.scale ?? 1f;
            var extraScale = comp.GetAdditionalScaleForRot(Rot4.South, decoration);
            var scale = new Vector3(
                decoration.drawSize.x * baseScale * extraScale.x * sizeFactor.x,
                1f,
                decoration.drawSize.y * baseScale * extraScale.z * sizeFactor.z);

            var layer = (drawData?.LayerForRot(Rot4.South, nodeLayer) ?? nodeLayer) - nodeLayer
                        + comp.GetAdditionalLayerForRot(Rot4.South, decoration);

            var flip = settings.Flipped ^ (drawData?.FlipForRot(Rot4.South) ?? false);
            var angle = drawData?.RotationOffsetForRot(Rot4.South) ?? 0f;
            if (flip)
            {
                angle *= -1f;
            }

            cache.draws.Add(new GroundDraw(material, offset, scale, angle, layer, flip));

            if (decoration.HasGlow && settings.GlowEnabled)
            {
                var glowMaterial = ArmorDecorationGraphicUtility.BuildGlowGraphic(decoration, settings, useBodyType, graphicBodyType)?.MatAt(Rot4.South);
                if (glowMaterial != null)
                {
                    cache.draws.Add(new GroundDraw(glowMaterial, offset, scale, angle, layer + ArmorDecorationGraphicUtility.GlowLayerOffset, flip));
                }
            }
        }
    }
}

/// <summary>
/// The angle vanilla gives an item lying on the ground, so decorations can follow it.
/// </summary>
public static class GroundRotationUtility
{
    private static readonly AccessTools.FieldRef<Graphic_RandomRotated, float> MaxAngleRef =
        AccessTools.FieldRefAccess<Graphic_RandomRotated, float>("maxAngle");

    public static float GroundAngleFor(Thing thing)
    {
        if (thing == null)
        {
            return 0f;
        }

        var rackAngle = RotInRack(thing);
        if (rackAngle.HasValue)
        {
            return rackAngle.Value;
        }

        if (thing.Graphic is not Graphic_RandomRotated randomRotated)
        {
            return 0f;
        }

        var maxAngle = MaxAngleRef(randomRotated);
        return 0f - maxAngle + (float)(thing.thingIDNumber * 542) % (maxAngle * 2f);
    }

    /// <summary>Graphic_RandomRotated.GetRotInRack is private; this is the same rule.</summary>
    private static float? RotInRack(Thing thing)
    {
        if (!thing.def.IsWeapon || !thing.Spawned)
        {
            return null;
        }

        var position = thing.Position;
        var map = thing.Map;
        if (!position.InBounds(map) || position.GetEdifice(map) == null || position.GetItemCount(map) < 2)
        {
            return null;
        }

        return thing.def.rotateInShelves ? -90f : 0f;
    }
}
