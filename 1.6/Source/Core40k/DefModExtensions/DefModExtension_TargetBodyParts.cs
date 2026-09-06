using System.Collections.Generic;
using Verse;

namespace Core40k;

public class DefModExtension_TargetBodyParts : DefModExtension
{
    public List<BodyPartDef> bodyParts;
    public List<BodyPartGroupDef> bodyPartGroups;
    public float chance = 1f;
    public bool weightByCoverage;

    public bool Matches(BodyPartRecord part)
    {
        if (bodyParts != null && bodyParts.Contains(part.def))
        {
            return true;
        }

        if (bodyPartGroups == null || part.groups == null)
        {
            return false;
        }

        foreach (var group in part.groups)
        {
            if (bodyPartGroups.Contains(group))
            {
                return true;
            }
        }

        return false;
    }
}