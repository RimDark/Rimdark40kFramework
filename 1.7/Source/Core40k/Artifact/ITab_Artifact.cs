using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class ITab_Artifact : ITab
{
    private Vector2 scrollPosition;
    private float viewHeight = 1000f;

    private CompArtifactWeapon Comp => ArtifactWeaponUtility.ArtifactOf(SelThing);

    public ITab_Artifact()
    {
        size = new Vector2(440f, 480f);
        labelKey = "BEWH.Framework.Artifact.Tab";
    }

    public override bool IsVisible => Comp is { IsArtifact: true };

    protected override void FillTab()
    {
        var comp = Comp;
        if (comp == null || !comp.IsArtifact)
        {
            return;
        }

        var outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
        var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

        var listing = new Listing_Standard();
        listing.Begin(viewRect);

        Text.Font = GameFont.Medium;
        listing.Label(comp.parent.LabelCap);
        Text.Font = GameFont.Small;
        listing.Label(comp.Kind.LabelCap + " - " + comp.parent.def.LabelCap);
        listing.GapLine();

        //Stage
        listing.Label("BEWH.Framework.Artifact.Stage".Translate(comp.Stage + 1, comp.Kind.stages.Count));
        var next = comp.NextStage;
        if (next != null)
        {
            listing.Label("BEWH.Framework.Artifact.NextStage".Translate());
            var wielder = comp.Wielder;
            foreach (var condition in next.conditions)
            {
                if (condition == null)
                {
                    continue;
                }
                var met = condition.IsMet(comp, wielder);
                var line = "  " + condition.Description(comp);
                listing.Label(met ? line.Colorize(ColorLibrary.Green) : line);
            }
        }
        if (!comp.WielderMeetsRequirements)
        {
            listing.Label("BEWH.Framework.Artifact.WielderUnworthy".Translate().Colorize(ColorLibrary.RedReadable));
            if (!RequirementData.IsNullOrEmpty(comp.Kind.wielderRequirements))
            {
                comp.Kind.wielderRequirements.Has(comp.Wielder, out var unworthyReport);
                listing.Label(unworthyReport);
            }
        }
        listing.GapLine();

        //Traits
        listing.Label("BEWH.Framework.Artifact.Traits".Translate());
        foreach (var trait in comp.Traits)
        {
            if (trait == null)
            {
                continue;
            }
            var label = "  " + trait.LabelCap;
            if (trait.isDrawback)
            {
                label += " " + "BEWH.Framework.Artifact.DrawbackSuffix".Translate();
            }
            if (!comp.TraitActive(trait))
            {
                label += " " + "BEWH.Framework.Artifact.DormantShort".Translate();
                label = label.Colorize(Color.gray);
            }
            var rect = listing.Label(label);
            if (!trait.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, trait.description);
            }
        }
        listing.GapLine();

        //Kills
        var kills = comp.Kills;
        listing.Label("BEWH.Framework.Artifact.KillsHeader".Translate(kills.total));
        var killLines = new StringBuilder();
        AppendKillLine(killLines, ArtifactKillCategory.Humanlike, kills);
        AppendKillLine(killLines, ArtifactKillCategory.Animal, kills);
        AppendKillLine(killLines, ArtifactKillCategory.Mechanoid, kills);
        AppendKillLine(killLines, ArtifactKillCategory.Entity, kills);
        AppendKillLine(killLines, ArtifactKillCategory.Other, kills);
        if (killLines.Length > 0)
        {
            listing.Label(killLines.ToString().TrimEndNewlines());
        }
        listing.GapLine();

        //Bearers
        listing.Label("BEWH.Framework.Artifact.Bearers".Translate());
        for (var i = comp.Bearers.Count - 1; i >= 0; i--)
        {
            var bearer = comp.Bearers[i];
            if (bearer == null)
            {
                continue;
            }
            var current = i == comp.Bearers.Count - 1 && comp.Wielder != null;
            var line = "  " + (bearer.pawnName ?? "?") + " - " + "BEWH.Framework.Artifact.BearerKills".Translate(bearer.kills);
            if (current)
            {
                line += " " + "BEWH.Framework.Artifact.CurrentBearer".Translate();
            }
            listing.Label(line);
        }

        listing.End();
        if (Event.current.type == EventType.Layout)
        {
            viewHeight = listing.CurHeight + 20f;
        }
        Widgets.EndScrollView();
    }

    private static void AppendKillLine(StringBuilder sb, ArtifactKillCategory category, ArtifactKillRecord kills)
    {
        var count = kills.CountOf(category);
        if (count > 0)
        {
            sb.AppendLine("  " + ArtifactKillRecord.LabelOf(category) + ": " + count);
        }
    }
}
