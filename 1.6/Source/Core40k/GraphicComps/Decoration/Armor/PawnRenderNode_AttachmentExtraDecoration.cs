using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class PawnRenderNode_AttachmentExtraDecoration : PawnRenderNode
{
    public CompDecorativeBase decorativeComp;
    public DecorationDef decorationDef;

    private List<PawnRenderNode> hostApparelNodes;
    private bool hostApparelNodesResolved;
    
    public PawnRenderNode_AttachmentExtraDecoration(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
    }

    /// <summary>
    /// Render nodes drawing the apparel this decoration sits on. Resolved lazily because sibling nodes are only attached once every dynamic setup has run.
    /// </summary>
    public List<PawnRenderNode> HostApparelNodes
    {
        get
        {
            if (hostApparelNodesResolved)
            {
                return hostApparelNodes;
            }
            hostApparelNodesResolved = true;

            var host = decorativeComp?.parent;
            if (host == null)
            {
                return null;
            }

            CollectHostNodes(parent, host);
            CollectHostNodes(TagNode(PawnRenderNodeTagDefOf.ApparelHead), host);
            CollectHostNodes(TagNode(PawnRenderNodeTagDefOf.ApparelBody), host);
            CollectHostNodes(TagNode(PawnRenderNodeTagDefOf.Head), host);
            CollectHostNodes(TagNode(PawnRenderNodeTagDefOf.Body), host);

            return hostApparelNodes;
        }
    }

    private PawnRenderNode TagNode(PawnRenderNodeTagDef tag)
    {
        return tree != null && tree.TryGetNodeByTag(tag, out var node) ? node : null;
    }

    private void CollectHostNodes(PawnRenderNode searchIn, Thing host)
    {
        if (searchIn?.children == null)
        {
            return;
        }

        foreach (var child in searchIn.children)
        {
            if (child == null || child.apparel != host)
            {
                continue;
            }

            hostApparelNodes ??= [];
            if (!hostApparelNodes.Contains(child))
            {
                hostApparelNodes.Add(child);
            }
        }
    }

    public override Graphic GraphicFor(Pawn pawn)
    {
        var propsMulti = (PawnRenderNodePropertiesMultiColor)Props;

        var texPath = propsMulti.texPath;
        var shader = propsMulti.shaderTypeDef;
        var maskPath = propsMulti.maskDef?.maskPath ?? string.Empty;
        
        var bodyType = propsMulti.bodyType ?? pawn?.story?.bodyType;
        var useBodyType = propsMulti.useBodyType && bodyType != null;
        var gender = pawn?.gender ?? Gender.None;

        if (useBodyType)
        {
            texPath = BodyTypeUtils.BodyTypedPath(texPath, bodyType, gender);
        }

        if (maskPath != string.Empty)
        {
            if (useBodyType)
            {
                maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType, gender) ?? maskPath;
            }
        }
        else
        {
            maskPath = propsMulti.texPath;
            if (useBodyType)
            {
                maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType, gender) ?? maskPath;
            }
            maskPath += "_mask";
        }
        
        return MultiColorUtils.GetGraphic<Graphic_Multi>(texPath, shader.Shader, propsMulti.drawSize, propsMulti.color ?? Color.white, propsMulti.colorTwo ?? Color.white, propsMulti.colorThree ?? Color.white, null, maskPath);

    }
}