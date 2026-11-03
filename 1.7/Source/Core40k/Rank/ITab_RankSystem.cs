using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using VEF.Utils;
using Verse;
using Verse.Sound;

namespace Core40k;

[StaticConstructorOnStartup]
public class ITab_RankSystem : ITab
{
    private RankInfoForTab currentlySelectedRank = null;
        
    private RankCategoryDef currentlySelectedRankCategory = null;
        
    private List<RankCategoryDef> availableCategories = [];
        
    private List<RankInfoForTab> availableRanksForCategory = [];

    private Pawn pawn;
        
    private CompRankInfo compRankInfo;

    private bool redoRankInfo = false;

    private TaggedString? noCategorySelectedText;
    private string noneText;
    
    Dictionary<RankDef, Vector2> rankPos = new Dictionary<RankDef, Vector2>();

    private Core40kModSettings modSettings;
    private Core40kModSettings ModSettings => modSettings ??= LoadedModManager.GetMod<Core40kMod>().GetSettings<Core40kModSettings>();
        
    private static readonly CachedTexture LockedIcon = new CachedTexture("UI/Misc/LockedIcon");
        
    const float rankIconRectSize = 40f;
    const float rankIconGapSize = 40f;
    const float rankPlacementMult = rankIconGapSize + rankIconRectSize;
    const float rankIconMargin = 20f;

    //IsVisible runs on every GUI event while a pawn is selected, and the category walk behind it is
    //not cheap, so the answer is held for a short window per selected pawn.
    private const int VisibilityCacheFrames = 30;
    private Pawn visibilityCachedFor;
    private int visibilityCachedFrame = -1;
    private bool visibilityCached;

    public override bool IsVisible
    {
        get
        {
            if (SelPawn == null)
            {
                return false;
            }

            if (Find.Selector?.SingleSelectedThing is not Pawn p)
            {
                return ModSettings?.alwaysShowRankTab ?? false;
            }

            var frame = Time.frameCount;
            if (p == visibilityCachedFor && frame - visibilityCachedFrame < VisibilityCacheFrames && frame >= visibilityCachedFrame)
            {
                return visibilityCached;
            }

            visibilityCachedFor = p;
            visibilityCachedFrame = frame;
            visibilityCached = ComputeVisible(p);
            return visibilityCached;
        }
    }

    private bool ComputeVisible(Pawn p)
    {
        var defaultRes = ModSettings?.alwaysShowRankTab ?? false;
        if (!p.HasComp<CompRankInfo>() || p.Faction == null || !p.Faction.IsPlayer || p.IsSlaveOfColony || p.IsPrisonerOfColony || availableCategories.NullOrEmpty())
        {
            return defaultRes;
        }

        foreach (var rankCategoryDef in availableCategories)
        {
            if (CanView(rankCategoryDef, p))
            {
                return true;
            }
        }

        return defaultRes;
    }

    //God mode opens every category for viewing, whatever its unlock and lock requirements.
    private static bool CanView(RankCategoryDef category, Pawn p)
    {
        return DebugSettings.godMode || category.RankCategoryUnlockedFor(p);
    }

    public ITab_RankSystem()
    {
        labelKey = "BEWH.Framework.RankSystem.RankTab";
        UpdateRankCategoryList();
    }
        
    public override void OnOpen()
    {
        base.OnOpen();
        pawn = SelPawn;

        size = new Vector2(UI.screenWidth, PaneTopY - 100);
        
        compRankInfo = pawn.GetComp<CompRankInfo>();
        if (compRankInfo == null)
        {
            CloseTab();
            return;
        }
        rankPos.Clear();
        cachedGridBounds = null;
        UpdateRankCategoryList();
        if (compRankInfo.LastOpenedRankCategory != null && CanView(compRankInfo.LastOpenedRankCategory, pawn))
        {
            currentlySelectedRankCategory = compRankInfo.LastOpenedRankCategory;
        }
        else
        {
            currentlySelectedRankCategory = availableCategories.FirstOrDefault(availableCategory => availableCategory.RankCategoryUnlockedFor(pawn))
                                            ?? availableCategories.FirstOrDefault(availableCategory => CanView(availableCategory, pawn));
        }
        GetRanksForCategory();
        if (!compRankInfo.UnlockedRanks.NullOrEmpty())
        {
            var highestRank = compRankInfo.HighestRankDef(true) ?? compRankInfo.HighestRankDef(false);
            currentlySelectedRank = availableRanksForCategory.FirstOrFallback(rank => rank.rankDef == highestRank, fallback: null);
        }
        else
        {
            currentlySelectedRank = availableRanksForCategory.FirstOrFallback(rank => rank.rankDef.defaultFirstRank, fallback: null);
        }
    }

    protected override void FillTab()
    {
        if (pawn != SelPawn)
        {
            CloseTab();
            return;
        }

        var font = Text.Font;
        var anchor = Text.Anchor;
            
        var rect = new Rect(Vector2.one * 20f, size - Vector2.one * 40f);
        var rect2 = rect.TakeLeftPart(size.x * 0.25f);
            
        var curY = rect.y;
            
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
            
        //Button to switch between rank categories
        var categoryText = currentlySelectedRankCategory != null
            ? currentlySelectedRankCategory.LabelCap
            : (noCategorySelectedText ??= "BEWH.Framework.RankSystem.NoCategorySelected".Translate());
            
        var categoryTextRect = new Rect(rect2)
        {
            height = 30f
        };
        categoryTextRect.width /= 2;
        categoryTextRect.x += categoryTextRect.width/2;

        curY += categoryTextRect.height;
            
        //Dev Options: reset sits at the far right of the tab, clear of the close button.
        if (Prefs.DevMode)
        {
            var debugResetRankRect = new Rect(rect.xMax - DevButtonWidth - 30f, categoryTextRect.y - 5f, DevButtonWidth, categoryTextRect.height + 10f);

            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(debugResetRankRect,"dev:\nreset ranks"))
            {
                compRankInfo.ResetRanks(currentlySelectedRankCategory);
                redoRankInfo = true;
            }
            Text.Font = GameFont.Medium;
        }

        //Select rank category
        if (Widgets.ButtonText(categoryTextRect,categoryText))
        {
            var list = new List<FloatMenuOption>();
            foreach (var category in availableCategories)
            {
                if (currentlySelectedRankCategory == category)
                {
                    continue;
                }   
                var menuOption = new FloatMenuOption(category.label.CapitalizeFirst(), () => SelectCategory(category), Widgets.PlaceholderIconTex, Color.white);
                if (!category.RankCategoryUnlockedFor(pawn))
                {
                    //Still listed with its requirements in god mode, just selectable.
                    menuOption.tooltip = category.RankCategoryRequirementsNotMetFor(pawn);
                    if (DebugSettings.godMode)
                    {
                        menuOption.Label += " (god mode)";
                    }
                    else
                    {
                        menuOption.Disabled = true;
                    }
                }
                list.Add(menuOption);
            }

            if (list.NullOrEmpty())
            {
                var menuOptionNone = new FloatMenuOption("NoneBrackets".Translate(), null);
                list.Add(menuOptionNone);
            }
            
            Find.WindowStack.Add(new FloatMenu(list));
        }
            
        var toolTip = currentlySelectedRankCategory != null ? currentlySelectedRankCategory.LabelCap.ToString() : (noneText ??= "BEWH.Framework.CommonKeyword.None".Translate().ToString());
        TooltipHandler.TipRegion(categoryTextRect, toolTip);

        curY += 12f;

        if (redoRankInfo)
        {
            GetRanksForCategory();
            redoRankInfo = false;
        }
            
        //Rank info
        var rectRankInfo = new Rect(rect2);
        rectRankInfo.height -= curY;
        rectRankInfo.y = curY;

        Widgets.DrawMenuSection(rectRankInfo);
            
        FillRankInfo(rectRankInfo);
            
        //Rank tree
        var rectRankTree = new Rect(rect)
        {
            xMin = rectRankInfo.xMax,
            yMin = rectRankInfo.yMin,
            yMax = rectRankInfo.yMax,
        };
        rectRankTree.xMin += 50f;

        Widgets.DrawMenuSection(rectRankTree);
        FillRankTree(rectRankTree);
            
        Text.Font = font;
        Text.Anchor = anchor;
    }

    //The display positions the tree covers, ranks and bridge ghosts, in cells. Rebuilt with the rank list.
    private Rect? cachedGridBounds;

    private Rect GridBounds()
    {
        if (cachedGridBounds.HasValue)
        {
            return cachedGridBounds.Value;
        }

        if (currentlySelectedRankCategory == null || availableRanksForCategory.NullOrEmpty())
        {
            cachedGridBounds = Rect.zero;
            return Rect.zero;
        }

        float xMin = 0f, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        void Extend(Vector2 cell)
        {
            xMin = Math.Min(xMin, cell.x);
            yMin = Math.Min(yMin, cell.y);
            xMax = Math.Max(xMax, cell.x);
            yMax = Math.Max(yMax, cell.y);
        }

        foreach (var rank in availableRanksForCategory)
        {
            Extend(currentlySelectedRankCategory.rankDict[rank.rankDef].displayPosition);
        }
        foreach (var ghost in ghosts)
        {
            Extend(ghost.cell);
        }

        cachedGridBounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return cachedGridBounds.Value;
    }

    private const float MinTreeZoom = 0.4f;
    private const float MaxTreeZoom = 2f;
    private const float TreeZoomStep = 1.1f;

    //The furthest the view zooms out on its own when fitting a large tree; the wheel still goes down to MinTreeZoom.
    private const float MinFrameZoom = 0.75f;

    //Pixels a press has to move before it counts as a drag rather than a click.
    private const float DragThreshold = 4f;

    //How much of the tree must stay inside the view when panning.
    private const float TreeKeepVisible = 60f;

    //Where tree position (0, 0) sits inside the tree area, and the zoom; set by FrameTree when the tree is (re)built.
    private Vector2 treePan;
    private float treeZoom = 1f;

    private bool treeDragging;
    private bool treeDragMoved;
    private int treeDragButton;
    private Vector2 treeDragStart;

    //The tree's extent in tree space (unzoomed; (0, 0) is the corner of a rank at display position (0, 0)).
    private Rect TreeBounds()
    {
        var grid = GridBounds();
        return Rect.MinMaxRect(
            grid.xMin * rankPlacementMult - rankIconMargin,
            grid.yMin * rankPlacementMult - rankIconMargin,
            grid.xMax * rankPlacementMult + rankIconRectSize + rankIconMargin,
            grid.yMax * rankPlacementMult + rankIconRectSize + rankIconMargin);
    }

    /// <summary>
    /// Fits the whole tree in the view, never above 1x and never below MinFrameZoom, left-aligned and centred vertically.
    /// A tree too tall to fit at MinFrameZoom is centred on its leftmost column instead, so the starting ranks show.
    /// </summary>
    private void FrameTree(Vector2 viewSize)
    {
        var bounds = TreeBounds();
        var fit = Math.Min(viewSize.x / bounds.width, viewSize.y / bounds.height);
        treeZoom = Mathf.Clamp(fit, MinFrameZoom, 1f);

        var centreY = bounds.center.y;
        if (bounds.height * treeZoom > viewSize.y && !rankPos.NullOrEmpty())
        {
            var leftX = rankPos.Values.Min(pos => pos.x);
            var leftColumn = rankPos.Values.Where(pos => pos.x <= leftX).ToList();
            centreY = (leftColumn.Min(pos => pos.y) + leftColumn.Max(pos => pos.y) + rankIconRectSize) / 2f;
        }

        treePan = new Vector2(-bounds.xMin * treeZoom, viewSize.y / 2f - centreY * treeZoom);
        ClampTreePan(viewSize);
    }

    /// <summary>
    /// Pans so a rank's icon is in the middle of the view, keeping the zoom.
    /// </summary>
    private void CentreOnRank(RankDef rankDef, Vector2 viewSize)
    {
        if (rankDef == null || !rankPos.TryGetValue(rankDef, out var treePosition))
        {
            return;
        }

        var centre = treePosition + new Vector2(rankIconRectSize / 2f, rankIconRectSize / 2f);
        treePan = viewSize / 2f - centre * treeZoom;
        ClampTreePan(viewSize);
    }

    private void ClampTreePan(Vector2 viewSize)
    {
        var bounds = TreeBounds();
        var keepX = Math.Min(TreeKeepVisible, bounds.width * treeZoom);
        var keepY = Math.Min(TreeKeepVisible, bounds.height * treeZoom);
        treePan.x = Mathf.Clamp(treePan.x, keepX - bounds.xMax * treeZoom, viewSize.x - keepX - bounds.xMin * treeZoom);
        treePan.y = Mathf.Clamp(treePan.y, keepY - bounds.yMax * treeZoom, viewSize.y - keepY - bounds.yMin * treeZoom);
    }

    private Vector2 TreeToView(Vector2 treePosition)
    {
        return treePan + treePosition * treeZoom;
    }

    private void FillRankTree(Rect rectRankTree)
    {
        if (currentlySelectedRankCategory == null || availableRanksForCategory.NullOrEmpty())
        {
            return;
        }

        var outRect = rectRankTree.ContractedBy(10f);
        var viewSize = outRect.size;

        //Rank positions in tree space; rebuilt (and the view reframed) when the tab opens or the category changes.
        if (rankPos.NullOrEmpty())
        {
            foreach (var rank in availableRanksForCategory)
            {
                rankPos.TryAdd(rank.rankDef, currentlySelectedRankCategory.rankDict[rank.rankDef].displayPosition * rankPlacementMult);
            }

            FrameTree(viewSize);

            //Opened through a bridge: start on the bridged rank.
            if (pendingFocusRank != null)
            {
                CentreOnRank(pendingFocusRank, viewSize);
                pendingFocusRank = null;
            }
        }

        GUI.BeginGroup(outRect);
        var localRect = new Rect(Vector2.zero, viewSize);
        Widgets.DrawRectFast(localRect, new Color(0f, 0f, 0f, 0.3f));

        //Zoom around the cursor, so the point under it stays put.
        var currentEvent = Event.current;
        if (currentEvent.type == EventType.ScrollWheel && Mouse.IsOver(localRect))
        {
            var mouse = currentEvent.mousePosition;
            var treePoint = (mouse - treePan) / treeZoom;
            var zoom = currentEvent.delta.y > 0f ? treeZoom / TreeZoomStep : treeZoom * TreeZoomStep;
            treeZoom = Mathf.Clamp(zoom, MinTreeZoom, MaxTreeZoom);
            treePan = mouse - treePoint * treeZoom;
            ClampTreePan(viewSize);
            currentEvent.Use();
        }

        //Requirement lines; dashed for "one among" requirements, where any one of the lines is enough.
        var lineWidth = Math.Max(1f, 2f * treeZoom);
        foreach (var rank in availableRanksForCategory)
        {
            var requirementData = currentlySelectedRankCategory.rankDict[rank.rankDef];
            DrawRequirementLines(rank.rankDef, requirementData.rankRequirements, lineWidth, localRect, false);
            var oneAmong = requirementData.rankRequirementsOneAmong;
            DrawRequirementLines(rank.rankDef, oneAmong, lineWidth, localRect, oneAmong is { Count: > 1 });
        }

        var iconSize = rankIconRectSize * treeZoom;
        var clickedGhost = DrawGhosts(localRect, iconSize);

        //Icons
        foreach (var rank in availableRanksForCategory)
        {
            if (!rankPos.TryGetValue(rank.rankDef, out var treePosition))
            {
                continue;
            }

            var rankRect = new Rect(TreeToView(treePosition), new Vector2(iconSize, iconSize));
            if (!rankRect.Overlaps(localRect))
            {
                continue;
            }

            if (rank == currentlySelectedRank)
            {
                Widgets.DrawStrongHighlight(rankRect.ExpandedBy(4f * treeZoom));
            }

            DrawIcon(rankRect, rank.rankDef.RankIcon, true);

            if (!AlreadyUnlocked(rank.rankDef))
            {
                var colour = rank.requirementsMet ? new Color(0f, 0f, 0f, 0.55f) : new Color(0f, 0f, 0f, 0.9f);
                Widgets.DrawRectFast(rankRect, colour);
            }

            if (HasAnyIncompatibleRank(rank.rankDef))
            {
                DrawIcon(rankRect, LockedIcon.Texture, false);
            }

            if (Widgets.ButtonInvisible(rankRect))
            {
                currentlySelectedRank = rank;
            }

            TooltipHandler.TipRegion(rankRect, rank.treeTip ?? rank.rankDef.LabelCap);
        }

        HandleTreeDrag(localRect, viewSize);

        //Controls hint, bottom right
        var font = Text.Font;
        var anchor = Text.Anchor;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.LowerRight;
        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        Widgets.Label(localRect.ContractedBy(4f), "BEWH.Framework.RankSystem.TreeControlsHint".Translate());
        GUI.color = Color.white;
        Text.Font = font;
        Text.Anchor = anchor;

        GUI.EndGroup();

        //Switched after drawing, as it rebuilds the lists drawn above.
        if (clickedGhost != null)
        {
            JumpThroughBridge(clickedGhost);
        }
    }

    /// <summary>
    /// Dragging moves the tree: with the left button from empty space, or the middle or right button anywhere.
    /// A double click on empty space reframes it. Runs after the icons, so a press on an icon goes to its button.
    /// </summary>
    private void HandleTreeDrag(Rect localRect, Vector2 viewSize)
    {
        var currentEvent = Event.current;

        //The button can be let go outside the tab, where the MouseUp never reaches it.
        if (treeDragging && !Input.GetMouseButton(treeDragButton))
        {
            treeDragging = false;
        }

        switch (currentEvent.type)
        {
            case EventType.MouseDown when Mouse.IsOver(localRect):
                if (currentEvent.button == 0 && currentEvent.clickCount == 2)
                {
                    FrameTree(viewSize);
                    currentEvent.Use();
                    break;
                }

                treeDragging = true;
                treeDragMoved = false;
                treeDragButton = currentEvent.button;
                treeDragStart = currentEvent.mousePosition;
                currentEvent.Use();
                break;
            case EventType.MouseDrag when treeDragging:
                if (!treeDragMoved && (currentEvent.mousePosition - treeDragStart).magnitude >= DragThreshold)
                {
                    treeDragMoved = true;
                }

                if (treeDragMoved)
                {
                    treePan += currentEvent.delta;
                    ClampTreePan(viewSize);
                }
                currentEvent.Use();
                break;
            case EventType.MouseUp when treeDragging:
                treeDragging = false;
                currentEvent.Use();
                break;
        }
    }
        
    private bool HasAnyIncompatibleRank(RankDef rankDef)
    {
        var incompatibleRanks = rankDef.incompatibleRanks;
        if (incompatibleRanks == null)
        {
            return false;
        }

        for (var i = 0; i < incompatibleRanks.Count; i++)
        {
            if (compRankInfo.HasRank(incompatibleRanks[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly Vector2 IconCentre = new Vector2(rankIconRectSize / 2f, rankIconRectSize / 2f);

    private void DrawRequirementLines(RankDef rankDef, List<RankData> requirements, float lineWidth, Rect localRect, bool dashed)
    {
        if (requirements == null || !rankPos.TryGetValue(rankDef, out var rankPosition))
        {
            return;
        }

        var startPos = TreeToView(rankPosition + IconCentre);
        foreach (var rankReq in requirements)
        {
            if (rankReq?.rankDef == null || !rankPos.TryGetValue(rankReq.rankDef, out var reqPosition))
            {
                continue;
            }
            var endPos = TreeToView(reqPosition + IconCentre);
            if (!RankTreeLines.MayCross(startPos, endPos, localRect, lineWidth))
            {
                continue;
            }

            var rankUnlocked = compRankInfo.HasRank(rankReq.rankDef) ? Color.white : Color.grey;

            if (currentlySelectedRank != null && currentlySelectedRank.rankDef == rankReq.rankDef)
            {
                rankUnlocked = new Color(0.0f, 0.5f, 1f, 0.9f);
            }

            if (dashed)
            {
                RankTreeLines.DrawDashedLine(startPos, endPos, rankUnlocked, lineWidth, localRect, treeZoom);
            }
            else
            {
                Widgets.DrawLine(startPos, endPos, rankUnlocked, lineWidth);
            }
        }
    }

    private List<RankTreeGhost> ghosts = [];

    //Set when a bridge opens another category, to centre the view on the bridged rank once it is laid out.
    private RankDef pendingFocusRank;

    private const int MaxGhostsPerBridge = 3;

    //Where a bridge's ghost column may go, in cells from the rank, tried in order; the first spot clear of ranks and
    //other ghosts wins. Portals lead on to the right, entrances come in from the left.
    private static readonly Vector2[] PortalGhostOffsets = [new(1.5f, 0f), new(1.5f, -0.5f), new(1.5f, 0.5f), new(0f, -1.5f), new(0f, 1.5f)];
    private static readonly Vector2[] EntranceGhostOffsets = [new(-1.5f, 0f), new(-1.5f, -0.5f), new(-1.5f, 0.5f), new(0f, -1.5f), new(0f, 1.5f)];

    //Ghosts closer than this many cells on both axes overlap.
    private const float GhostSeparation = 0.6f;

    private static readonly Color GhostVeil = new Color(0f, 0f, 0f, 0.6f);

    /// <summary>
    /// Lays out the ghosts of every bridged rank in the open category. Runs with GetRanksForCategory, not every frame.
    /// </summary>
    private void BuildGhosts()
    {
        ghosts = [];
        var category = currentlySelectedRankCategory;
        foreach (var data in category.ranks)
        {
            if (data?.rankDef == null)
            {
                continue;
            }

            foreach (var bridge in RankBridgeUtility.BridgesOf(category, data.rankDef))
            {
                var targets = bridge.role == RankBridgeRole.Portal ? bridge.category.SuccessorsOf(data.rankDef) : bridge.category.PredecessorsOf(data.rankDef);
                if (targets.Count == 0)
                {
                    continue;
                }

                var shown = Math.Min(targets.Count, MaxGhostsPerBridge);
                var column = GhostColumn(data.displayPosition, shown, bridge);
                var locked = !bridge.category.RankCategoryUnlockedFor(pawn);
                var tip = GhostTip(bridge, targets, locked);
                for (var i = 0; i < shown; i++)
                {
                    ghosts.Add(new RankTreeGhost
                    {
                        rankDef = targets[i],
                        anchor = data.rankDef,
                        target = bridge.category,
                        cell = column + new Vector2(0f, i),
                        accent = bridge.category.treeColor,
                        targetLocked = locked,
                        overflow = i == shown - 1 ? targets.Count - shown : 0,
                        tip = tip,
                    });
                }
            }
        }
    }

    //The cell of the top ghost of a bridge's column; the column is centred on the chosen spot.
    private Vector2 GhostColumn(Vector2 anchor, int count, RankBridge bridge)
    {
        var centring = new Vector2(0f, -(count - 1) / 2f);
        if (bridge.ghostOffset.HasValue)
        {
            return anchor + bridge.ghostOffset.Value + centring;
        }

        var offsets = bridge.role == RankBridgeRole.Portal ? PortalGhostOffsets : EntranceGhostOffsets;
        foreach (var offset in offsets)
        {
            var top = anchor + offset + centring;
            var free = true;
            for (var i = 0; i < count && free; i++)
            {
                free = IsCellFree(top + new Vector2(0f, i));
            }

            if (free)
            {
                return top;
            }
        }

        return anchor + offsets[0] + centring;
    }

    private bool IsCellFree(Vector2 cell)
    {
        foreach (var data in currentlySelectedRankCategory.ranks)
        {
            if (data != null && Overlaps(data.displayPosition, cell))
            {
                return false;
            }
        }

        foreach (var ghost in ghosts)
        {
            if (Overlaps(ghost.cell, cell))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Overlaps(Vector2 a, Vector2 b)
    {
        return Math.Abs(a.x - b.x) < GhostSeparation && Math.Abs(a.y - b.y) < GhostSeparation;
    }

    private string GhostTip(RankBridge bridge, List<RankDef> targets, bool locked)
    {
        var label = bridge.category.LabelCap;
        var builder = new StringBuilder();
        builder.Append((bridge.role == RankBridgeRole.Portal ? "BEWH.Framework.RankSystem.BridgeContinuesIn" : "BEWH.Framework.RankSystem.BridgeLeadsFrom").Translate(label));
        foreach (var target in targets)
        {
            builder.AppendLine().Append("    ").Append(target.LabelCap);
        }

        builder.AppendLine().AppendLine();
        builder.Append(locked ? LockedReason(bridge.category) : "BEWH.Framework.RankSystem.BridgeClickHint".Translate(label).Resolve());
        return builder.ToString();
    }

    private string LockedReason(RankCategoryDef category)
    {
        var reason = category.RankCategoryRequirementsNotMetFor(pawn);
        return reason.NullOrEmpty() ? "BEWH.Framework.RankSystem.BridgeCategoryLocked".Translate(category.LabelCap).Resolve() : reason;
    }

    /// <summary>
    /// Draws the ghosts and the dashed links to their ranks, and returns the one clicked, if any.
    /// </summary>
    private RankTreeGhost DrawGhosts(Rect localRect, float iconSize)
    {
        if (ghosts.Count == 0)
        {
            return null;
        }

        var lineWidth = Math.Max(1.5f, 3f * treeZoom);

        //Links first, so no link crosses over another ghost's icon.
        foreach (var ghost in ghosts)
        {
            if (!rankPos.TryGetValue(ghost.anchor, out var anchorPosition))
            {
                continue;
            }

            var ghostRect = GhostRect(ghost, iconSize);
            var start = TreeToView(anchorPosition + IconCentre);
            var end = BorderPoint(ghostRect.ExpandedBy(2f * treeZoom), start);
            if (RankTreeLines.MayCross(start, end, localRect, lineWidth))
            {
                RankTreeLines.DrawDashedLine(start, end, GhostColour(ghost, ghostRect), lineWidth, localRect, treeZoom);
            }
        }

        RankTreeGhost clicked = null;
        var outline = Math.Max(1, Mathf.RoundToInt(2f * treeZoom));
        foreach (var ghost in ghosts)
        {
            var ghostRect = GhostRect(ghost, iconSize);
            if (!ghostRect.Overlaps(localRect))
            {
                continue;
            }

            var colour = GhostColour(ghost, ghostRect);
            DrawIcon(ghostRect, ghost.rankDef.RankIcon, true);
            Widgets.DrawRectFast(ghostRect, GhostVeil);
            Widgets.DrawBoxSolidWithOutline(ghostRect.ExpandedBy(2f * treeZoom), Color.clear, colour, outline);
            if (ghost.targetLocked)
            {
                DrawIcon(ghostRect, LockedIcon.Texture, false);
            }

            if (Mouse.IsOver(ghostRect))
            {
                DrawGhostCaption(ghostRect, ghost, colour);
            }

            if (Widgets.ButtonInvisible(ghostRect))
            {
                clicked = ghost;
            }

            TooltipHandler.TipRegion(ghostRect, ghost.tip);
        }

        return clicked;
    }

    private Rect GhostRect(RankTreeGhost ghost, float iconSize)
    {
        return new Rect(TreeToView(ghost.cell * rankPlacementMult), new Vector2(iconSize, iconSize));
    }

    private static Color GhostColour(RankTreeGhost ghost, Rect ghostRect)
    {
        var accent = ghost.accent;
        return Mouse.IsOver(ghostRect) ? accent : new Color(accent.r, accent.g, accent.b, accent.a * 0.45f);
    }

    //The category's name under a hovered ghost, and how many more ranks the bridge leads to.
    private static void DrawGhostCaption(Rect ghostRect, RankTreeGhost ghost, Color colour)
    {
        var font = Text.Font;
        var anchor = Text.Anchor;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        GUI.color = colour;

        var label = ghost.target.LabelCap.ToString();
        if (ghost.overflow > 0)
        {
            label += "\n" + "BEWH.Framework.RankSystem.BridgeMore".Translate(ghost.overflow);
        }
        var size = Text.CalcSize(label);
        Widgets.Label(new Rect(ghostRect.center.x - size.x / 2f - 2f, ghostRect.yMax + 2f, size.x + 4f, size.y), label);

        GUI.color = Color.white;
        Text.Font = font;
        Text.Anchor = anchor;
    }

    //Where a line from inside the rect towards a point leaves the rect.
    private static Vector2 BorderPoint(Rect rect, Vector2 towards)
    {
        var direction = towards - rect.center;
        if (direction == Vector2.zero)
        {
            return rect.center;
        }

        var scaleX = direction.x == 0f ? float.MaxValue : rect.width / 2f / Math.Abs(direction.x);
        var scaleY = direction.y == 0f ? float.MaxValue : rect.height / 2f / Math.Abs(direction.y);
        return rect.center + direction * Math.Min(Math.Min(scaleX, scaleY), 1f);
    }

    private void JumpThroughBridge(RankTreeGhost ghost)
    {
        if (!CanView(ghost.target, pawn))
        {
            Messages.Message(LockedReason(ghost.target), MessageTypeDefOf.RejectInput, false);
            return;
        }

        SelectCategory(ghost.target, ghost.anchor);
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }

    /// <summary>
    /// Opens a category in the tree, optionally with a rank selected and the view centred on it.
    /// </summary>
    private void SelectCategory(RankCategoryDef category, RankDef focusRank = null)
    {
        currentlySelectedRankCategory = category;
        compRankInfo.OpenedRankCategory(category);
        GetRanksForCategory();
        currentlySelectedRank = focusRank == null ? null : availableRanksForCategory.FirstOrDefault(rank => rank.rankDef == focusRank);
        rankPos.Clear();
        pendingFocusRank = focusRank;
    }

    private Vector2 scrollPosRankInfo;
    private float scrollViewHeightRankInfo = 0f;
    private void FillRankInfo(Rect rect)
    {
        var rectRankInfo = new Rect(rect);
        rectRankInfo = rectRankInfo.ContractedBy(20f);
        
        if (currentlySelectedRank != null)
        {
            var viewRect = new Rect(rectRankInfo.x, rectRankInfo.y, rectRankInfo.width - 16f,
                Math.Max(scrollViewHeightRankInfo, rectRankInfo.height));
            
            var listingRankInfo = new Listing_Standard
            {
                maxOneColumn = true,
            };
            //Start
            Widgets.BeginScrollView(rectRankInfo, ref scrollPosRankInfo, viewRect);
            listingRankInfo.Begin(viewRect);
                
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
                
            //Name
            listingRankInfo.Gap(5);
            var rankLabel = currentlySelectedRank.rankDef.label.CapitalizeFirst();
            var rankLabelRect = listingRankInfo.Label(rankLabel);

            if (Prefs.DevMode)
            {
                DrawRankDevButtons(rankLabelRect);
            }

            //Show day as rank
            var rankDayAmount = compRankInfo.GetDaysAsRank(currentlySelectedRank.rankDef);
            if (rankDayAmount > 0f)
            {
                listingRankInfo.Gap(5);
                Text.Font = GameFont.Small;
                listingRankInfo.Label("BEWH.Framework.RankSystem.DaysSinceRankGiven".Translate(rankDayAmount));
                Text.Font = GameFont.Medium;
            }
            //Unlock button
            else if (currentlySelectedRank.requirementsMet && !AlreadyUnlocked(currentlySelectedRank.rankDef))
            {   
                listingRankInfo.Gap();
                listingRankInfo.Indent(viewRect.width * 0.25f);
                if (listingRankInfo.ButtonText("BEWH.Framework.RankSystem.UnlockRank".Translate(), widthPct: 0.5f))
                {
                    var rankToUnlock = currentlySelectedRank.rankDef;
                    void Action()
                    {
                        if (RankUtils.IsLimited(rankToUnlock) && !compRankInfo.GameComponentRankInfo.CanHaveMoreOfRank(rankToUnlock))
                        {
                            Messages.Message("BEWH.Framework.RankSystem.RankLimitReached".Translate(rankToUnlock.label), pawn, MessageTypeDefOf.RejectInput, false);
                            redoRankInfo = true;
                            return;
                        }

                        UnlockRank(rankToUnlock);
                    }
                    if (ModSettings.confirmRankUnlock)
                    {
                        var window = Dialog_MessageBox.CreateConfirmation("BEWH.Framework.RankSystem.UnlockRankConfirm".Translate(pawn, currentlySelectedRank.rankDef.label), Action, destructive: true);
                        Find.WindowStack.Add(window);
                    }
                    else
                    {
                        Action();
                    }
                }
                listingRankInfo.Outdent(viewRect.width * 0.25f);
            }
                
            listingRankInfo.GapLine(1f);
            listingRankInfo.Indent(viewRect.width * 0.02f);
            
            //Description
            listingRankInfo.Gap();
            Text.Anchor = TextAnchor.UpperLeft;
            listingRankInfo.Label("BEWH.Framework.RankSystem.RankDescription".Translate());
            Text.Font = GameFont.Small;
            listingRankInfo.Label(currentlySelectedRank.rankDef.description);

            //Requirements
            listingRankInfo.Gap();
            Text.Font = GameFont.Medium;
            listingRankInfo.Label("BEWH.Framework.RankSystem.RankRequirements".Translate());
            Text.Font = GameFont.Small;
            listingRankInfo.Label(currentlySelectedRank.rankText);
                
            //Given stats
            listingRankInfo.Gap();
            Text.Font = GameFont.Medium;
            listingRankInfo.Label("BEWH.Framework.RankSystem.RankBonuses".Translate());
            Text.Font = GameFont.Small;
            listingRankInfo.Label(currentlySelectedRank.rankBonusText);

            //End
            listingRankInfo.Outdent(viewRect.width * 0.02f);
            scrollViewHeightRankInfo = listingRankInfo.CurHeight + rankIconMargin;
            listingRankInfo.End();
            Widgets.EndScrollView();
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            var text = "BEWH.Framework.RankSystem.NoRankSelected".Translate();
            Widgets.Label(rectRankInfo, text);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    private void UpdateRankCategoryList()
    {
        availableCategories = DefDatabase<RankCategoryDef>.AllDefsListForReading;
    }

    private void GetRanksForCategory()
    {
        availableRanksForCategory = [];
        ghosts = [];
        cachedGridBounds = null;

        if (currentlySelectedRankCategory == null)
        {
            return;
        }
        
        foreach (var rank in currentlySelectedRankCategory.ranks)
        {
            var rankInfo = BuildRankInfoForCategory(rank.rankDef);
            availableRanksForCategory.Add(rankInfo);
        }

        BuildGhosts();
    }

    private RankInfoForTab BuildRankInfoForCategory(RankDef rankDef)
    {
        var reqMet = rankDef.RequirementMet(pawn, compRankInfo, currentlySelectedRankCategory, out var reason);
        
        return new RankInfoForTab
        {
            rankDef = rankDef,
            requirementsMet = reqMet,
            rankText = reason,
            rankBonusText = rankDef.GetRankBonusString(),
            treeTip = BuildTreeTip(rankDef),
        };
    }

    //The rank's name, plus the other categories it is bridged to.
    private string BuildTreeTip(RankDef rankDef)
    {
        var bridges = RankBridgeUtility.BridgesOf(currentlySelectedRankCategory, rankDef);
        if (bridges.NullOrEmpty())
        {
            return rankDef.LabelCap;
        }

        var builder = new StringBuilder(rankDef.LabelCap);
        foreach (var bridge in bridges)
        {
            var key = bridge.role == RankBridgeRole.Portal ? "BEWH.Framework.RankSystem.BridgeAlsoIn" : "BEWH.Framework.RankSystem.BridgeReachedFrom";
            builder.AppendLine().Append(key.Translate(bridge.category.LabelCap));
        }
        return builder.ToString();
    }
        
    private bool AlreadyUnlocked(RankDef rankDef)
    {
        return compRankInfo != null && compRankInfo.HasRank(rankDef);
    }

    private static void DrawIcon(Rect inRect, Texture2D icon, bool drawBg)
    {
        var color = Mouse.IsOver(inRect) ? GenUI.MouseoverColor : Color.white;
        GUI.color = color;
        if (drawBg)
        {
            GUI.DrawTexture(inRect, Command.BGTexShrunk);
        }
        GUI.color = Color.white;
        GUI.DrawTexture(inRect, icon);
    }
        
    private void UnlockRank(RankDef rank)
    {
        compRankInfo.UnlockRank(rank);
        redoRankInfo = true;
    }

    private const float DevButtonWidth = 80f;
    private const float DevDayButtonWidth = 48f;

    /// <summary>
    /// Dev buttons beside the rank name: days held (left, only for a held rank) and a toggle that gives or removes the rank (right).
    /// </summary>
    private void DrawRankDevButtons(Rect rankLabelRect)
    {
        var rankDef = currentlySelectedRank.rankDef;
        var held = AlreadyUnlocked(rankDef);

        var font = Text.Font;
        var anchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;

        var buttonHeight = rankLabelRect.height + 10f;
        var buttonY = rankLabelRect.y - 5f;

        var toggleRect = new Rect(rankLabelRect.xMax - DevButtonWidth, buttonY, DevButtonWidth, buttonHeight);
        if (Widgets.ButtonText(toggleRect, held ? "dev:\nremove rank" : "dev:\ngive rank"))
        {
            if (held)
            {
                compRankInfo.RemoveRank(rankDef, true);
                redoRankInfo = true;
            }
            else
            {
                UnlockRank(rankDef);
            }
        }

        if (held)
        {
            //Shift changes days by 10 instead of 1.
            var step = Event.current.shift ? 10 : 1;
            var removeRect = new Rect(rankLabelRect.x, buttonY, DevDayButtonWidth, buttonHeight);
            var addRect = new Rect(removeRect.xMax + 4f, buttonY, DevDayButtonWidth, buttonHeight);
            if (Widgets.ButtonText(removeRect, "dev:\n-" + step + "d"))
            {
                ChangeDaysAsRank(rankDef, -step);
            }
            if (Widgets.ButtonText(addRect, "dev:\n+" + step + "d"))
            {
                ChangeDaysAsRank(rankDef, step);
            }
            TooltipHandler.TipRegion(removeRect, "Remove days held (10 with shift)");
            TooltipHandler.TipRegion(addRect, "Add days held (10 with shift)");
        }

        Text.Font = font;
        Text.Anchor = anchor;
    }

    private void ChangeDaysAsRank(RankDef rankDef, int days)
    {
        compRankInfo.SetDaysAsRank(rankDef, Math.Max(0f, compRankInfo.GetDaysAsRank(rankDef) + days));
        redoRankInfo = true;
    }
}

internal class RankInfoForTab
{
    public RankDef rankDef;
    public bool requirementsMet;
    public string rankText;
    public string rankBonusText;
    public string treeTip;
}

/// <summary>
/// A greyed copy of a rank from another category, beside a bridged rank: where its tree continues (portal) or what
/// leads into it there (entrance). Clicking it opens that category on the bridged rank.
/// </summary>
internal class RankTreeGhost
{
    public RankDef rankDef;
    public RankDef anchor;
    public RankCategoryDef target;
    public Vector2 cell;
    public Color accent;
    public bool targetLocked;
    //On the last ghost of a bridge: how many more ranks it leads to than are shown.
    public int overflow;
    public string tip;
}
