using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Core40k;

public class PawnRenderNode_FlagEdit : PawnRenderNode_Apparel
{
    private static Game cachedGameForCoreUtils;
    private static GameComponent_CoreUtils coreUtils;

    private static GameComponent_CoreUtils CoreUtils
    {
        get
        {
            if (coreUtils != null && cachedGameForCoreUtils == Current.Game)
            {
                return coreUtils;
            }

            cachedGameForCoreUtils = Current.Game;
            coreUtils = cachedGameForCoreUtils?.GetComponent<GameComponent_CoreUtils>();
            return coreUtils;
        }
    }
    
    public PawnRenderNode_FlagEdit(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
    }

    private string ModifyPathByFlags(List<TextureFlag> textureFlags, Pawn pawn)
    {
        if (textureFlags.NullOrEmpty())
        {
            return string.Empty;
        }

        var path = string.Empty;
        foreach (var flag in textureFlags)
        {
            var flagExpansion = ModifyByFlag(flag, pawn);
            if (flagExpansion == string.Empty)
            {
                continue;
            }

            if (flag.exclusive)
            {
                return flagExpansion;
            }

            path += flagExpansion;
        }
        return path;
    }

    protected virtual string ModifyByFlag(TextureFlag flag, Pawn pawn)
    {
        if (flag.pathExpansion == string.Empty)
        {
            return string.Empty;
        }

        if (flag.thingActivator != null && pawn?.apparel?.WornApparel != null
            && !WearsDef(pawn.apparel.WornApparel, flag.thingActivator)
            && pawn.equipment?.Primary?.def != flag.thingActivator)
        {
            return string.Empty;
        }

        if (flag.hediffActivator != null && pawn?.health?.hediffSet != null
            && !pawn.health.hediffSet.HasHediff(flag.hediffActivator))
        {
            return string.Empty;
        }

        if (flag.geneActivator != null && pawn?.genes != null
            && !pawn.genes.HasActiveGene(flag.geneActivator))
        {
            return string.Empty;
        }

        if (flag.gizmoActivated && !GizmoToggledOn(pawn))
        {
            return string.Empty;
        }

        return flag.pathExpansion;
    }

    private static bool WearsDef(List<Apparel> wornApparel, ThingDef def)
    {
        for (var i = 0; i < wornApparel.Count; i++)
        {
            if (wornApparel[i].def == def)
            {
                return true;
            }
        }

        return false;
    }

    private bool GizmoToggledOn(Pawn pawn)
    {
        if (pawn == null || apparel == null)
        {
            return false;
        }

        var coreUtils = CoreUtils;
        return coreUtils != null && coreUtils.cachedGizmoToggles.TryGetValue((pawn, apparel), out var toggledOn) && toggledOn;
    }

    public override Graphic GraphicFor(Pawn pawn)
    {
        var defMod = apparel?.def?.GetModExtension<DefModExtension_TextureFlags>();
        if (defMod == null)
        {
            return base.GraphicFor(pawn);
        }
        
        var modifiedPath = ModifyPathByFlags(defMod.SwapFlagsByOrder, pawn);
        
        var path = Props.texPath;
        
        if (Props.bodyTypeGraphicPaths != null)
        {
            foreach (var bodyTypeGraphicPath in Props.bodyTypeGraphicPaths)
            {
                if (pawn.story.bodyType != bodyTypeGraphicPath.bodyType)
                {
                    continue;
                }
                path = bodyTypeGraphicPath.texturePath;
                break;
            }
        }
        
        string maskPath = null;
        var multiColor = apparel.GetComp<CompMultiColor>();
        if (multiColor?.MaskDef != null)
        {
            if (defMod.ShouldExpandMaskPath(multiColor.MaskDef, Props.texSeed))
            {
                maskPath = multiColor.MaskDef.maskPath + defMod.GetExpansionPathByIdentifier(Props.texSeed);
            }
        }
        
        if (defMod.ShouldExpandBasePath(Props.texSeed))
        {
            path += modifiedPath;
            if (maskPath != null)
            {
                maskPath += modifiedPath;
            }
        }
        
        return MultiColorUtils.GetGraphic<Graphic_Multi>(path, Core40kDefOf.BEWH_CutoutThreeColor.Shader, Props.drawSize, 
            multiColor?.DrawColor ?? apparel.DrawColor, 
            multiColor?.DrawColorTwo ?? apparel.DrawColorTwo, 
            multiColor?.DrawColorThree ?? apparel.DrawColorTwo, apparel.def.graphicData, maskPath);
    }
    
    protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
    {
        var defMod = apparel.def.GetModExtension<DefModExtension_TextureFlags>();
        if (defMod == null)
        {
            yield return GraphicFor(pawn);
            yield break;
        }
        
        //TODO: build and yield a graphic per shouldAddInsteadOfSwap flag. The loop that used to
        //stand here iterated defMod.textureFlags on every resolve purely to do nothing.
        yield return GraphicFor(pawn);
    }
}