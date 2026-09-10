using UnityEngine;
using Verse;

namespace Core40k;

[StaticConstructorOnStartup]
public static class GeneCustomizationTex
{
    public static readonly Texture2D GizmoIcon = ContentFinder<Texture2D>.Get("UI/Icons/Genes/GeneBackground_Xenogene", false) ?? BaseContent.BadTex;

    public static readonly Texture2D NoneIcon = ContentFinder<Texture2D>.Get("UI/Widgets/CheckOff", false) ?? BaseContent.BadTex;

    public static readonly Texture2D LockIcon = ContentFinder<Texture2D>.Get("UI/Misc/LockedIcon", false) ?? BaseContent.BadTex;
}
