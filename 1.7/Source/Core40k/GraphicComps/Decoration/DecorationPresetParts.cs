using UnityEngine;
using Verse;

namespace Core40k;

public class DecorationPresetParts : IExposable
{
    public string extraDecorationDefs = null;
    public bool flipped = false;
    public bool glowEnabled = false;
    public Color colour = Color.white;
    public Color colourTwo = Color.white;
    public Color colourThree = Color.white;
    public MaskDef maskDef = null;
    
    public DecorationPresetParts()
    {
        
    }
    
    public DecorationPresetParts(ExtraDecorationDef extraDecorationDefs, bool flipped, Color colour, Color colourTwo, Color colourThree, MaskDef maskDef)
    {
        this.extraDecorationDefs = extraDecorationDefs.defName;
        this.flipped = flipped;
        this.colour = colour;
        this.colourTwo = colourTwo;
        this.colourThree = colourThree;
        this.maskDef = maskDef;
    }
    
    public void ExposeData()
    {
        Scribe_Values.Look(ref extraDecorationDefs, "extraDecorationDefs");
        Scribe_Values.Look(ref flipped, "isFlipped");
        Scribe_Values.Look(ref glowEnabled, "glowEnabled");
        Scribe_Values.Look(ref colour, "colour");
        Scribe_Values.Look(ref colourTwo, "colourTwo");
        Scribe_Values.Look(ref colourThree, "colourThree");
        Scribe_Defs.Look(ref maskDef, "maskDef");
    }
}