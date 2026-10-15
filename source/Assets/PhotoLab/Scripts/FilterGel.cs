using UnityEngine;

namespace PhotoLab
{
    /// <summary>A colour gel in the rack. Carried to the lamp it slides into the slot under the head.</summary>
    public class FilterGel : BenchItem
    {
        public bool inSlot;
    }

    /// <summary>A stock bottle: bicarbonate, DI water, or the dropper bottle of detergent.</summary>
    public class Bottle : BenchItem
    {
        public Solution solution = Solution.None;
        public bool detergent;
    }

    /// <summary>A specimen on the shelf: what the hole punch is used on.</summary>
    public class Specimen : BenchItem
    {
        public string specimenId = "spinach";
    }

    /// <summary>The hole punch; carried to a specimen it cuts ten disks.</summary>
    public class HolePunch : BenchItem
    {
    }
}
