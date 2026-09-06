using System.Collections.Generic;
using Verse;

namespace Core40k;

public class DefModExtension_TextureFlags : DefModExtension
{
    public List<TextureFlag> textureFlags = [];
    
    public List<MaskExpansion> maskExpansions = [];

    [Unsaved]
    private List<TextureFlag> swapFlagsByOrder;

    /// <summary>
    /// The flags that swap the texture path, in order. Built once; the def is immutable after load.
    /// </summary>
    public List<TextureFlag> SwapFlagsByOrder
    {
        get
        {
            if (swapFlagsByOrder != null)
            {
                return swapFlagsByOrder;
            }

            var flags = new List<TextureFlag>();
            foreach (var textureFlag in textureFlags)
            {
                if (!textureFlag.shouldAddInsteadOfSwap)
                {
                    flags.Add(textureFlag);
                }
            }
            flags.SortStable((first, second) => first.order.CompareTo(second.order));
            swapFlagsByOrder = flags;
            return flags;
        }
    }

    [Unsaved]
    private List<TextureFlag> gizmoFlags;

    /// <summary>
    /// The flags toggled from a gizmo. Built once; the def is immutable after load.
    /// </summary>
    public List<TextureFlag> GizmoFlags
    {
        get
        {
            if (gizmoFlags != null)
            {
                return gizmoFlags;
            }

            var flags = new List<TextureFlag>();
            foreach (var textureFlag in textureFlags)
            {
                if (textureFlag.gizmoActivated)
                {
                    flags.Add(textureFlag);
                }
            }
            gizmoFlags = flags;
            return flags;
        }
    }

    public bool ShouldExpandMaskPath(MaskDef maskDef, int identifier)
    {
        foreach (var maskExpansion in maskExpansions)
        {
            if (maskExpansion.identifier == identifier && maskExpansion.maskDefsWithExpansion.Contains(maskDef))
            {
                return true;
            }
        }

        return false;
    }

    public bool ShouldExpandBasePath(int identifier)
    {
        foreach (var textureFlag in textureFlags)
        {
            if (textureFlag.maskIdentifiers.Contains(identifier))
            {
                return true;
            }
        }

        return false;
    }

    public string GetExpansionPathByIdentifier(int identifier)
    {
        foreach (var maskExpansion in maskExpansions)
        {
            if (maskExpansion.identifier == identifier)
            {
                return maskExpansion.pathExpansionOnMask;
            }
        }
        
        return string.Empty;
    }
}

public class TextureFlag
{
    public TextureFlag(){}
    
    public int order = 0;
    public List<int> maskIdentifiers = [];
    public string pathExpansion = string.Empty;
    
    public bool shouldAddInsteadOfSwap = false;
    public bool hideThing = false;
    public bool exclusive = false;

    [NoTranslate]
    public string hideTexPath = "Things/Armor/Imperium/PowerArmor/CommonIcons/BEWH_None";
    
    public ThingDef thingActivator = null;
    public HediffDef hediffActivator = null;
    public GeneDef geneActivator = null;

    public bool gizmoActivated = false;
    public string gizmoOnText = "On";
    public string gizmoOffText = "Off";
}

public class MaskExpansion
{
    public MaskExpansion(){}

    public int identifier = 0;
    public string pathExpansionOnMask = string.Empty;
    public List<MaskDef> maskDefsWithExpansion = [];
}