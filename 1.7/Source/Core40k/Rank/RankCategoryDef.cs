using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Core40k;

public class RankCategoryDef : Def
{
    public List<RankCategorySpecificData> ranks = [];
    
    public Dictionary<RankDef, RankCategorySpecificData> rankDict = new Dictionary<RankDef, RankCategorySpecificData>();
    
    public GeneDef unlockedByGene;
    public HediffDef unlockedByHediff;
    public TraitDef unlockedByTrait;
    public GeneDef lockedByGene;
    public HediffDef lockedByHediff;
    public TraitDef lockedByTrait;
    public RoyalTitleRequirement unlockedByTitle;
    public RoyalTitleRequirement lockedByTitle;
    public int unlockTraitDegree = 0;
    public int lockTraitDegree = 0;

    //A pawn must meet all unlockRequirements; meeting any lockRequirement locks the category.
    //Checked after the single unlockedBy/lockedBy fields above.
    public List<PawnRequirement> unlockRequirements = [];
    public List<PawnRequirement> lockRequirements = [];

    //Colour of the bridge ghosts in other categories' trees that lead into this one.
    public Color treeColor = new Color(0.62f, 0.44f, 0.9f);

    [Unsaved]
    private Dictionary<RankDef, List<RankDef>> successors;

    private static readonly List<RankDef> NoRanks = [];

    /// <summary>
    /// Ranks in this category that list the rank in their rankRequirements or rankRequirementsOneAmong, top to bottom.
    /// </summary>
    public List<RankDef> SuccessorsOf(RankDef rank)
    {
        if (successors == null)
        {
            successors = new Dictionary<RankDef, List<RankDef>>();
            foreach (var data in ranks)
            {
                AddSuccessor(data, data.rankRequirements);
                AddSuccessor(data, data.rankRequirementsOneAmong);
            }

            foreach (var list in successors.Values)
            {
                list.SortBy(successor => rankDict[successor].displayPosition.y, successor => rankDict[successor].displayPosition.x);
            }
        }

        return rank != null && successors.TryGetValue(rank, out var result) ? result : NoRanks;
    }

    private void AddSuccessor(RankCategorySpecificData data, List<RankData> requirements)
    {
        if (data?.rankDef == null || requirements == null)
        {
            return;
        }

        foreach (var requirement in requirements)
        {
            if (requirement?.rankDef == null)
            {
                continue;
            }

            if (!successors.TryGetValue(requirement.rankDef, out var list))
            {
                list = [];
                successors[requirement.rankDef] = list;
            }

            if (!list.Contains(data.rankDef))
            {
                list.Add(data.rankDef);
            }
        }
    }

    /// <summary>
    /// The ranks this category requires before the rank (rankRequirements, then rankRequirementsOneAmong).
    /// </summary>
    public List<RankDef> PredecessorsOf(RankDef rank)
    {
        var result = new List<RankDef>();
        if (rank == null || !rankDict.TryGetValue(rank, out var data))
        {
            return result;
        }

        AddPredecessors(data.rankRequirements, result);
        AddPredecessors(data.rankRequirementsOneAmong, result);
        return result;
    }

    private static void AddPredecessors(List<RankData> requirements, List<RankDef> into)
    {
        if (requirements == null)
        {
            return;
        }

        foreach (var requirement in requirements)
        {
            if (requirement?.rankDef != null && !into.Contains(requirement.rankDef))
            {
                into.Add(requirement.rankDef);
            }
        }
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();
        if (ranks.Count != ranks.Distinct().Count())
        {
            Log.Warning(defName + " has duplicate rank categories.");
        }
        rankDict = new Dictionary<RankDef, RankCategorySpecificData>();
        foreach (var rank in ranks)
        {
            rankDict.Add(rank.rankDef, rank);
        }
    }
    [Unsaved]
    private List<PawnRequirement> allUnlockRequirements;

    [Unsaved]
    private List<PawnRequirement> allLockRequirements;

    /// <summary>
    /// Requirements a pawn must all meet to use this category: unlockedByGene/Hediff/Trait/Title, then &lt;unlockRequirements&gt;.
    /// </summary>
    public List<PawnRequirement> AllUnlockRequirements => allUnlockRequirements ??= BuildUnlockRequirements();

    /// <summary>
    /// Requirements that each lock this category when met: lockedByGene/Hediff/Trait/Title, then &lt;lockRequirements&gt;.
    /// </summary>
    public List<PawnRequirement> AllLockRequirements => allLockRequirements ??= BuildLockRequirements();

    private List<PawnRequirement> BuildUnlockRequirements()
    {
        var list = new List<PawnRequirement>();
        AddSingle(list, unlockedByGene, unlockedByHediff, unlockedByTrait, unlockTraitDegree, unlockedByTitle);
        RequirementUtility.AddListed(list, unlockRequirements);
        return list;
    }

    private List<PawnRequirement> BuildLockRequirements()
    {
        var list = new List<PawnRequirement>();
        AddSingle(list, lockedByGene, lockedByHediff, lockedByTrait, lockTraitDegree, lockedByTitle);
        RequirementUtility.AddListed(list, lockRequirements);
        return list;
    }

    private static void AddSingle(List<PawnRequirement> list, GeneDef gene, HediffDef hediff, TraitDef trait, int traitDegree, RoyalTitleRequirement title)
    {
        if (gene != null)
        {
            list.Add(new PawnRequirement_Genes { genes = [gene] });
        }
        if (hediff != null)
        {
            list.Add(new PawnRequirement_Hediffs { hediffs = [hediff] });
        }
        if (trait != null)
        {
            list.Add(new PawnRequirement_Traits { traits = [new TraitData(trait, traitDegree)] });
        }
        if (title != null)
        {
            list.Add(new PawnRequirement_Titles { titles = [title] });
        }
    }

    public bool RankCategoryUnlockedFor(Pawn pawn)
    {
        var context = new RequirementContext(pawn);
        return RequirementUtility.AllMet(AllUnlockRequirements, context) && !RequirementUtility.AnyMet(AllLockRequirements, context);
    }

    /// <summary>
    /// Why the category is locked for this pawn: the unlock requirements when any is missing, and the lock requirements
    /// when any applies, each line coloured. Empty when the category is open.
    /// </summary>
    public string RankCategoryRequirementsNotMetFor(Pawn pawn)
    {
        var context = new RequirementContext(pawn);
        var stringBuilder = new StringBuilder();

        if (!RequirementUtility.AllMet(AllUnlockRequirements, context))
        {
            stringBuilder.Append("BEWH.Framework.RankSystem.CategoryRequiresUnlock".Translate(label.CapitalizeFirst()).Resolve().TrimEnd());
            RequirementUtility.DescribeAll(AllUnlockRequirements, context, new RequirementReport(stringBuilder));
        }

        if (RequirementUtility.AnyMet(AllLockRequirements, context))
        {
            stringBuilder.AppendLineIfNotEmpty();
            stringBuilder.AppendLineIfNotEmpty();
            stringBuilder.Append("BEWH.Framework.RankSystem.CategoryRequiresLock".Translate(label.CapitalizeFirst()).Resolve().TrimEnd());
            //A met lock requirement is the bad outcome, so the colours are flipped.
            var lockReport = new RequirementReport(stringBuilder) { Inverted = true };
            foreach (var requirement in AllLockRequirements)
            {
                if (!requirement.IsEmpty)
                {
                    lockReport.MarkHeaders(requirement.Describe(context, lockReport));
                }
            }
        }

        return stringBuilder.ToString().TrimEndNewlines();
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
        {
            yield return error;
        }

        foreach (var error in RequirementUtility.ConfigErrors(this, unlockRequirements, nameof(unlockRequirements)))
        {
            yield return error;
        }

        foreach (var error in RequirementUtility.ConfigErrors(this, lockRequirements, nameof(lockRequirements)))
        {
            yield return error;
        }

        foreach (var data in ranks)
        {
            foreach (var error in RankBridgeUtility.ConfigErrors(this, data))
            {
                yield return error;
            }
        }
    }
}