using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

/// <summary>
/// Resolves the graphic for an armour decoration that is not being worn - on an outfit stand or on
/// the ground. The same resolution PawnRenderNode_AttachmentExtraDecoration.GraphicFor does, with no
/// wearer so no gendered art.
/// </summary>
public static class ArmorDecorationGraphicUtility
{
    //Keeps the lit part on top of the decoration it repeats, which has written the same depth.
    public const float GlowLayerOffset = 0.1f;

    public static Graphic BuildGraphic(ExtraDecorationDef decoration, DecorationSettings settings, bool useBodyType, BodyTypeDef bodyType)
    {
        var shader = settings.maskDef?.shaderType ?? decoration.shaderType;
        if (shader == null)
        {
            return null;
        }

        return BuildGraphic(decoration, settings, shader, settings.Color, settings.ColorTwo, settings.ColorThree, useBodyType, bodyType);
    }

    /// <summary>
    /// The lit part of a decoration: the same texture and mask through the glow shader, showing only its glowChannels.
    /// </summary>
    public static Graphic BuildGlowGraphic(ExtraDecorationDef decoration, DecorationSettings settings, bool useBodyType, BodyTypeDef bodyType)
    {
        return BuildGraphic(
            decoration,
            settings,
            Core40kDefOf.BEWH_GlowThreeColor,
            decoration.GlowColour(GlowChannel.Colour, settings.Color),
            decoration.GlowColour(GlowChannel.ColourTwo, settings.ColorTwo),
            decoration.GlowColour(GlowChannel.ColourThree, settings.ColorThree),
            useBodyType,
            bodyType);
    }

    private static Graphic BuildGraphic(ExtraDecorationDef decoration, DecorationSettings settings, ShaderTypeDef shader, Color colour, Color colourTwo, Color colourThree, bool useBodyType, BodyTypeDef bodyType)
    {
        var texPath = decoration.drawnTextureIconPath;
        var maskPath = settings.maskDef?.maskPath ?? string.Empty;

        if (useBodyType)
        {
            texPath = BodyTypeUtils.BodyTypedPath(texPath, bodyType);
        }

        if (maskPath != string.Empty)
        {
            if (useBodyType)
            {
                maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType) ?? maskPath;
            }
        }
        else
        {
            maskPath = decoration.drawnTextureIconPath;
            if (useBodyType)
            {
                maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType) ?? maskPath;
            }
            maskPath += "_mask";
        }

        return MultiColorUtils.GetGraphic<Graphic_Multi>(
            texPath,
            shader.Shader,
            decoration.drawSize,
            colour,
            colourTwo,
            colourThree,
            null,
            maskPath);
    }
}
