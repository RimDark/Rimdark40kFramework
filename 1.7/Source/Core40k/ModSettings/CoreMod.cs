using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Core40k;

public abstract class CoreMod : Mod
{
    public virtual ModSettings Settings { get; set; }
    
    protected CoreMod(ModContentPack content) : base(content)
    {
    }
    
    protected List<TabRecord> tabs = [];
    private bool tabsInitialized = false;
    protected ModSettingTab currentSettingTab;
    
    public virtual void InitializeTabs()
    {
        tabsInitialized = true;
    }
    
    public override void DoSettingsWindowContents(Rect inRect)
    {
        if (!tabsInitialized)
        {
            InitializeTabs();
            //Set here too, so an override that skips base doesn't add its tabs again every frame.
            tabsInitialized = true;
        }
        base.DoSettingsWindowContents(inRect);
        var menuRect = inRect.ContractedBy(10f);
        menuRect.y += 20f;
        menuRect.height -= 20f;
        Widgets.DrawMenuSection(menuRect);
        TabDrawer.DrawTabs(menuRect, tabs);
        currentSettingTab?.DrawTab(menuRect.ContractedBy(5f), Settings);
    }
}