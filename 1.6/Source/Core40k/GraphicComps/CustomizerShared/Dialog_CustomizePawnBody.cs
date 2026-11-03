using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Core40k;

/// <summary>
/// Paints a non-humanlike pawn's body (CompMultiColorPawn). Opened from the pawn's paint gizmo; changes are free
/// and applied straight away on Accept.
/// </summary>
public class Dialog_CustomizePawnBody : Window, ICustomizationDialog
{
    public bool Closed { get; private set; }

    private Pawn pawn;

    private Vector2 scrollPosition;

    private static readonly Vector2 ButSize = new Vector2(200f, 40f);

    public override Vector2 InitialSize => new Vector2(950f, 750f);

    private Dictionary<CustomizationTabDef, CustomizerTabDrawer> tabDrawers = [];
    private Dictionary<CustomizationTabDef, TabRecord> tabRecords = [];
    private readonly List<TabRecord> visibleTabRecords = [];

    private CustomizationTabDef curTab;

    private string cachedTitle;
    private string Title => cachedTitle ??= "BEWH.Framework.Customization.PaintPawn".Translate().CapitalizeFirst() + ": " + pawn.LabelShortCap;

    public Dialog_CustomizePawnBody()
    {
    }

    public Dialog_CustomizePawnBody(Pawn pawn)
    {
        this.pawn = pawn;

        if (pawn == null)
        {
            Closed = true;
            return;
        }

        foreach (var tabDef in CustomizationTabResolver.TabsFor(pawn.def))
        {
            if (!tabRecords.ContainsKey(tabDef))
            {
                var tabRecord = new TabRecord(tabDef.label, delegate
                {
                    curTab = tabDef;
                }, () => curTab == tabDef);
                tabRecords.Add(tabDef, tabRecord);
            }

            if (!tabDrawers.ContainsKey(tabDef))
            {
                var tabDrawer = (CustomizerTabDrawer)Activator.CreateInstance(tabDef.tabDrawerClass);
                tabDrawer.Setup(pawn);
                tabDrawers.Add(tabDef, tabDrawer);
            }
        }

        curTab = tabRecords.Keys.FirstOrFallback();
        Find.TickManager.Pause();
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (pawn == null || pawn.Destroyed || curTab == null || !tabDrawers.ContainsKey(curTab))
        {
            Close();
            return;
        }

        RefreshVisibleTabs();

        Text.Font = GameFont.Medium;
        var rect = new Rect(inRect)
        {
            height = Text.LineHeight * 2f
        };
        Widgets.Label(rect, Title);
        Text.Font = GameFont.Small;
        inRect.yMin = rect.yMax + 4f;
        var rect2 = inRect;
        rect2.width *= 0.3f;
        rect2.yMax -= ButSize.y + 4f;
        DrawPawn(rect2);
        var rect3 = inRect;
        rect3.xMin = rect2.xMax + 10f;
        rect3.yMax -= ButSize.y + 4f;
        Widgets.DrawMenuSection(rect3);
        TabDrawer.DrawTabs(rect3, visibleTabRecords);
        rect3 = rect3.ContractedBy(18f);

        tabDrawers[curTab].DrawTab(rect3, pawn, ref scrollPosition);

        DrawBottomButtons(inRect);
    }

    private void RefreshVisibleTabs()
    {
        visibleTabRecords.Clear();
        CustomizationTabDef firstVisible = null;
        var curTabVisible = false;
        foreach (var pair in tabRecords)
        {
            if (!tabDrawers[pair.Key].IsVisible(pawn))
            {
                continue;
            }

            firstVisible ??= pair.Key;
            curTabVisible |= pair.Key == curTab;
            visibleTabRecords.Add(pair.Value);
        }

        if (!curTabVisible && firstVisible != null)
        {
            curTab = firstVisible;
        }
    }

    //Four views like the armour dialog: west, south, east, north from the top. The materials are drawn straight from
    //the painted body graphic, so a mirrored side (no west texture) is flipped here as it is on the map.
    private void DrawPawn(Rect rect)
    {
        var multiColor = pawn.GetComp<CompMultiColorPawn>();
        var graphic = multiColor?.BodyGraphicFor(CompMultiColorPawn.VanillaBodyGraphic(pawn));

        //The column spans exactly the colouring tab's box: the top and bottom views sit flush with its edges,
        //with the gaps only between views.
        const float gap = 8f;
        var viewHeight = (rect.height - gap * 3f) / 4f;

        Widgets.BeginGroup(rect);
        for (var i = 0; i < 4; i++)
        {
            var position = new Rect(0f, (viewHeight + gap) * i, rect.width, viewHeight);
            Widgets.DrawMenuSection(position);

            var rot = new Rot4(3 - i);
            var material = graphic?.MatAt(rot);
            if (material?.mainTexture == null)
            {
                continue;
            }

            var tex = material.mainTexture;
            var flipped = (rot == Rot4.West && graphic.WestFlipped) || (rot == Rot4.East && graphic.EastFlipped);
            var texCoords = flipped ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            Widgets.DrawTextureFitted(position.ContractedBy(1f), tex, 1f, new Vector2(tex.width, tex.height), texCoords, 0f, material);
        }
        Widgets.EndGroup();
    }

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

    public override void Close(bool doCloseSound = true)
    {
        if (closeOnCancel || closeOnClickedOutside)
        {
            Reset();
        }

        foreach (var tab in tabDrawers)
        {
            tab.Value.OnClose(pawn, closeOnCancel, closeOnClickedOutside);
        }

        Closed = true;
        base.Close(doCloseSound);
    }

    private void Reset()
    {
        foreach (var tab in tabDrawers)
        {
            tab.Value.OnReset(pawn);
        }

        pawn.Drawer.renderer.SetAllGraphicsDirty();
    }

    //Painting a body is free: no styling-station job, no cost.
    private void Accept()
    {
        DecorationWorkUtility.CommitImmediately(pawn, tabDrawers.Values.SelectMany(tab => tab.Comps).Distinct().ToList());
        Close();
    }
}
