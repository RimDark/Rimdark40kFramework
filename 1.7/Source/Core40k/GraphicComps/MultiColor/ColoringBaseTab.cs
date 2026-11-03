using System;
using System.Collections.Generic;
using System.Linq;
using ColourPicker;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class ColoringBaseTab : CustomizerTabDrawer
{
    protected static Core40kModSettings modSettings = null;
    public static Core40kModSettings ModSettings => modSettings ??= LoadedModManager.GetMod<Core40kMod>().GetSettings<Core40kModSettings>();

    private bool DevMode => Prefs.DevMode;
    
    protected List<CompMultiColor> multiColorComps = [];
    
    private Dictionary<ThingDef, List<MaskDef>> masks = new ();
    private Dictionary<ThingDef, int> apparelColorMaskPageNumber = new Dictionary<ThingDef, int>();
    private Dictionary<(ThingDef, MaskDef), Material> cachedMaterials = new ();
    private readonly Dictionary<CompMultiColor, CompAlternateTexture> alternateTextureComps = new();
    private readonly Dictionary<(ThingDef, AlternateBaseFormDef), List<MaskDef>> filteredMasks = new();
    private readonly List<MaskDef> curPageMaskBuffer = [];

    private CompAlternateTexture AlternateTextureOf(CompMultiColor multiColor)
    {
        if (!alternateTextureComps.TryGetValue(multiColor, out var alternateTexture))
        {
            alternateTexture = multiColor.parent.GetComp<CompAlternateTexture>();
            alternateTextureComps.Add(multiColor, alternateTexture);
        }

        return alternateTexture;
    }

    /// <summary>
    /// The masks for an item, minus those the current alternate form forbids. Filtered once per
    /// (item, form) pair rather than on every frame.
    /// </summary>
    private List<MaskDef> MasksFor(ThingDef def, List<MaskDef> allMasks, AlternateBaseFormDef alternateBaseForm)
    {
        if (alternateBaseForm == null)
        {
            return allMasks;
        }

        var key = (def, alternateBaseForm);
        if (filteredMasks.TryGetValue(key, out var filtered))
        {
            return filtered;
        }

        filtered = [];
        foreach (var maskDef in allMasks)
        {
            if (!alternateBaseForm.incompatibleMaskDefs.Contains(maskDef))
            {
                filtered.Add(maskDef);
            }
        }
        filteredMasks.Add(key, filtered);
        return filtered;
    }
    private List<ColourPresetDef> presets;

    private Dictionary<ThingDef, (Color col1, Color col2, Color col3)> defaultColors = new();
    
    private bool recache = true;
    private float viewRectHeight;
    
    public override void Setup(Pawn pawn)
    {
        SetupHook(pawn);
        alternateTextureComps.Clear();
        filteredMasks.Clear();
        
        var masksTemp = DefDatabase<MaskDef>.AllDefs.Where(def => def.appliesToKind is AppliesToKind.Thing or AppliesToKind.All).ToList();
        
        foreach (var multiColor in multiColorComps)
        {
            multiColor.SetOriginals();
            var masksForItem = masksTemp.Where(mask => mask.appliesTo.Contains(multiColor.parent.def.defName) || mask.appliesToKind == AppliesToKind.All).ToList();

            if (masksForItem.Any())
            {
                masksForItem.SortBy(def => def.sortOrder);
                masks.Add(multiColor.parent.def, masksForItem);
            }

            defaultColors.Add(multiColor.parent.def, DefaultColorsFor(multiColor));
            
            apparelColorMaskPageNumber.Add(multiColor.parent.def, 0);
        }

        presets = DefDatabase<ColourPresetDef>.AllDefsListForReading;
    }
    
    protected virtual void SetupHook(Pawn pawn) { }

    public override IEnumerable<CompGraphicParent> Comps => multiColorComps;

    private bool IsColourable(CompMultiColor multiColor)
    {
        return multiColor.TextureColourable(AlternateTextureOf(multiColor)?.CurrentAlternateBaseForm);
    }

    public override bool IsVisible(Pawn pawn)
    {
        foreach (var multiColor in multiColorComps)
        {
            if (IsColourable(multiColor))
            {
                return true;
            }
        }

        return false;
    }

    public override void DrawTab(Rect rect, Pawn pawn, ref Vector2 scrollPosition)
    {
        var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, viewRectHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        var curY = rect.y;
        foreach (var multiColor in multiColorComps)
        {
            if (!IsColourable(multiColor))
            {
                continue;
            }

            var item = multiColor.parent;
            //Item name
            var nameRect = new Rect(rect.x, curY, viewRect.width, 30f);
            nameRect.width /= 2;
            nameRect.x += nameRect.width / 2;
            Widgets.DrawMenuSection(nameRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(nameRect, item.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;

            if (multiColor.ColoursLocked(out var colourLockReason))
            {
                curY += nameRect.height + 3f;
                var lockedRect = new Rect(rect.x, curY, viewRect.width - 16f, 30f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Core40kUtils.LockedColour;
                Widgets.Label(lockedRect, "BEWH.Framework.Customization.ColoursLocked".Translate(colourLockReason));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                curY += lockedRect.height + 22f;
                continue;
            }

            //Select button
            var selectPresetRect = new Rect(rect.x, curY, viewRect.width - 16f, 30f);
            selectPresetRect.width /= 5;
            selectPresetRect.x = nameRect.xMax + nameRect.width/20;
            if (Widgets.ButtonText(selectPresetRect, "BEWH.Framework.Customization.ColorPreset".Translate()))
            {
                var list = new List<FloatMenuOption>();
                
                var defaultMenuOption = new FloatMenuOption("BEWH.Framework.CommonKeyword.Default".Translate(), delegate
                {
                    multiColor.DrawColor = defaultColors[item.def].col1;
                    multiColor.DrawColorTwo = defaultColors[item.def].col2;
                    multiColor.DrawColorThree = defaultColors[item.def].col3;
                    recache = true;
                            
                }, Core40kUtils.ThreeColourPreview(defaultColors[item.def].col1, defaultColors[item.def].col2, defaultColors[item.def].col3, 3), Color.white);
                list.Add(defaultMenuOption);
                
                foreach (var preset in presets.Where(p => p.appliesTo.Contains(item.def.defName) || p.appliesTo.Empty()))
                {
                    var menuOption = new FloatMenuOption(preset.label, delegate
                    {
                        multiColor.DrawColor = preset.primaryColour;
                        multiColor.DrawColorTwo = preset.secondaryColour;
                        multiColor.DrawColorThree = preset.tertiaryColour ?? preset.secondaryColour;
                        recache = true;
                            
                    }, Core40kUtils.ThreeColourPreview(preset.primaryColour, preset.secondaryColour, preset.tertiaryColour, preset.colorAmount), Color.white);
                    list.Add(menuOption);
                }
                    
                foreach (var preset in ModSettings.ColourPresets.Where(preset => preset.appliesToKind is PresetType.Armor or PresetType.All))
                {
                    var menuOption = new FloatMenuOption(preset.name.CapitalizeFirst(), delegate
                    {
                        multiColor.DrawColor = preset.primaryColour;
                        multiColor.DrawColorTwo = preset.secondaryColour;
                        multiColor.DrawColorThree = preset.tertiaryColour ?? preset.secondaryColour;
                        recache = true;
                            
                    }, Core40kUtils.ThreeColourPreview(preset.primaryColour, preset.secondaryColour, preset.tertiaryColour, 3), Color.white);
                    list.Add(menuOption);
                }
                
                if (!list.NullOrEmpty())
                {
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            }
                
            //Save button
            var savePresetRect = new Rect(rect.x, curY, viewRect.width - 16f, 30f);
            savePresetRect.width /= 5;
            savePresetRect.x = nameRect.xMin - savePresetRect.width - nameRect.width/20;
            if (Widgets.ButtonText(savePresetRect, "BEWH.Framework.Customization.EditPreset".Translate()))
            {
                var list = new List<FloatMenuOption>();
                    
                //Delete or override existing
                foreach (var preset in ModSettings.ColourPresets.Where(preset => preset.appliesToKind is PresetType.Armor or PresetType.All))
                {
                    var menuOption = new FloatMenuOption(preset.name, delegate
                    {
                        Find.WindowStack.Add(new Dialog_ConfirmColorPresetOverride(preset, multiColor.DrawColor, multiColor.DrawColorTwo, multiColor.DrawColorThree));
                    }, Widgets.PlaceholderIconTex, Color.white);
                    menuOption.extraPartWidth = 30f;
                    menuOption.extraPartOnGUI = rect1 => Core40kUtils.DeletePreset(rect1, preset);
                    menuOption.tooltip = "BEWH.Framework.Customization.OverridePreset".Translate(preset.name);
                    list.Add(menuOption);
                }
                    
                //Create new
                var newPreset = new FloatMenuOption("BEWH.Framework.Customization.NewPreset".Translate(), delegate
                {
                    var newColourPreset = new ColourPreset
                    {
                        primaryColour = multiColor.DrawColor,
                        secondaryColour = multiColor.DrawColorTwo,
                        tertiaryColour = multiColor.DrawColorThree,
                        appliesToKind = PresetType.Armor,
                    };
                    Find.WindowStack.Add( new Dialog_EditColourPresets(newColourPreset));
                }, Widgets.PlaceholderIconTex, Color.white);
                list.Add(newPreset);
                
                if (!list.NullOrEmpty())
                {
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            }
            
            curY += nameRect.height + 3f;
            var itemRect = new Rect(rect.x, curY, viewRect.width - 16f, 76f);
            curY += itemRect.height;
                
            var colorOneRect = new Rect(itemRect)
            {
                x = itemRect.xMin,
                y = itemRect.yMin + 2f
            };
            Rect colorTwoRect;

            var alternateTexture = AlternateTextureOf(multiColor);
            
            var colAmount = multiColor.Props.colorMaskAmount;
            if (alternateTexture?.CurrentAlternateBaseForm != null)
            {
                colAmount = alternateTexture.CurrentAlternateBaseForm.colorAmount;
            }
            if (multiColor.MaskDef is { setsNull: false })
            {
                colAmount = multiColor.MaskDef.colorAmount;
            }
            
            switch (colAmount)
            {
                case 1:
                    colorOneRect = colorOneRect.ContractedBy(3);
                    DecoColorBox(colorOneRect, multiColor, multiColor.DrawColor, 1);
                    break;
                case 2:
                    colorOneRect.width /= 2;
                    colorTwoRect = new Rect(colorOneRect)
                    {
                        x = colorOneRect.xMax
                    };
                    
                    colorOneRect = colorOneRect.ContractedBy(3);
                    DecoColorBox(colorOneRect, multiColor, multiColor.DrawColor, 1);
                    
                    colorTwoRect = colorTwoRect.ContractedBy(3);
                    DecoColorBox(colorTwoRect, multiColor, multiColor.DrawColorTwo, 2);
                    break;
                case 3:
                    colorOneRect.width /= 3;
                    colorTwoRect = new Rect(colorOneRect)
                    {
                        x = colorOneRect.xMax
                    };
                    var colorThreeRect = new Rect(colorTwoRect)
                    {
                        x = colorTwoRect.xMax
                    };
                    
                    colorOneRect = colorOneRect.ContractedBy(3);
                    DecoColorBox(colorOneRect, multiColor, multiColor.DrawColor, 1);
                    
                    colorTwoRect = colorTwoRect.ContractedBy(3);
                    DecoColorBox(colorTwoRect, multiColor, multiColor.DrawColorTwo, 2);
                    
                    colorThreeRect = colorThreeRect.ContractedBy(3);
                    DecoColorBox(colorThreeRect, multiColor, multiColor.DrawColorThree, 3);
                    break;
                default:
                    Log.Warning("Wrong setup in " + item + "colorAmount is more than 3 or less than 1");
                    break;
            }
            
            //Mask Stuff
            if (masks.TryGetValue(item.def, out var allMasks) && allMasks.Count > 1)
            {
                var maskRect = new Rect(itemRect)
                {
                    y = colorOneRect.yMax + 3f,
                };
                maskRect.height = maskRect.width/4;
                var arrowsEnabled = allMasks.Count > 4;
                if (arrowsEnabled)
                {
                    maskRect.height += maskRect.height / 5;
                }
                curY += maskRect.height;
                maskRect = maskRect.ContractedBy(3);
                Widgets.DrawMenuSection(maskRect.ContractedBy(-1));
                var posRect = new Rect(maskRect);
                posRect.width /= 4;
                posRect.height = posRect.width;
                
                var maskDefs = MasksFor(item.def, allMasks, alternateTexture?.CurrentAlternateBaseForm);

                var maxPage = Math.Max(0, (int)Math.Ceiling((float)maskDefs.Count / 4) - 1);
                if (apparelColorMaskPageNumber[item.def] > maxPage)
                {
                    apparelColorMaskPageNumber[item.def] = maxPage;
                }

                var pageStart = apparelColorMaskPageNumber[item.def] * 4;
                var num = Math.Max(0, Math.Min(4, maskDefs.Count - pageStart));

                var curPageMasks = curPageMaskBuffer;
                curPageMasks.Clear();
                for (var m = 0; m < num; m++)
                {
                    curPageMasks.Add(maskDefs[pageStart + m]);
                }
                
                for (var i = 0; i < curPageMasks.Count; i++)
                {
                    var curPosRect = new Rect(posRect);
                    curPosRect.x += curPosRect.width * i;
                    curPosRect = curPosRect.ContractedBy(15);
                    if (!cachedMaterials.ContainsKey((item.def, curPageMasks[i])) || recache)
                    {
                        if (recache)
                        {
                            cachedMaterials = new Dictionary<(ThingDef, MaskDef), Material>();
                        }
                        var graphic = MaskPreviewGraphic(multiColor, curPageMasks[i], pawn);
                        var material = graphic?.MatSouth ?? BaseContent.BadMat;
                        cachedMaterials.Add((item.def, curPageMasks[i]), material);
                        recache = false;
                    }
                    if (multiColor.MaskDef == curPageMasks[i])
                    {
                        Widgets.DrawStrongHighlight(curPosRect.ExpandedBy(6f));
                    }
                    
                    Widgets.DrawMenuSection(curPosRect.ContractedBy(-1));
                    
                    var windowMax = rect.yMax + scrollPosition.y;
                    var windowMin = rect.yMin + scrollPosition.y;
                    if (curPosRect.yMin > windowMin && curPosRect.yMax < windowMax)
                    {
                        Graphics.DrawTexture(curPosRect, cachedMaterials[(item.def, curPageMasks[i])].mainTexture, cachedMaterials[(item.def, curPageMasks[i])]);
                    }
                    
                    TooltipHandler.TipRegion(curPosRect, curPageMasks[i].label);
                    
                    Widgets.DrawHighlightIfMouseover(curPosRect);
                    if (Widgets.ButtonInvisible(curPosRect))
                    {
                        multiColor.MaskDef = curPageMasks[i];
                    }
                }
                if (arrowsEnabled)
                {
                    var arrowBack = new Rect(maskRect)
                    {
                        height = maskRect.height / 5,
                        width = maskRect.height / 5
                    };
                    arrowBack.y = maskRect.yMax - arrowBack.height;
                    
                    if (apparelColorMaskPageNumber[item.def] > 0)
                    {
                        if (Widgets.ButtonInvisible(arrowBack))
                        {
                            apparelColorMaskPageNumber[item.def]--;
                        }
                        arrowBack = arrowBack.ContractedBy(5);
                        Widgets.DrawTextureFitted(arrowBack, Core40kUtils.ScrollBackwardIcon, 1);
                    }
                    
                    if (apparelColorMaskPageNumber[item.def] < maxPage)
                    {
                        var arrowForward = new Rect(arrowBack)
                        {
                            x = maskRect.xMax - arrowBack.width
                        };
                        if (Widgets.ButtonInvisible(arrowForward))
                        {
                            apparelColorMaskPageNumber[item.def]++;
                        }
                        arrowForward = arrowForward.ContractedBy(5);
                        Widgets.DrawTextureFitted(arrowForward, Core40kUtils.ScrollForwardIcon, 1);
                    }
                }
            }
            curY += 22;
        }
        if (Event.current.type == EventType.Layout)
        {
            viewRectHeight = curY - rect.y;
        }
        Widgets.EndScrollView();
    }
    
    /// <summary>
    /// The graphic shown on a mask button: the item in its current colours with that mask applied.
    /// </summary>
    protected virtual Graphic MaskPreviewGraphic(CompMultiColor multiColor, MaskDef mask, Pawn pawn)
    {
        var item = multiColor.parent;
        var alternateTexture = AlternateTextureOf(multiColor);
        var bodyType = BodyTypeUtils.SafeBodyType(
            pawn, item.def.GetModExtension<DefModExtension_ForcesBodyType>()?.forcedBodyType);
        var gender = pawn?.gender ?? Gender.None;

        var alternatePath = alternateTexture?.CurrentAlternateBaseForm?.drawnTextureIconPath;

        var usedPath = string.Empty;

        if (item is Apparel apparel)
        {
            usedPath = alternatePath.NullOrEmpty() ? apparel.WornGraphicPath : alternatePath;

            usedPath = apparel.def.apparel.LastLayer != ApparelLayerDefOf.Overhead
                       && apparel.def.apparel.LastLayer != ApparelLayerDefOf.EyeCover
                       && !apparel.RenderAsPack()
                       && usedPath != BaseContent.PlaceholderImagePath
                       && usedPath != BaseContent.PlaceholderGearImagePath
                ? BodyTypeUtils.BodyTypedPath(usedPath, bodyType, gender) : usedPath;
        }
        else
        {
            usedPath = alternatePath.NullOrEmpty() ? item.def.graphicData.texPath : alternatePath;
        }

        var shader = Core40kDefOf.BEWH_CutoutThreeColor.Shader;
        var maskPath = mask?.maskPath;
        if (mask != null && mask.useBodyTypes)
        {
            maskPath = BodyTypeUtils.BodyTypedMaskPath(maskPath, bodyType, gender);
        }
        return MultiColorUtils.GetGraphic<Graphic_Multi>(usedPath, shader, item.def.graphicData.drawSize, multiColor.DrawColor, multiColor.DrawColorTwo, multiColor.DrawColorThree, null, maskPath);
    }

    /// <summary>
    /// The colours the "Default" preset restores.
    /// </summary>
    protected virtual (Color col1, Color col2, Color col3) DefaultColorsFor(CompMultiColor multiColor)
    {
        var item = multiColor.parent;
        return (multiColor.Props?.defaultPrimaryColor ?? (item.def.MadeFromStuff ? item.def.GetColorForStuff(item.Stuff) : Color.white), multiColor.Props?.defaultSecondaryColor ?? Color.white, multiColor.Props?.defaultTertiaryColor ?? Color.white);
    }

    private void DecoColorBox(Rect colorRect, CompMultiColor multiColor, Color currentColor, int colorNum)
    {
        colorRect = colorRect.ContractedBy(2f);
        Widgets.DrawMenuSection(colorRect);
        colorRect = colorRect.ContractedBy(1f);
        Widgets.DrawRectFast(colorRect, currentColor);
        TooltipHandler.TipRegion(colorRect, "BEWH.Framework.Customization.ChooseCustomColour".Translate());
        if (Widgets.ButtonInvisible(colorRect))
        {
            Find.WindowStack.Add( new Dialog_ColourPicker( currentColor, newColour =>
            {
                recache = true;
                switch (colorNum)
                {
                    case 1:
                        multiColor.DrawColor = newColour;
                        break;
                    case 2:
                        multiColor.DrawColorTwo = newColour;
                        break;
                    case 3:
                        multiColor.DrawColorThree = newColour;
                        break;
                }
            } ) );
        }
    }
    
    public override void OnClose(Pawn pawn, bool closeOnCancel, bool closeOnClickedOutside)
    {
        OnReset(pawn);
    }
    
    public override void OnReset(Pawn pawn)
    {
        foreach (var multiColor in multiColorComps)
        {
            multiColor.Reset();
        }
    }
}