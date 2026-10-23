using RimWorld;
using Verse;

namespace Core40k;

/// <summary>
/// Applies a hediff the first time the carrier goes down or bleeds past a threshold, then waits out
/// a rearm delay before it can fire again.
/// </summary>
public class HediffComp_AutoInjector : HediffComp
{
    private const int CheckIntervalTicks = 60;

    private int rearmedOnTick = -1;

    private HediffCompProperties_AutoInjector Props => (HediffCompProperties_AutoInjector)props;

    private bool Armed => rearmedOnTick < 0 || Find.TickManager.TicksGame >= rearmedOnTick;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref rearmedOnTick, "rearmedOnTick", -1);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        if (Props.hediffToApply == null || Pawn.Dead || Pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!Pawn.IsHashIntervalTick(CheckIntervalTicks) || !Armed || !ShouldTrigger())
        {
            return;
        }

        Trigger();
    }

    private bool ShouldTrigger()
    {
        if (Pawn.health.hediffSet.HasHediff(Props.hediffToApply))
        {
            return false;
        }

        if (Props.triggerOnDowned && Pawn.Downed && !(Props.ignoreDownedWhileAnesthetized && Pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic)))
        {
            return true;
        }

        if (Props.triggerOnBloodLoss < 0f)
        {
            return false;
        }

        var bloodLoss = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        return bloodLoss != null && bloodLoss.Severity >= Props.triggerOnBloodLoss;
    }

    private void Trigger()
    {
        Pawn.health.AddHediff(Props.hediffToApply);
        rearmedOnTick = Find.TickManager.TicksGame + Props.rearmDelayTicks;

        if (Props.triggeredMessage.NullOrEmpty() || Pawn.Faction is not { IsPlayer: true })
        {
            return;
        }

        Messages.Message(Props.triggeredMessage.Translate(Pawn.LabelShortCap), Pawn, MessageTypeDefOf.NeutralEvent);
    }
}
