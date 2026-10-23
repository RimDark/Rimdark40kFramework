using Verse;

namespace Core40k;

public class DefModExtension_RepairRecipe : DefModExtension
{
    public float materialValueFraction = 0.5f;

    public float workFactor = 0.3f;

    public float minWork = 400f;

    public float maxWork = 8000f;

    public float fallbackWorkToMake = 10000f;

    public int maxCandidates = 20;
}
