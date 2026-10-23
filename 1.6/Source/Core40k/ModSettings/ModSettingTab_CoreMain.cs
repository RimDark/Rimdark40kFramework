using System;
using UnityEngine;
using Verse;

namespace Core40k;

public class ModSettingTab_CoreMain : ModSettingTab
{
    public override void DrawTab(Rect inRect, ModSettings settings)
    {
        if (settings is not Core40kModSettings core40KModSettings)
        {
            Log.Error("Settings not correct type");
            return;
        }
        
        var viewRect = new Rect(inRect.x, inRect.y, inRect.width - 16f, scrollViewHeight);
        scrollViewHeight = 0f;
            
        Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(viewRect);
        listingStandard.Gap(36);
        scrollViewHeight += ListingHeightIncreaseGap;
        scrollViewHeight += ListingHeightIncrease;
        
        listingStandard.CheckboxLabeled("BEWH.Framework.ModSettings.ConfirmRankUnlock".Translate(), ref core40KModSettings.confirmRankUnlock);

        scrollViewHeight += ListingHeightIncrease;

        listingStandard.CheckboxLabeled("BEWH.Framework.ModSettings.NotifyRankEligibility".Translate(), ref core40KModSettings.notifyOnRankEligibility);

        scrollViewHeight += ListingHeightIncrease;

        listingStandard.CheckboxLabeled("BEWH.Framework.ModSettings.NotifyGeneVariantAvailability".Translate(), ref core40KModSettings.notifyOnGeneVariantAvailability);

        scrollViewHeight += ListingHeightIncrease;

        listingStandard.CheckboxLabeled("BEWH.Framework.ModSettings.AlwaysShowShields".Translate(), ref core40KModSettings.alwaysShowShields, "BEWH.Framework.ModSettings.AlwaysShowShieldsTooltip".Translate());

        scrollViewHeight += ListingHeightIncrease;

        listingStandard.Label("BEWH.Framework.ModSettings.RepairCostFactor".Translate(core40KModSettings.repairCostFactor.ToStringPercent()), -1f, "BEWH.Framework.ModSettings.RepairCostFactorTooltip".Translate());
        scrollViewHeight += ListingHeightIncrease;

        var repairCostFactor = listingStandard.Slider(core40KModSettings.repairCostFactor, 0f, 2f);
        core40KModSettings.repairCostFactor = (float)Math.Round(repairCostFactor * 20f) / 20f;
        scrollViewHeight += ListingHeightIncrease;

        if (listingStandard.ButtonText("BEWH.Framework.CommonKeyword.Reset".Translate(), null, 0.25f))
        {
            core40KModSettings.repairCostFactor = Core40kModSettings.DefaultRepairCostFactor;
        }
        scrollViewHeight += ListingHeightIncreaseGap;
        
        //Check VEF patches
        listingStandard.GapLine(36);
        scrollViewHeight += ListingHeightIncreaseGap;
        listingStandard.Label("\n" + "BEWH.ModSettings.CheckVEFPatches".Translate());
        scrollViewHeight += ListingHeightIncrease;
        
        scrollViewHeight += ListingHeightIncrease;
        
        listingStandard.End();
        Widgets.EndScrollView();
    }
}