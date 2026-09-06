using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class CompWeaponDecoration : CompDecorativeBase
{
    public CompProperties_WeaponDecoration Props => (CompProperties_WeaponDecoration)props;
    public override void InitialSetup()
    {
        ApplyDecorationsFromList(Props.decorations, free: true);
        base.InitialSetup();
    }
    
    public override void Reset()
    {
        cachedGraphics = [];
        cachedPlacements = null;
        base.Reset();
        InvalidateToolsAndVerbs();
    }

    public override void Notify_GraphicChanged()
    {
        //Rebuilt lazily by the Graphics getter; one user action can raise this several times.
        recacheGraphics = true;
        cachedPlacements = null;
        base.Notify_GraphicChanged();
    }

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        if (Decorations.Count == 0 || !GroundDecorationRenderer.Enabled)
        {
            return;
        }
        RenderWeaponAttachments.DrawOnGround(this, drawLoc);
    }

    public override void PostPrintOnto(SectionLayer layer)
    {
        if (Decorations.Count == 0 || !GroundDecorationRenderer.Enabled)
        {
            return;
        }
        RenderWeaponAttachments.PrintOnGround(this, layer);
    }
    
    public bool recacheGraphics = true;
    private Dictionary<DecorationDef, Graphic> cachedGraphics = [];
    public Dictionary<DecorationDef, Graphic> Graphics
    {
        get
        {
            if (recacheGraphics)
            {
                RecacheDecorationGraphics();
            }

            return cachedGraphics ??= new Dictionary<DecorationDef, Graphic>();
        }
    }
    public void RecacheDecorationGraphics()
    {
        recacheGraphics = false;
        cachedGraphics = [];
        cachedPlacements = null;
        var sortedGraphics = Decorations.Keys.ToList();
        if (!sortedGraphics.NullOrEmpty())
        {
            sortedGraphics.SortBy(def => GetLayerForDeco(def, parent));
        }
        foreach (var weaponDecoration in sortedGraphics)
        {
            if (weaponDecoration == null)
            {
                continue;
            }
            
            if (!weaponDecoration.HasVisual)
            {
                continue;
            }

            var settings = decorations[weaponDecoration];

            var mask = settings.maskDef ?? weaponDecoration.defaultMask;
            var usesMask = mask is { setsNull: false };
            var maskPath = usesMask && !mask.maskPath.NullOrEmpty() ? mask.maskPath : null;
            var colorAmount = usesMask ? mask.colorAmount : weaponDecoration.colorAmount;

            Graphic graphic;
            if (colorAmount > 2)
            {
                graphic = MultiColorUtils.GetGraphic<Graphic_Single>(
                    weaponDecoration.drawnTextureIconPath,
                    Core40kDefOf.BEWH_CutoutThreeColor.Shader,
                    weaponDecoration.drawSize,
                    settings.Color,
                    settings.ColorTwo,
                    settings.ColorThree,
                    null,
                    maskPath);
            }
            else
            {
                graphic = GraphicDatabase.Get<Graphic_Single>(
                    weaponDecoration.drawnTextureIconPath,
                    (usesMask ? mask.shaderType?.Shader : null) ?? weaponDecoration.shaderType?.Shader ?? ShaderTypeDefOf.Cutout.Shader,
                    weaponDecoration.drawSize,
                    settings.Color,
                    settings.ColorTwo,
                    null,
                    maskPath);
            }

            cachedGraphics.Add(weaponDecoration, graphic);
        }
    }

    private List<WeaponDecorationPlacement>[] cachedPlacements;

    /// <summary>
    /// The material, offset, size and layer of every drawn decoration at the given rotation. Built
    /// once per rotation and dropped whenever the graphics are recached, so the per-frame draw is a
    /// plain loop over a list.
    /// </summary>
    public List<WeaponDecorationPlacement> PlacementsFor(Rot4 rotation)
    {
        var graphics = Graphics;
        cachedPlacements ??= new List<WeaponDecorationPlacement>[4];

        var rotationIndex = rotation.IsValid ? rotation.AsInt : Rot4.South.AsInt;
        var placements = cachedPlacements[rotationIndex];
        if (placements != null)
        {
            return placements;
        }

        placements = [];
        var weaponDefName = parent.def.defName;
        foreach (var decoCompGraphic in graphics)
        {
            if (decoCompGraphic.Key is not WeaponDecorationDef weaponDecoration)
            {
                continue;
            }
            var material = decoCompGraphic.Value?.MatSingle;
            if (material == null)
            {
                continue;
            }

            var offset = Vector3.zero;
            var drawSize = weaponDecoration.drawSize;
            var layer = weaponDecoration.layerPlacement;
            if (weaponDecoration.weaponSpecificDrawData != null && weaponDecoration.weaponSpecificDrawData.TryGetValue(weaponDefName, out var value))
            {
                offset = value.OffsetForRot(rotation);
                drawSize *= value.scale;
                layer = value.LayerForRot(rotation, layer);
            }
            else if (weaponDecoration.drawData != null)
            {
                offset = weaponDecoration.drawData.OffsetForRot(rotation);
                drawSize *= weaponDecoration.drawData.scale;
            }

            if (drawDatas.TryGetValue(weaponDecoration, out var drawData))
            {
                offset += drawData.defaultData.offset;
                drawSize *= drawData.defaultData.scale;
                layer += drawData.defaultData.layer;
            }

            placements.Add(new WeaponDecorationPlacement(material, offset, drawSize, layer));
        }

        cachedPlacements[rotationIndex] = placements;
        return placements;
    }
    
    private List<Tool> cachedTools;
    private List<VerbProperties> cachedVerbProperties;
    private bool toolsAndVerbsCached;

    public List<Tool> DecoratedTools
    {
        get
        {
            RecacheToolsAndVerbs();
            return cachedTools;
        }
    }

    public List<VerbProperties> DecoratedVerbProperties
    {
        get
        {
            RecacheToolsAndVerbs();
            return cachedVerbProperties;
        }
    }

    public bool AnyDecorationChangesToolsOrVerbs => Decorations.Keys.OfType<WeaponDecorationDef>().Any(deco => deco.ChangesToolsOrVerbs);

    private bool verbModifiersCached;
    private int cachedAdditionalBurstShotCount;
    private float cachedAdditionalRange;
    private float cachedAdditionalWarmupTime;

    public int AdditionalBurstShotCount
    {
        get
        {
            RecacheVerbModifiers();
            return cachedAdditionalBurstShotCount;
        }
    }

    public float AdditionalRange
    {
        get
        {
            RecacheVerbModifiers();
            return cachedAdditionalRange;
        }
    }

    public float AdditionalWarmupTime
    {
        get
        {
            RecacheVerbModifiers();
            return cachedAdditionalWarmupTime;
        }
    }

    /// <summary>
    /// Sums the verb modifiers of the fitted decorations once, so the Verb getter postfixes read a
    /// field instead of walking the decoration dictionary on every call.
    /// </summary>
    private void RecacheVerbModifiers()
    {
        if (verbModifiersCached)
        {
            return;
        }

        verbModifiersCached = true;
        cachedAdditionalBurstShotCount = 0;
        cachedAdditionalRange = 0f;
        cachedAdditionalWarmupTime = 0f;

        foreach (var decoration in Decorations.Keys)
        {
            if (decoration is not WeaponDecorationDef { verbModifier: not null } weaponDecoration)
            {
                continue;
            }

            cachedAdditionalBurstShotCount += weaponDecoration.verbModifier.additionalBurstShotCount;
            cachedAdditionalRange += weaponDecoration.verbModifier.additionalRange;
            cachedAdditionalWarmupTime += weaponDecoration.verbModifier.additionalWarmupTime;
        }
    }

    public void InvalidateToolsAndVerbs()
    {
        verbModifiersCached = false;
        toolsAndVerbsCached = false;
        cachedTools = null;
        cachedVerbProperties = null;

        var equippable = parent.GetComp<CompEquippable>();
        if (equippable?.verbTracker == null)
        {
            return;
        }

        equippable.verbTracker.VerbsNeedReinitOnLoad();

        if (parent.ParentHolder is not Pawn_EquipmentTracker { pawn: not null } tracker)
        {
            return;
        }

        foreach (var verb in equippable.AllVerbs)
        {
            verb.caster = tracker.pawn;
        }
    }

    private void RecacheToolsAndVerbs()
    {
        if (toolsAndVerbsCached)
        {
            return;
        }

        toolsAndVerbsCached = true;
        cachedTools = null;
        cachedVerbProperties = null;
        
        var relevantDecorations = Decorations.Keys
            .OfType<WeaponDecorationDef>()
            .Where(deco => deco.ChangesToolsOrVerbs)
            .OrderBy(deco => deco.defName)
            .ToList();

        if (relevantDecorations.NullOrEmpty())
        {
            return;
        }

        var disabledTools = new List<string>();
        var disabledVerbs = new List<string>();
        var disableAllTools = false;

        foreach (var decoration in relevantDecorations)
        {
            disableAllTools |= decoration.disablesAllWeaponTools;
            if (!decoration.disablesWeaponTools.NullOrEmpty())
            {
                disabledTools.AddRange(decoration.disablesWeaponTools);
            }
            if (!decoration.disablesWeaponVerbs.NullOrEmpty())
            {
                disabledVerbs.AddRange(decoration.disablesWeaponVerbs);
            }
        }

        var tools = new List<Tool>();
        if (!parent.def.tools.NullOrEmpty() && !disableAllTools)
        {
            foreach (var tool in parent.def.tools)
            {
                if (!tool.MatchesAny(disabledTools))
                {
                    tools.Add(tool);
                }
            }
        }

        var verbProperties = new List<VerbProperties>();
        foreach (var verb in parent.def.Verbs)
        {
            if (!verb.MatchesAny(disabledVerbs))
            {
                verbProperties.Add(verb);
            }
        }

        foreach (var decoration in relevantDecorations)
        {
            if (!decoration.tools.NullOrEmpty())
            {
                foreach (var tool in decoration.tools)
                {
                    tools.Add(WeaponDecorationVerbUtility.CopyTool(tool));
                }
            }
            if (!decoration.verbs.NullOrEmpty())
            {
                verbProperties.AddRange(decoration.verbs);
            }
        }

        cachedTools = tools;
        cachedVerbProperties = verbProperties;
    }

    //Decoration changes
    public override void AddDecoration(DecorationDef decoration, DecorationSettings decorationSettings = null, bool setDefaultColors = false, bool free = false)
    {
        base.AddDecoration(decoration, decorationSettings, setDefaultColors, free);
        InvalidateToolsAndVerbs();
    }

    public override bool RemoveDecoration(DecorationDef decoration)
    {
        if (!base.RemoveDecoration(decoration))
        {
            return false;
        }
        InvalidateToolsAndVerbs();
        return true;
    }

    public override void RemoveAllDecorations()
    {
        base.RemoveAllDecorations();
        InvalidateToolsAndVerbs();
    }

    protected override void OnDecorationsChanged()
    {
        base.OnDecorationsChanged();
        InvalidateToolsAndVerbs();
    }

    public override void RemoveInvalidDecorations(Pawn pawn)
    {
        var before = Decorations.Count;
        base.RemoveInvalidDecorations(pawn);
        if (Decorations.Count != before)
        {
            InvalidateToolsAndVerbs();
        }
    }

    public override void RemoveDecorationsIncompatibleWithAlternate(AlternateBaseFormDef alternateBaseFormDef)
    {
        var before = Decorations.Count;
        base.RemoveDecorationsIncompatibleWithAlternate(alternateBaseFormDef);
        if (Decorations.Count != before)
        {
            InvalidateToolsAndVerbs();
        }
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
    {
        foreach (var statDrawEntry in base.SpecialDisplayStats())
        {
            yield return statDrawEntry;
        }

        var addedTools = Decorations.Keys
            .OfType<WeaponDecorationDef>()
            .Where(deco => !deco.tools.NullOrEmpty())
            .OrderBy(deco => deco.defName)
            .ToList();

        if (addedTools.NullOrEmpty())
        {
            yield break;
        }

        var report = new StringBuilder();
        var count = 0;
        foreach (var decoration in addedTools)
        {
            report.AppendLine(decoration.LabelCap + ":");
            foreach (var tool in decoration.tools)
            {
                count++;
                report.AppendLine("  " + WeaponDecorationVerbUtility.ToolSummary(tool));
            }
            report.AppendLine();
        }

        yield return new StatDrawEntry(
            StatCategoryDefOf.Weapon_Melee,
            "BEWH.Framework.Customization.AddedMeleeAttacks".Translate(),
            count.ToString(),
            report.ToString().TrimEndNewlines(),
            5000);
    }

    private float GetLayerForDeco(DecorationDef decoDef, Thing eq)
    {
        if (decoDef is not WeaponDecorationDef weaponDecoDef)
        {
            return 0f;
        }
        var layer = weaponDecoDef.layerPlacement;
        if (eq.ParentHolder is not Pawn_EquipmentTracker equipmentTracker)
        {
            return layer;
        }
        if (weaponDecoDef.weaponSpecificDrawData != null && weaponDecoDef.weaponSpecificDrawData.TryGetValue(eq.def.defName, out var value))
        {
            layer = value.LayerForRot(equipmentTracker.pawn.Rotation, layer);
        }
        if (drawDatas.TryGetValue(weaponDecoDef, out var drawData))
        {
            layer += drawData.defaultData.layer;
        }

        return layer;
    }
    
    public override void PostExposeData()
    {
        if (Scribe.mode != LoadSaveMode.Saving || !weaponDecorations.NullOrEmpty() || !originalWeaponDecorations.NullOrEmpty())
        {
            //TODO: Remove at later point
            Scribe_Collections.Look(ref originalWeaponDecorations, "originalWeaponDecorations");
            Scribe_Collections.Look(ref weaponDecorations, "weaponDecorations");
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit && (!weaponDecorations.NullOrEmpty() || !originalWeaponDecorations.NullOrEmpty()))
        {
            FixDecos();
        }
        base.PostExposeData();
        
        if (Scribe.mode == LoadSaveMode.PostLoadInit && AnyDecorationChangesToolsOrVerbs)
        {
            InvalidateToolsAndVerbs();
        }
    }
    
    [Obsolete]
    private Dictionary<WeaponDecorationDef, ExtraDecorationSettings> originalWeaponDecorations = new ();
    [Obsolete]
    private Dictionary<WeaponDecorationDef, ExtraDecorationSettings> weaponDecorations = new ();
    [Obsolete]
    private void FixDecos()
    {
        decorations ??= new Dictionary<DecorationDef, DecorationSettings>();
        originalDecorations ??= new Dictionary<DecorationDef, DecorationSettings>();
        foreach (var weapDecos in weaponDecorations)
        {
            decorations.SetOrAdd(weapDecos.Key, weapDecos.Value);
        }
        foreach (var orgWeapDecos in originalWeaponDecorations)
        {
            originalDecorations.SetOrAdd(orgWeapDecos.Key, orgWeapDecos.Value);
        }

        weaponDecorations = new Dictionary<WeaponDecorationDef, ExtraDecorationSettings>();
        originalWeaponDecorations = new Dictionary<WeaponDecorationDef, ExtraDecorationSettings>();
    }
}

public readonly struct WeaponDecorationPlacement
{
    public readonly Material material;
    public readonly Vector3 offset;
    public readonly Vector2 drawSize;
    public readonly float layer;

    public WeaponDecorationPlacement(Material material, Vector3 offset, Vector2 drawSize, float layer)
    {
        this.material = material;
        this.offset = offset;
        this.drawSize = drawSize;
        this.layer = layer;
    }
}
