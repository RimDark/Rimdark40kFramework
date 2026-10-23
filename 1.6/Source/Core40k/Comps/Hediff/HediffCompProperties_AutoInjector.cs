using Verse;

namespace Core40k;

public class HediffCompProperties_AutoInjector : HediffCompProperties
{
    public HediffDef hediffToApply = null;

    public bool triggerOnDowned = true;

    public bool ignoreDownedWhileAnesthetized = true;

    //Blood loss severity that sets the injector off on a pawn still on its feet. Negative disables it.
    public float triggerOnBloodLoss = 0.6f;

    public int rearmDelayTicks = 60000;

    public string triggeredMessage = null;

    public HediffCompProperties_AutoInjector()
    {
        compClass = typeof(HediffComp_AutoInjector);
    }
}
