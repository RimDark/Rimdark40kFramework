using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Core40k;

public class Core40kModSettings : ModSettings
{
    public bool alwaysShowRankTab = false;
    
    public bool showCustomizationDebugOptions = false;
    public int decorationsPerRow = 6;

    public bool confirmRankUnlock = false;

    public bool notifyOnRankEligibility = true;

    public bool notifyOnGeneVariantAvailability = true;

    public bool showAllRankCategories = false;
    
    public bool decorationWorkEnabled = true;

    public bool decorationCostEnabled = true;

    public bool showDecorationsOnGround = true;

    public float appearanceChangeWorkAmount = 200f;

    public float minimumWorkAmount = 50f;
        
    private List<ColourPreset> colourPresets = [];
    public List<ColourPreset> ColourPresets => colourPresets;
        
    private List<DecorationPreset> extraDecorationPresets = []; //Todo: rename to decorationPresets
    public List<DecorationPreset> ExtraDecorationPresets => extraDecorationPresets;

    //Colour Preset
    public bool AddPreset(ColourPreset preset)
    {
        if (Enumerable.Any(colourPresets, cPreset => cPreset.name == preset.name))
        {
            return false;
        }
            
        colourPresets.Add(preset);
            
        Mod.WriteSettings();
        return true;
    }
    public void UpdatePreset(ColourPreset preset, Color primaryColour, Color secondaryColour, Color tertiaryColour)
    {
        var existingPreset = colourPresets.Find(cPreset => cPreset.name == preset.name);
        existingPreset.primaryColour = primaryColour;
        existingPreset.secondaryColour = secondaryColour;
        existingPreset.tertiaryColour = tertiaryColour;
        Mod.WriteSettings();
    }
    public void RemovePreset(ColourPreset preset)
    {
        if (!colourPresets.Contains(preset))
        {
            return;
        }
            
        colourPresets.Remove(preset);
        Mod.WriteSettings();
    }
        
    //Extra Decoration Preset
    public bool AddPreset(DecorationPreset preset)
    {
        if (Enumerable.Any(extraDecorationPresets, cPreset => cPreset.name == preset.name))
        {
            return false;
        }
            
        extraDecorationPresets.Add(preset);
            
        Mod.WriteSettings();
        return true;
    }
    public void UpdatePreset(DecorationPreset preset, DecorationPreset newPreset)
    {
        if (!extraDecorationPresets.Contains(preset))
        {
            return;
        }
            
        var indexOf = extraDecorationPresets.IndexOf(preset);
        extraDecorationPresets[indexOf] = newPreset;
            
        Mod.WriteSettings();
    }
    public void RemovePreset(DecorationPreset preset)
    {
        if (!extraDecorationPresets.Contains(preset))
        {
            return;
        }
            
        extraDecorationPresets.Remove(preset);
        Mod.WriteSettings();
    }
        
    public override void ExposeData()
    {
        Scribe_Values.Look(ref alwaysShowRankTab, "alwaysShowRankTab", false);
        Scribe_Values.Look(ref showCustomizationDebugOptions, "showCustomizationDebugOptions", false);
        Scribe_Values.Look(ref decorationsPerRow, "decorationsPerRow", 6);
        Scribe_Values.Look(ref confirmRankUnlock, "confirmRankUnlock", false);
        Scribe_Values.Look(ref notifyOnRankEligibility, "notifyOnRankEligibility", true);
        Scribe_Values.Look(ref notifyOnGeneVariantAvailability, "notifyOnGeneVariantAvailability", true);
        Scribe_Values.Look(ref showAllRankCategories, "showAllRankCategories", false);
        Scribe_Values.Look(ref decorationWorkEnabled, "decorationWorkEnabled", true);
        Scribe_Values.Look(ref decorationCostEnabled, "decorationCostEnabled", true);
        Scribe_Values.Look(ref showDecorationsOnGround, "showDecorationsOnGround", true);
        Scribe_Values.Look(ref appearanceChangeWorkAmount, "appearanceChangeWorkAmount", 200f);
        Scribe_Values.Look(ref minimumWorkAmount, "minimumWorkAmount", 50f);
        Scribe_Collections.Look(ref colourPresets, "colourPresets");
        Scribe_Collections.Look(ref extraDecorationPresets, "extraDecorationPresets");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            foreach (var preset in colourPresets)
            {
                if (preset.appliesToKind == PresetType.None)
                {
                    preset.appliesToKind = PresetType.Armor;
                }
            }
        }
        
        base.ExposeData();
    }
}