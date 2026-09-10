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