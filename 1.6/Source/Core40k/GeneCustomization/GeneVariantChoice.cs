using Verse;

namespace Core40k;

public class GeneVariantChoice : IExposable
{
    public GeneDef gene;
    public GeneVariantDef variant;
    public DecorationSettings settings = new();

    public GeneVariantChoice()
    {
    }

    public GeneVariantChoice(GeneDef gene, GeneVariantDef variant)
    {
        this.gene = gene;
        this.variant = variant;
    }

    public GeneVariantChoice(GeneVariantChoice other)
    {
        gene = other.gene;
        variant = other.variant;
        settings = new DecorationSettings(other.settings);
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref gene, "gene");
        Scribe_Defs.Look(ref variant, "variant");
        Scribe_Deep.Look(ref settings, "settings");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            settings ??= new DecorationSettings();
        }
    }
}
