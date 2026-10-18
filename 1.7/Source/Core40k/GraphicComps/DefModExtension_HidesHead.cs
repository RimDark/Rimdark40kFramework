using System.Collections.Generic;
using Verse;

namespace Core40k;

public class DefModExtension_HidesHead : DefModExtension
{
    public List<Rot4> rotations = null;

    /// <summary>
    /// True when the head is hidden for this facing. An empty list hides it on every facing.
    /// </summary>
    public bool HidesFor(Rot4 facing)
    {
        return rotations.NullOrEmpty() || rotations.Contains(facing);
    }
}
