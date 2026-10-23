using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Core40k;

//TODO: Rename to ArmorDecorationDef at 1.7
public class ExtraDecorationDef : DecorationDef
{
    public bool drawInHeadSpace = false;
    
    public bool decoSizeMatchesThingSize = false;

    public bool showOnGround = true;
    
    public List<Rot4> defaultShowRotation = [Rot4.North, Rot4.South, Rot4.East, Rot4.West];
    
    [Unsaved]
    private List<Rot4> flippedShowRotation;

    public List<Rot4> ShowRotation(bool flipped)
    {
        if (!flipped)
        {
            return defaultShowRotation;
        }

        if (flippedShowRotation != null)
        {
            return flippedShowRotation;
        }

        flippedShowRotation = [];
        if (defaultShowRotation != null)
        {
            foreach (var rotation in defaultShowRotation)
            {
                flippedShowRotation.Add(rotation.Opposite);
            }
        }

        return flippedShowRotation;
    }
    
    public List<BodyTypeDef> appliesToBodyTypes = [];

    //Extra offset, layer and scale per host apparel defName, added on top of drawData the same way
    //the per-item precision data is. The precision tab's Debug copy exports an entry for this.
    public Dictionary<string, DecorationDrawData> apparelSpecificDrawData = [];

    public DecorationDrawData ApparelSpecificDrawDataFor(ThingDef apparelDef)
    {
        if (apparelDef == null || apparelSpecificDrawData == null || apparelSpecificDrawData.Count == 0)
        {
            return null;
        }
        return apparelSpecificDrawData.TryGetValue(apparelDef.defName, out var data) ? data : null;
    }

    public override bool MeetsPawnRequirements(Pawn pawn)
    {
        if (!base.MeetsPawnRequirements(pawn))
        {
            return false;
        }
        if (appliesToBodyTypes.NullOrEmpty() || pawn?.apparel == null)
        {
            return true;
        }

        Apparel bodyApparel = null;
        var wornApparel = pawn.apparel.WornApparel;
        for (var i = 0; i < wornApparel.Count; i++)
        {
            if (wornApparel[i].HasComp<CompDecorative>())
            {
                bodyApparel = wornApparel[i];
                break;
            }
        }
        if (bodyApparel == null)
        {
            return true;
        }

        var pawnBodyType = BodyTypeUtils.SafeBodyType(
            pawn, bodyApparel.def?.GetModExtension<DefModExtension_ForcesBodyType>()?.forcedBodyType);
        return BodyTypeUtils.MatchesAny(pawnBodyType, appliesToBodyTypes, out _);
    }

    public override bool HasRequirements(Pawn pawn, out string lockedReason)
    {
        var requirementFulfilled = base.HasRequirements(pawn, out lockedReason);
        if (appliesToBodyTypes.NullOrEmpty())
        {
            return requirementFulfilled;
        }
        if (pawn?.apparel == null)
        {
            return requirementFulfilled;
        }
        var bodyApparel = pawn.apparel.WornApparel.FirstOrFallback(a => a.HasComp<CompDecorative>());
        if (bodyApparel == null)
        {
            return requirementFulfilled;
        }
        
        var reason = new StringBuilder();
        reason.AppendLine(lockedReason);
        
        var pawnBodyType = BodyTypeUtils.SafeBodyType(
            pawn, bodyApparel.def?.GetModExtension<DefModExtension_ForcesBodyType>()?.forcedBodyType);
        if (!BodyTypeUtils.MatchesAny(pawnBodyType, appliesToBodyTypes, out _))
        {
            reason.AppendLine("BEWH.Framework.Customization.InvalidBodytype".Translate());
            lockedReason = reason.ToString();
            requirementFulfilled = false;
        }
        
        return requirementFulfilled;
    }
}