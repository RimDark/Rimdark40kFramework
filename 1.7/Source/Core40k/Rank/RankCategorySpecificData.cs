using System.Collections.Generic;
using UnityEngine;

namespace Core40k;

public class RankCategorySpecificData
{
    public RankDef rankDef;
    public Vector2 displayPosition;
    public List<RankData> rankRequirements = [];
    public List<RankData> rankRequirementsOneAmong = [];

    //Links to the same rank placed in other categories; see RankBridge.
    public List<RankBridge> bridges = [];

    private PawnRequirement_Ranks ranksAllRequirement;
    private PawnRequirement_Ranks ranksOneAmongRequirement;

    public PawnRequirement_Ranks RanksAllRequirement => ranksAllRequirement ??= new PawnRequirement_Ranks { ranks = rankRequirements };

    public PawnRequirement_Ranks RanksOneAmongRequirement => ranksOneAmongRequirement ??= new PawnRequirement_Ranks { ranks = rankRequirementsOneAmong, oneAmong = true };
}
