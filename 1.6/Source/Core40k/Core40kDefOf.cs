using RimWorld;
using Verse;

namespace Core40k;

[DefOf]
public static class Core40kDefOf
{
    public static DamageDef BEWH_WarpFlame;

    public static MaskDef BEWH_DefaultMask;

    public static JobDef BEWH_OpenStylingStationDialogForApparelMultiColor;
    public static JobDef BEWH_OpenStylingStationDialogForWeaponMultiColor;
    public static JobDef BEWH_ChangeAmmo;

    public static ShaderTypeDef BEWH_CutoutThreeColor;
    
    public static StatDef BEWH_ArtificialPartsAffinityFactor;
    public static StatDef BEWH_RankLearningFactor;

    //Voidfaring stats. Always defined; each is inert unless its expansion or mod is loaded.
    public static StatDef BEWH_GravshipFuelEfficiency;
    public static StatDef BEWH_GravEngineCooldownFactor;
    public static StatDef BEWH_GravshipRangeOffset;
    public static StatDef BEWH_ShipEvasionSkillOffset;
    public static StatDef BEWH_ShipGunnerySkillOffset;
    
    public static JoyKindDef BEWH_RecreationFromSkill;
    
    public static DecorationTypeDef BEWH_UndefinedType;
    public static DecorationTypeDef BEWH_DecoCategory_Internal;

    public static GeneSlotCategoryDef BEWH_GeneSlot_Undefined;

    public static StatDef BEWH_InternalUpgradeSlots;
    
    public static StatCategoryDef BEWH_Voidfaring;
    public static StatCategoryDef BEWH_Customization;
    public static StatCategoryDef BEWH_DecorationOffsets;
    public static StatCategoryDef BEWH_DecorationFactors;
    public static StatCategoryDef BEWH_AlternateTextureOffsets;
    public static StatCategoryDef BEWH_AlternateTextureFactors;
    
    public static BodyPartGroupDef UpperHead;

    static Core40kDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(Core40kDefOf));
    }
}