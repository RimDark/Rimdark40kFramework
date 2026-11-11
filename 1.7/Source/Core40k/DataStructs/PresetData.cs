using System.Xml;
using UnityEngine;
using Verse;

namespace Core40k;

public class PresetData
{
    public DecorationDef decorationDef;

    public bool flipped = false;
    public bool glowEnabled = false;
    public Color? colour = null;
    public Color? colourTwo = null;
    public Color? colourThree = null;
    public MaskDef maskDef = null;

    public PresetData()
    {
    }

    public PresetData(DecorationDef decorationDef, bool flipped, Color colour, Color colourTwo, Color colourThree, MaskDef maskDef)
    {
        this.decorationDef = decorationDef;
        this.flipped = flipped;
        this.colour = colour;
        this.colourTwo = colourTwo;
        this.colourThree = colourThree;
        this.maskDef = maskDef;
    }

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "decorationDef", xmlRoot.Name, null, null, typeof(DecorationDef));
        foreach (XmlNode xmlNode in xmlRoot.ChildNodes)
        {
            if (xmlNode is not XmlElement childNode)
            {
                continue;
            }

            switch (childNode.Name)
            {
                case "flipped":
                    flipped = ParseHelper.FromString<bool>(childNode.FirstChild.Value);
                    break;
                case "glowEnabled":
                    glowEnabled = ParseHelper.FromString<bool>(childNode.FirstChild.Value);
                    break;
                case "colour":
                    colour = ParseHelper.FromString<Color>(childNode.FirstChild.Value);
                    break;
                case "colourTwo":
                    colourTwo = ParseHelper.FromString<Color>(childNode.FirstChild.Value);
                    break;
                case "colourThree":
                    colourThree = ParseHelper.FromString<Color>(childNode.FirstChild.Value);
                    break;
                case "maskDef":
                    DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "maskDef", childNode.InnerText);
                    break;
                default:
                    Log.Warning("Error in DecorationPresetDef, " + childNode.Name + " not recognized as a valid field.");
                    break;
            }
        }
    }
}