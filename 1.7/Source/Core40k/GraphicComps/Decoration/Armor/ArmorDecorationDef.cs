using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
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

        //Render recache runs on several threads: fill a local list and publish it in one assignment,
        //so no thread sees a half-filled list.
        var flippedRotations = new List<Rot4>();
        if (defaultShowRotation != null)
        {
            foreach (var rotation in defaultShowRotation)
            {
                flippedRotations.Add(rotation.Opposite);
            }
        }

        flippedShowRotation = flippedRotations;
        return flippedRotations;
    }
    
    public List<BodyTypeDef> appliesToBodyTypes = [];

    //Mask channels that glow, unaffected by darkness, while the player has the decoration's glow turned on.
    public List<GlowChannel> glowChannels = [];

    public bool HasGlow => !glowChannels.NullOrEmpty();

    /// <summary>
    /// The colour the glow shader gets for a channel: clear for one that does not glow.
    /// </summary>
    public Color GlowColour(GlowChannel channel, Color colour)
    {
        return glowChannels.Contains(channel) ? colour : Color.clear;
    }

    //Extra offset, layer and scale per host apparel defName, added on top of drawData the same way
    //the per-item precision data is. The precision tab's Debug copy exports an entry for this.
    public Dictionary<string, DecorationDrawData> apparelSpecificDrawData = [];

    //apparelSpecificDrawData keyed by the apparel def itself, built in ResolveReferences. This is read three
    //times a frame per decoration node, so it avoids hashing the defName string each time.
    [Unsaved]
    private Dictionary<ThingDef, DecorationDrawData> apparelSpecificDrawDataByDef;

    public DecorationDrawData ApparelSpecificDrawDataFor(ThingDef apparelDef)
    {
        if (apparelDef == null || apparelSpecificDrawData == null || apparelSpecificDrawData.Count == 0)
        {
            return null;
        }

        if (apparelSpecificDrawDataByDef != null)
        {
            return apparelSpecificDrawDataByDef.TryGetValue(apparelDef, out var byDef) ? byDef : null;
        }

        return apparelSpecificDrawData.TryGetValue(apparelDef.defName, out var data) ? data : null;
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (apparelSpecificDrawData.NullOrEmpty())
        {
            return;
        }

        var byDef = new Dictionary<ThingDef, DecorationDrawData>();
        foreach (var entry in apparelSpecificDrawData)
        {
            //Entries for apparel from mods that aren't loaded are skipped, as before.
            var apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.Key);
            if (apparelDef != null)
            {
                byDef[apparelDef] = entry.Value;
            }
        }

        apparelSpecificDrawDataByDef = byDef;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var configError in base.ConfigErrors())
        {
            yield return configError;
        }

        if (isInternal && HasGlow)
        {
            yield return "isInternal but has glowChannels - internal upgrades are never drawn";
        }
    }

    protected override void AddBuiltInRequirements(List<PawnRequirement> list)
    {
        if (!appliesToBodyTypes.NullOrEmpty())
        {
            list.Add(new PawnRequirement_BodyType { bodyTypes = appliesToBodyTypes });
        }
    }
}