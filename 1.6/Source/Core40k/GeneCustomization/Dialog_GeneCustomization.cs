using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColourPicker;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Core40k;

public class Dialog_GeneCustomization : Window
{
    private readonly Pawn pawn;
    private readonly CompGeneCustomization comp;

    private readonly List<GeneVariantChoice> snapshot;
    private bool accepted;

    private static readonly Vector2 ButSize = new(200f, 40f);
    private static readonly Vector3 PortraitOffset = new(0f, 0f, 0.15f);

    private const float SlotColumnWidth = 260f;
    private const float SetPanelHeight = 120f;
    private const float SlotRowHeight = 44f;
    private const float HeaderHeight = 28f;
    private const float IconSize = 72f;
    private const float IconGap = 8f;

    private Vector2 slotScroll;
    private Vector2 variantScroll;
    private Vector2 setScroll;

    private readonly List<GeneDef> slots;
    private readonly Dictionary<GeneDef, DefModExtension_CustomizableGene> extensions = new();
    private readonly List<GeneVariantSetDef> relevantSets = [];
    private GeneDef selectedSlot;

    private readonly QuickSearchWidget searchWidget = new();

    private readonly Dictionary<(GeneVariantDef, MaskDef), Material> cachedMaterials = new();
    private bool recacheMaterials;

    //The game is paused while the dialog is open, so neither result changes until Accept.
    private readonly Dictionary<GeneVariantDef, (bool met, string reason)> requirementCache = new();
    private readonly Dictionary<GeneVariantDef, (bool affordable, string reason)> costCache = new();

    public override Vector2 InitialSize => new(1100f, 800f);

    private string cachedTitle;
    private string Title => cachedTitle ??= "BEWH.Framework.GeneCustomization.Title".Translate(pawn.Name.ToStringShort);

    public Dialog_GeneCustomization(Pawn pawn)
    {
        this.pawn = pawn;
        comp = pawn.GetComp<CompGeneCustomization>();

        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        doCloseX = true;

        comp.Reconcile();
        snapshot = comp.Snapshot();

        slots = comp.CustomizableGenes();
        foreach (var geneDef in slots)
        {
            extensions[geneDef] = geneDef.GetModExtension<DefModExtension_CustomizableGene>();
            foreach (var variant in GeneVariantIndex.VariantsFor(geneDef))
            {
                foreach (var set in GeneVariantIndex.SetsFor(variant))
                {
                    if (!relevantSets.Contains(set))
                    {
                        relevantSets.Add(set);
                    }
                }
            }
        }

        relevantSets.SortBy(set => set.sortOrder);
        selectedSlot = slots.FirstOrFallback();
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (comp == null || pawn.Dead)
        {
            Close();
            return;
        }

        Text.Font = GameFont.Medium;
        var titleRect = new Rect(inRect)
        {
            height = Text.LineHeight * 2f
        };
        Widgets.Label(titleRect, Title);
        Text.Font = GameFont.Small;
        inRect.yMin = titleRect.yMax + 4f;

        var contentRect = inRect;
        contentRect.yMax -= ButSize.y + 4f;

        var setRect = contentRect.BottomPartPixels(SetPanelHeight);
        contentRect.yMax = setRect.yMin - 6f;

        var pawnRect = contentRect.LeftPart(0.26f);
        DrawPawn(pawnRect);

        var slotRect = new Rect(pawnRect.xMax + 10f, contentRect.y, SlotColumnWidth, contentRect.height);
        Widgets.DrawMenuSection(slotRect);
        DrawSlots(slotRect.ContractedBy(6f));

        var variantRect = new Rect(slotRect.xMax + 10f, contentRect.y, contentRect.xMax - slotRect.xMax - 10f, contentRect.height);
        Widgets.DrawMenuSection(variantRect);
        DrawVariants(variantRect.ContractedBy(10f));

        Widgets.DrawMenuSection(setRect);
        DrawSets(setRect.ContractedBy(6f));

        DrawBottomButtons(inRect);
    }

    private void DrawPawn(Rect rect)
    {
        Widgets.BeginGroup(rect);
        for (var i = 0; i < 4; i++)
        {
            var position = new Rect(0f, rect.height / 4f * i, rect.width, rect.height / 4f).ContractedBy(4f);
            var image = PortraitsCache.Get(pawn, new Vector2(position.width, position.height), new Rot4(3 - i), PortraitOffset, 1.1f, supersample: true, compensateForUIScale: true, true, true, null, null, stylingStation: true);
            GUI.DrawTexture(position, image);
        }
        Widgets.EndGroup();
    }

    //Slots

    private bool SlotUnlocked(GeneDef geneDef, out string reason)
    {
        reason = string.Empty;
        return extensions.TryGetValue(geneDef, out var extension) && extension.HasRequirements(pawn, out reason);
    }

    private string SlotLabel(GeneDef geneDef)
    {
        var extension = extensions.TryGetValue(geneDef, out var ext) ? ext : null;
        var label = extension?.slotLabel.NullOrEmpty() == false ? extension.slotLabel : geneDef.label;
        return label.CapitalizeFirst();
    }

    private void DrawSlots(Rect rect)
    {
        if (slots.Count == 0)
        {
            Widgets.Label(rect, "BEWH.Framework.GeneCustomization.NoSlots".Translate());
            return;
        }

        var viewHeight = 0f;
        GeneSlotCategoryDef lastCategory = null;
        var first = true;
        foreach (var geneDef in slots)
        {
            var category = extensions[geneDef]?.category;
            if (first || category != lastCategory)
            {
                viewHeight += HeaderHeight;
                lastCategory = category;
                first = false;
            }
            viewHeight += SlotRowHeight;
        }

        var viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);
        Widgets.BeginScrollView(rect, ref slotScroll, viewRect);

        var y = 0f;
        lastCategory = null;
        first = true;
        foreach (var geneDef in slots)
        {
            var category = extensions[geneDef]?.category;
            if (first || category != lastCategory)
            {
                var headerRect = new Rect(0f, y, viewRect.width, HeaderHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Medium;
                Widgets.Label(headerRect, (category ?? Core40kDefOf.BEWH_GeneSlot_Undefined).LabelCap);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                y += HeaderHeight;
                lastCategory = category;
                first = false;
            }

            var rowRect = new Rect(0f, y, viewRect.width, SlotRowHeight);
            DrawSlotRow(rowRect, geneDef);
            y += SlotRowHeight;
        }

        Widgets.EndScrollView();
    }

    private void DrawSlotRow(Rect rowRect, GeneDef geneDef)
    {
        var selected = geneDef == selectedSlot;
        if (selected)
        {
            Widgets.DrawHighlightSelected(rowRect);
        }
        else
        {
            Widgets.DrawHighlightIfMouseover(rowRect);
        }

        var unlocked = SlotUnlocked(geneDef, out var lockedReason);
        var variant = comp.VariantFor(geneDef);

        var iconRect = new Rect(rowRect.x + 4f, rowRect.y + 4f, rowRect.height - 8f, rowRect.height - 8f);
        var icon = variant?.Icon ?? geneDef.Icon;
        if (icon != null)
        {
            GUI.color = variant == null ? geneDef.IconColor : Color.white;
            Widgets.DrawTextureFitted(iconRect, icon, 1f);
            GUI.color = Color.white;
        }

        var labelRect = new Rect(iconRect.xMax + 6f, rowRect.y + 2f, rowRect.width - iconRect.width - 34f, rowRect.height / 2f);
        var subRect = new Rect(labelRect.x, labelRect.yMax, labelRect.width, rowRect.height / 2f - 2f);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, SlotLabel(geneDef));
        GUI.color = unlocked ? Color.gray : Core40kUtils.RequirementNotMetColour;
        Text.Font = GameFont.Tiny;
        Widgets.Label(subRect, variant?.LabelCap ?? "BEWH.Framework.CommonKeyword.None".Translate().ToString());
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        if (!unlocked)
        {
            var lockRect = new Rect(rowRect.xMax - 26f, rowRect.y + (rowRect.height - 20f) / 2f, 20f, 20f);
            GUI.color = Core40kUtils.LockedColour;
            Widgets.DrawTextureFitted(lockRect, GeneCustomizationTex.LockIcon, 1f);
            GUI.color = Color.white;
        }

        var tooltip = new StringBuilder();
        tooltip.AppendLine(geneDef.LabelCap);
        if (!geneDef.description.NullOrEmpty())
        {
            tooltip.AppendLine();
            tooltip.AppendLine(geneDef.description);
        }
        if (!unlocked && !lockedReason.NullOrEmpty())
        {
            tooltip.AppendLine();
            tooltip.AppendLine(lockedReason.TrimEndNewlines());
        }
        TooltipHandler.TipRegion(rowRect, tooltip.ToString().TrimEndNewlines());

        if (Widgets.ButtonInvisible(rowRect))
        {
            selectedSlot = geneDef;
            searchWidget.Reset();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
    }

    //Variants

    private void DrawVariants(Rect rect)
    {
        if (selectedSlot == null)
        {
            Widgets.Label(rect, "BEWH.Framework.GeneCustomization.SelectSlot".Translate());
            return;
        }

        var headerRect = rect.TopPartPixels(Text.LineHeight * 1.5f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(headerRect, "BEWH.Framework.GeneCustomization.VariantsFor".Translate(SlotLabel(selectedSlot)));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        var searchRect = new Rect(rect.x, headerRect.yMax + 4f, Mathf.Min(rect.width, 300f), QuickSearchWidget.WidgetHeight);
        searchWidget.OnGUI(searchRect, null, null);

        var slotUnlocked = SlotUnlocked(selectedSlot, out var slotLockedReason);
        var y = searchRect.yMax + 6f;
        if (!slotUnlocked)
        {
            var reasonRect = new Rect(rect.x, y, rect.width, Text.CalcHeight(slotLockedReason, rect.width) + 4f);
            GUI.color = Core40kUtils.RequirementNotMetColour;
            Widgets.Label(reasonRect, slotLockedReason.TrimEndNewlines());
            GUI.color = Color.white;
            y = reasonRect.yMax + 4f;
        }

        var extension = extensions[selectedSlot];
        var current = comp.VariantFor(selectedSlot);
        var variants = GeneVariantIndex.VariantsFor(selectedSlot);

        var entries = new List<GeneVariantDef>();
        if (extension == null || extension.allowNone)
        {
            entries.Add(null);
        }
        foreach (var variant in variants)
        {
            if (!searchWidget.filter.Active || searchWidget.filter.Matches(variant.label ?? variant.defName))
            {
                entries.Add(variant);
            }
        }

        var gridRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
        var optionsHeight = current != null && current.colourable && current.HasVisual ? 70f : 0f;
        gridRect.yMax -= optionsHeight;

        var perRow = Mathf.Max(1, Mathf.FloorToInt((gridRect.width - 16f + IconGap) / (IconSize + IconGap)));
        var rows = Mathf.CeilToInt(entries.Count / (float)perRow);
        var cellHeight = IconSize + Text.LineHeight * 2f + IconGap;
        var viewRect = new Rect(0f, 0f, gridRect.width - 16f, rows * cellHeight);

        Widgets.BeginScrollView(gridRect, ref variantScroll, viewRect);
        for (var i = 0; i < entries.Count; i++)
        {
            var column = i % perRow;
            var row = i / perRow;
            var cellRect = new Rect(column * (IconSize + IconGap), row * cellHeight, IconSize, cellHeight - IconGap);
            DrawVariantCell(cellRect, entries[i], current, slotUnlocked);
        }
        Widgets.EndScrollView();

        if (optionsHeight > 0f)
        {
            var optionsRect = new Rect(rect.x, gridRect.yMax + 4f, rect.width, optionsHeight - 4f);
            DrawColourOptions(optionsRect, current);
        }
    }

    private void DrawVariantCell(Rect cellRect, GeneVariantDef variant, GeneVariantDef current, bool slotUnlocked)
    {
        var iconRect = new Rect(cellRect.x, cellRect.y, IconSize, IconSize);
        var labelRect = new Rect(cellRect.x - IconGap / 2f, iconRect.yMax, IconSize + IconGap, cellRect.yMax - iconRect.yMax);

        var isCurrent = variant == current;
        var hasRequirements = HasRequirementsCached(variant, out var lockedReason);
        var affordable = isCurrent || IsInSnapshot(variant) || CanPayCostCached(variant, out _);
        CanPayCostCached(variant, out var costReason);

        var selectable = slotUnlocked && hasRequirements && affordable;

        Widgets.DrawMenuSection(iconRect);
        if (isCurrent)
        {
            Widgets.DrawHighlightSelected(iconRect);
        }
        else if (selectable)
        {
            Widgets.DrawHighlightIfMouseover(iconRect);
        }

        var innerRect = iconRect.ContractedBy(4f);
        if (variant == null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            Widgets.DrawTextureFitted(innerRect, GeneCustomizationTex.NoneIcon, 0.6f);
        }
        else
        {
            GUI.color = selectable || isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            Widgets.DrawTextureFitted(innerRect, variant.Icon, 1f);
        }
        GUI.color = Color.white;

        if (variant != null && (!hasRequirements || !slotUnlocked))
        {
            var lockRect = new Rect(iconRect.xMax - 22f, iconRect.y + 2f, 20f, 20f);
            GUI.color = Core40kUtils.LockedColour;
            Widgets.DrawTextureFitted(lockRect, GeneCustomizationTex.LockIcon, 1f);
            GUI.color = Color.white;
        }

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        GUI.color = selectable || isCurrent ? Color.white : Color.gray;
        Widgets.Label(labelRect, variant?.LabelCap ?? "BEWH.Framework.CommonKeyword.None".Translate().ToString());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        TooltipHandler.TipRegion(iconRect, () => VariantTooltip(variant, current, isCurrent, hasRequirements, lockedReason, affordable, costReason), variant?.GetHashCode() ?? 0);

        if (!Widgets.ButtonInvisible(iconRect))
        {
            return;
        }

        if (!selectable)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        if (isCurrent)
        {
            return;
        }

        comp.SetVariant(selectedSlot, variant);
        recacheMaterials = true;
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }

    private bool HasRequirementsCached(GeneVariantDef variant, out string reason)
    {
        reason = string.Empty;
        if (variant == null)
        {
            return true;
        }

        if (!requirementCache.TryGetValue(variant, out var cached))
        {
            cached.met = variant.HasRequirements(pawn, out cached.reason);
            requirementCache.Add(variant, cached);
        }

        reason = cached.reason ?? string.Empty;
        return cached.met;
    }

    private bool CanPayCostCached(GeneVariantDef variant, out string reason)
    {
        reason = string.Empty;
        if (variant == null || !variant.HasCost)
        {
            return true;
        }

        if (!costCache.TryGetValue(variant, out var cached))
        {
            cached.affordable = variant.CanPayCost(pawn, out cached.reason);
            costCache.Add(variant, cached);
        }

        reason = cached.reason ?? string.Empty;
        return cached.affordable;
    }

    private bool IsInSnapshot(GeneVariantDef variant)
    {
        if (variant == null)
        {
            return false;
        }

        foreach (var choice in snapshot)
        {
            if (choice.gene == selectedSlot && choice.variant == variant)
            {
                return true;
            }
        }

        return false;
    }

    private string VariantTooltip(GeneVariantDef variant, GeneVariantDef current, bool isCurrent, bool hasRequirements, string lockedReason, bool affordable, string costReason)
    {
        var builder = new StringBuilder();
        if (variant == null)
        {
            builder.AppendLine("BEWH.Framework.GeneCustomization.NoneTooltip".Translate());
            var lines = GeneVariantStatUtility.StatDeltaLines(selectedSlot, current, null);
            if (lines.Count > 0)
            {
                builder.AppendLine();
                foreach (var line in lines)
                {
                    builder.AppendLine(line);
                }
            }
        }
        else
        {
            builder.AppendLine(variant.TooltipDescription(pawn, selectedSlot, current));
        }

        if (isCurrent)
        {
            builder.AppendLine();
            builder.AppendLine("BEWH.Framework.GeneCustomization.CurrentlyApplied".Translate());
        }

        if (!hasRequirements && !lockedReason.NullOrEmpty())
        {
            builder.AppendLine();
            builder.AppendLine(lockedReason.TrimEndNewlines());
        }

        if (!affordable && !costReason.NullOrEmpty())
        {
            builder.AppendLine();
            builder.AppendLine(costReason);
        }

        return builder.ToString().TrimEndNewlines();
    }

    //Colour

    private void DrawColourOptions(Rect rect, GeneVariantDef variant)
    {
        var settings = comp.SettingsFor(selectedSlot);
        if (settings == null)
        {
            return;
        }

        var colorAmount = variant.colorAmount;
        if (settings.maskDef != null && !settings.maskDef.setsNull)
        {
            colorAmount = settings.maskDef.colorAmount;
        }
        colorAmount = Mathf.Clamp(colorAmount, 1, 3);

        var boxRect = rect.TopPartPixels(30f);
        var boxWidth = boxRect.width / colorAmount;
        for (var i = 0; i < colorAmount; i++)
        {
            var colourRect = new Rect(boxRect.x + boxWidth * i, boxRect.y, boxWidth, boxRect.height);
            var colour = i switch
            {
                0 => settings.Color,
                1 => settings.ColorTwo,
                _ => settings.ColorThree,
            };
            DrawColourBox(colourRect, colour, i + 1);
        }

        var buttonRect = new Rect(rect.x, boxRect.yMax + 4f, rect.width, rect.yMax - boxRect.yMax - 4f);
        var hasMasks = GeneVariantIndex.MasksFor(variant).Count > 1;
        var presetRect = hasMasks ? buttonRect.LeftHalf().ContractedBy(1f) : buttonRect.ContractedBy(1f);

        TooltipHandler.TipRegion(presetRect, "BEWH.Framework.Customization.ColorPresetShortDesc".Translate());
        if (Widgets.ButtonText(presetRect, "BEWH.Framework.Customization.DecorationPreset".Translate()))
        {
            SelectPreset(variant);
        }

        if (!hasMasks)
        {
            return;
        }

        var maskRect = buttonRect.RightHalf().ContractedBy(1f);
        TooltipHandler.TipRegion(maskRect, "BEWH.Framework.Customization.MaskDesc".Translate());
        if (Widgets.ButtonText(maskRect, "BEWH.Framework.Customization.Mask".Translate()))
        {
            SelectMask(variant);
        }
    }

    private void DrawColourBox(Rect colourRect, Color currentColour, int colourIndex)
    {
        colourRect = colourRect.ContractedBy(2f);
        Widgets.DrawMenuSection(colourRect);
        colourRect = colourRect.ContractedBy(1f);
        Widgets.DrawRectFast(colourRect, currentColour);
        TooltipHandler.TipRegion(colourRect, "BEWH.Framework.Customization.ChooseCustomColour".Translate());
        if (!Widgets.ButtonInvisible(colourRect))
        {
            return;
        }

        var slot = selectedSlot;
        Find.WindowStack.Add(new Dialog_ColourPicker(currentColour, newColour =>
        {
            recacheMaterials = true;
            comp.SetColour(slot, colourIndex, newColour);
        }));
    }

    private void SelectPreset(GeneVariantDef variant)
    {
        var settings = comp.SettingsFor(selectedSlot);
        if (settings == null)
        {
            return;
        }

        var slot = selectedSlot;
        var list = new List<FloatMenuOption>();
        var colorAmount = variant.colorAmount;
        if (settings.maskDef is { setsNull: false })
        {
            colorAmount = settings.maskDef.colorAmount;
        }

        foreach (var preset in variant.availablePresets)
        {
            list.Add(new FloatMenuOption(preset.label, delegate
            {
                recacheMaterials = true;
                comp.SetColours(slot, preset.colour, preset.colourTwo ?? Color.white, preset.colourThree ?? Color.white);
            }, Core40kUtils.ThreeColourPreview(preset.colour, preset.colourTwo, preset.colourThree, colorAmount), Color.white));
        }

        var skin = comp.SourceColour(GeneVariantColourSource.Skin, Color.white);
        list.Add(new FloatMenuOption("BEWH.Framework.GeneCustomization.UseSkinColour".Translate(), delegate
        {
            recacheMaterials = true;
            comp.SetColours(slot, skin, skin, skin);
        }, Core40kUtils.ThreeColourPreview(skin, skin, skin, colorAmount), Color.white));

        var hair = comp.SourceColour(GeneVariantColourSource.Hair, Color.white);
        list.Add(new FloatMenuOption("BEWH.Framework.GeneCustomization.UseHairColour".Translate(), delegate
        {
            recacheMaterials = true;
            comp.SetColours(slot, hair, hair, hair);
        }, Core40kUtils.ThreeColourPreview(hair, hair, hair, colorAmount), Color.white));

        var sourced = comp.SourceColour(variant.defaultColourSource, Color.white);
        var col1 = variant.defaultColour ?? sourced;
        var col2 = variant.defaultColourTwo ?? sourced;
        var col3 = variant.defaultColourThree ?? sourced;
        list.Add(new FloatMenuOption("BEWH.Framework.Customization.SetDefaultColor".Translate(), delegate
        {
            recacheMaterials = true;
            comp.SetDefaultColours(slot);
        }, Core40kUtils.ThreeColourPreview(col1, col2, col3, colorAmount), Color.white));

        Find.WindowStack.Add(new FloatMenu(list));
    }

    private void SelectMask(GeneVariantDef variant)
    {
        var settings = comp.SettingsFor(selectedSlot);
        if (settings == null)
        {
            return;
        }

        if (recacheMaterials)
        {
            cachedMaterials.Clear();
            recacheMaterials = false;
        }

        var slot = selectedSlot;
        var list = new List<FloatMenuOption>();
        foreach (var mask in GeneVariantIndex.MasksFor(variant))
        {
            if (!cachedMaterials.TryGetValue((variant, mask), out var material))
            {
                var path = variant.texPath;
                var shader = mask.setsNull ? Core40kDefOf.BEWH_CutoutThreeColor.Shader : mask.shaderType?.Shader ?? variant.shaderType.Shader;
                var graphic = MultiColorUtils.GetGraphic<Graphic_Multi>(path, shader, Vector2.one, settings.Color, settings.ColorTwo, settings.ColorThree, null, mask.maskPath ?? path + "_mask");
                material = graphic?.MatSouth ?? BaseContent.BadMat;
                cachedMaterials.Add((variant, mask), material);
            }

            var captured = material;
            var menuOption = new FloatMenuOptionMask(mask.label, delegate
            {
                recacheMaterials = true;
                comp.SetMask(slot, mask);
            }, null, Color.white, extraPartRightJustified: true, extraPartWidth: 100f, mouseoverGuiAction: delegate(Rect previewRect)
            {
                Widgets.DrawMenuSection(previewRect);
                Graphics.DrawTexture(previewRect, captured.mainTexture, captured);
            });

            if (settings.maskDef == mask)
            {
                menuOption.Disabled = true;
            }

            list.Add(menuOption);
        }

        if (list.Count == 0)
        {
            list.Add(new FloatMenuOptionMask("NoneBrackets".Translate(), null));
        }

        Find.WindowStack.Add(new FloatMenuMask(list));
    }

    //Sets

    private void DrawSets(Rect rect)
    {
        Text.Font = GameFont.Medium;
        var headerRect = rect.TopPartPixels(Text.LineHeight);
        Widgets.Label(headerRect, "BEWH.Framework.GeneCustomization.SetBonuses".Translate());
        Text.Font = GameFont.Small;

        var listRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.yMax - headerRect.yMax - 2f);
        if (relevantSets.Count == 0)
        {
            GUI.color = Color.gray;
            Widgets.Label(listRect, "BEWH.Framework.GeneCustomization.NoSets".Translate());
            GUI.color = Color.white;
            return;
        }

        var rowHeight = Text.LineHeight + 4f;
        var viewRect = new Rect(0f, 0f, listRect.width - 16f, relevantSets.Count * rowHeight);
        Widgets.BeginScrollView(listRect, ref setScroll, viewRect);

        var y = 0f;
        foreach (var set in relevantSets)
        {
            var rowRect = new Rect(0f, y, viewRect.width, rowHeight);
            DrawSetRow(rowRect, set);
            y += rowHeight;
        }

        Widgets.EndScrollView();
    }

    private void DrawSetRow(Rect rowRect, GeneVariantSetDef set)
    {
        Widgets.DrawHighlightIfMouseover(rowRect);
        var count = comp.SetCount(set);
        var total = set.variants.Count;
        var anyActive = comp.ActiveSets.Contains(set);

        Text.Anchor = TextAnchor.MiddleLeft;
        var x = rowRect.x + 2f;
        if (set.Icon != null)
        {
            var iconRect = new Rect(x, rowRect.y + 2f, rowRect.height - 4f, rowRect.height - 4f);
            Widgets.DrawTextureFitted(iconRect, set.Icon, 1f);
            x = iconRect.xMax + 4f;
        }

        var labelRect = new Rect(x, rowRect.y, 220f, rowRect.height);
        GUI.color = anyActive ? Color.white : Color.gray;
        Widgets.Label(labelRect, $"{set.LabelCap} {count}/{total}");
        GUI.color = Color.white;

        x = labelRect.xMax + 8f;
        foreach (var tier in set.tiers)
        {
            var label = tier.label.NullOrEmpty() ? "BEWH.Framework.GeneCustomization.TierPieces".Translate(tier.requiredCount).ToString() : tier.label;
            var width = Text.CalcSize(label).x + 12f;
            var tierRect = new Rect(x, rowRect.y + 2f, width, rowRect.height - 4f);
            var active = comp.IsTierActive(tier);

            Widgets.DrawBoxSolid(tierRect, active ? new Color(0.2f, 0.5f, 0.2f, 0.6f) : new Color(0.3f, 0.3f, 0.3f, 0.5f));
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = active ? Color.white : Color.gray;
            Widgets.Label(tierRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleLeft;

            var tierDescription = tier.Description();
            var tierTip = "BEWH.Framework.GeneCustomization.TierRequires".Translate(tier.requiredCount).ToString();
            if (!tierDescription.NullOrEmpty())
            {
                tierTip += "\n\n" + tierDescription;
            }
            TooltipHandler.TipRegion(tierRect, tierTip);

            x = tierRect.xMax + 6f;
        }
        Text.Anchor = TextAnchor.UpperLeft;

        var tooltip = new StringBuilder();
        tooltip.AppendLine(set.LabelCap);
        if (!set.description.NullOrEmpty())
        {
            tooltip.AppendLine();
            tooltip.AppendLine(set.description);
        }
        tooltip.AppendLine();
        tooltip.AppendLine("BEWH.Framework.GeneCustomization.SetMembers".Translate());
        foreach (var variant in set.variants)
        {
            if (variant == null)
            {
                continue;
            }

            var applied = comp.ActiveVariants.Contains(variant);
            tooltip.AppendLine("BEWH.Framework.Customization.AppendedLabel".Translate(variant.LabelCap + (applied ? " ✓" : string.Empty)));
        }
        TooltipHandler.TipRegion(labelRect, tooltip.ToString().TrimEndNewlines());
    }

    //Buttons

    private void DrawBottomButtons(Rect inRect)
    {
        if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - ButSize.y, ButSize.x, ButSize.y), "Cancel".Translate()))
        {
            Close();
        }

        if (Widgets.ButtonText(new Rect(inRect.xMin + inRect.width / 2f - ButSize.x / 2f, inRect.yMax - ButSize.y, ButSize.x, ButSize.y), "Reset".Translate()))
        {
            Reset();
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(inRect.xMax - ButSize.x, inRect.yMax - ButSize.y, ButSize.x, ButSize.y), "Accept".Translate()))
        {
            Accept();
        }
    }

    private void Reset()
    {
        foreach (var geneDef in slots)
        {
            if (SlotUnlocked(geneDef, out _))
            {
                comp.SetVariant(geneDef, null);
            }
        }

        recacheMaterials = true;
    }

    private List<GeneVariantChoice> NewChoices()
    {
        var result = new List<GeneVariantChoice>();
        foreach (var choice in comp.Choices)
        {
            if (choice.variant == null)
            {
                continue;
            }

            var unchanged = false;
            foreach (var old in snapshot)
            {
                if (old.gene == choice.gene && old.variant == choice.variant)
                {
                    unchanged = true;
                    break;
                }
            }

            if (!unchanged)
            {
                result.Add(choice);
            }
        }

        return result;
    }

    private void Accept()
    {
        var newChoices = NewChoices();
        foreach (var choice in newChoices)
        {
            if (choice.variant.CanPayCost(pawn, out var reason))
            {
                continue;
            }

            Messages.Message("BEWH.Framework.GeneCustomization.CannotPay".Translate(choice.variant.LabelCap, reason ?? string.Empty), pawn, MessageTypeDefOf.RejectInput, false);
            return;
        }

        foreach (var choice in newChoices)
        {
            choice.variant.PayCost(pawn);
        }

        accepted = true;
        comp.Reconcile();
        Close();
    }

    public override void Close(bool doCloseSound = true)
    {
        if (!accepted && comp != null && snapshot != null)
        {
            comp.Restore(snapshot);
        }

        base.Close(doCloseSound);
    }
}
