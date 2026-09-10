using UnityEngine;
using Verse;

namespace Core40k;

public class GeneSlotCategoryDef : Def
{
    public float sortOrder = 0f;

    [NoTranslate]
    public string iconPath;

    [Unsaved]
    private Texture2D icon;

    public Texture2D Icon => icon ??= !iconPath.NullOrEmpty() ? ContentFinder<Texture2D>.Get(iconPath) : null;
}
