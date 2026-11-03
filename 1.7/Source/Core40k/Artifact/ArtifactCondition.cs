using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// One requirement on an artifact stage. Content adds its own with li Class="..." in the stage's conditions list.
/// The wielder may be null when checked outside of equipment (load, dev tools).
/// </summary>
public abstract class ArtifactCondition
{
    public abstract bool IsMet(CompArtifactWeapon comp, Pawn wielder);

    public abstract string Description(CompArtifactWeapon comp);
}

public class ArtifactCondition_KillCount : ArtifactCondition
{
    public int count = 1;

    public override bool IsMet(CompArtifactWeapon comp, Pawn wielder)
    {
        return comp.Kills.total >= count;
    }

    public override string Description(CompArtifactWeapon comp)
    {
        return "BEWH.Framework.Artifact.Condition.Kills".Translate(comp.Kills.total, count);
    }
}

public class ArtifactCondition_KillsOfCategory : ArtifactCondition
{
    public ArtifactKillCategory category = ArtifactKillCategory.Humanlike;
    public int count = 1;

    public override bool IsMet(CompArtifactWeapon comp, Pawn wielder)
    {
        return comp.Kills.CountOf(category) >= count;
    }

    public override string Description(CompArtifactWeapon comp)
    {
        return "BEWH.Framework.Artifact.Condition.KillsOfCategory".Translate(ArtifactKillRecord.LabelOf(category), comp.Kills.CountOf(category), count);
    }
}

public class ArtifactCondition_QualityAtLeast : ArtifactCondition
{
    public QualityCategory quality = QualityCategory.Excellent;

    public override bool IsMet(CompArtifactWeapon comp, Pawn wielder)
    {
        return comp.parent.TryGetQuality(out var current) && current >= quality;
    }

    public override string Description(CompArtifactWeapon comp)
    {
        return "BEWH.Framework.Artifact.Condition.Quality".Translate(quality.GetLabel());
    }
}

public class ArtifactCondition_WielderRequirements : ArtifactCondition
{
    public RequirementData requirements;

    public override bool IsMet(CompArtifactWeapon comp, Pawn wielder)
    {
        return wielder != null && (requirements == null || requirements.Meets(wielder));
    }

    public override string Description(CompArtifactWeapon comp)
    {
        var description = "BEWH.Framework.Artifact.Condition.Wielder".Translate().ToString();
        if (RequirementData.IsNullOrEmpty(requirements))
        {
            return description;
        }

        //What the current wielder still lacks, line by line.
        requirements.Has(comp.Wielder, out var report);
        return description + ":\n" + report;
    }
}

public class ArtifactCondition_StateAtLeast : ArtifactCondition
{
    [NoTranslate]
    public string key;

    public float value = 1f;

    [MustTranslate]
    public string label;

    public override bool IsMet(CompArtifactWeapon comp, Pawn wielder)
    {
        return !key.NullOrEmpty() && comp.GetState(key) >= value;
    }

    public override string Description(CompArtifactWeapon comp)
    {
        return (label.NullOrEmpty() ? key : label) + ": " + comp.GetState(key).ToString("0.#") + " / " + value.ToString("0.#");
    }
}
