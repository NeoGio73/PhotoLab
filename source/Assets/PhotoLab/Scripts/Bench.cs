using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// Sizes and places shared by the scene builder and the running lab. World units are 10 cm, the
    /// hood's work surface is y = 0, and the camera looks along +z (the same conventions as the
    /// other labs, so the fume hood and room copy across unchanged).
    ///
    /// The leaf disk assay bench, left to right:
    ///   specimen shelf (spinach, coleus, seagrass tank) | hole punch and syringe rack |
    ///   the cups under the lamp | PAR meter, timer and the stock bottles.
    /// </summary>
    public static class Bench
    {
        public const float Cm = 0.1f;

        // The lamp stands on a post at the back of the bench; its head slides up and down the post
        // and reaches forward over the row of cups. Distance is measured from the lamp face to the
        // water surface in the cups.
        public const float LampX = 0.3f, LampPostZ = 0.75f;
        public const float LampPostHeightCm = 60f, LampPostRadiusCm = 1.2f;
        public const float LampHeadWidthCm = 14f, LampHeadDepthCm = 9f, LampHeadHeightCm = 4f;
        public const float LampMinDistanceCm = 5f, LampMaxDistanceCm = 50f, LampDefaultDistanceCm = 15f;

        // Four cups in a row under the lamp. Clear plastic, 100 mL. Filled to 50 mL for the assay.
        public const int CupCount = 4;
        public const float CupRowX = 0.3f, CupRowZ = -0.35f;
        public const float CupPitchCm = 10.5f;
        public const float CupRadiusCm = 3.4f, CupHeightCm = 8.0f;
        public const float CupVolumeMl = 100f;
        public const float CupFillMl = 50f;
        public const float DiskRadiusCm = 0.32f, DiskThicknessCm = 0.05f;   // a standard 1/4" hole punch
        public const int DisksPerCup = 10;

        // Filter gels in a rack at the left of the lamp post. One slides into the slot under the
        // lamp head. Order matches PhotosynthesisModel.Filters.
        public const float FilterRackX = -0.85f, FilterRackZ = 0.75f;
        public const float FilterPitchCm = 2.0f, FilterSizeCm = 10f, FilterThicknessCm = 0.15f;

        // Syringes (10 mL) in a rack at the left front, one per cup, with the hole punch beside them.
        public const float SyringeRackX = -2.25f, SyringeRackZ = -1.2f;
        public const float SyringePitchCm = 3.0f, SyringeLengthCm = 11f, SyringeRadiusCm = 0.8f;
        public const float PunchX = -3.4f, PunchZ = -1.35f;
        public const float LeafX = -2.55f, LeafZ = -0.45f;

        // Specimen shelf at the far left back: three stations, left to right.
        public const float ShelfX = -2.9f, ShelfZ = 0.75f;
        public const float ShelfPitchCm = 19f, ShelfHeightCm = 1.5f, ShelfDepthCm = 16f;

        // Seagrass tank at the far right back: a small saltwater tank with its own lamp over it.
        public const float TankX = 3.3f, TankZ = 0.75f;
        public const float TankWidthCm = 24f, TankDepthCm = 14f, TankHeightCm = 18f, TankWaterCm = 15f;

        // PAR meter (the quantum sensor sits in a cup's place when reading), timer, and the stock
        // bottles at the right front.
        public const float MeterX = 0.75f, MeterZ = -1.5f;
        public const float MeterWidthCm = 9f, MeterDepthCm = 14f, MeterHeightCm = 3f;
        public const float TimerX = 4.4f, TimerZ = -1.55f;
        public const float BottleRowX = 2.7f, BottleRowZ = -1.3f;
        public const float BottlePitchCm = 8.5f, BottleRadiusCm = 2.4f, BottleHeightCm = 11f;

        public static readonly string[] BottleIds = { "bicarb", "water", "detergent" };
        public static readonly string[] BottleLabels = { "NaHCO3", "water", "detergent" };

        /// <summary>Where the base of cup i (0-3) sits.</summary>
        public static Vector3 CupSlot(int i)
        {
            return new Vector3(CupRowX + (i - (CupCount - 1) * 0.5f) * CupPitchCm * Cm, 0f, CupRowZ);
        }

        /// <summary>The water surface in a cup filled to the assay volume.</summary>
        public static float CupWaterSurfaceY
        {
            get { return CupHeightCm * Cm * (CupFillMl / CupVolumeMl); }
        }

        /// <summary>Where the lamp face is for a given distance above the water.</summary>
        public static Vector3 LampFace(float distanceCm)
        {
            return new Vector3(CupRowX, CupWaterSurfaceY + Mathf.Clamp(distanceCm, LampMinDistanceCm, LampMaxDistanceCm) * Cm, CupRowZ);
        }

        /// <summary>Where filter i stands in its rack (its base).</summary>
        public static Vector3 FilterSlot(int i)
        {
            return new Vector3(FilterRackX + (i - 1.5f) * FilterPitchCm * Cm, 0f, FilterRackZ);
        }

        /// <summary>Where the tip of syringe i (0-3) rests in its rack.</summary>
        public static Vector3 SyringeSlot(int i)
        {
            return new Vector3(SyringeRackX + (i - (CupCount - 1) * 0.5f) * SyringePitchCm * Cm, 0.5f * Cm, SyringeRackZ);
        }

        /// <summary>Where specimen station k (0 spinach, 1 coleus, 2 seagrass card) sits on the shelf.</summary>
        public static Vector3 ShelfSlot(int k)
        {
            return new Vector3(ShelfX + (k - 0.5f) * ShelfPitchCm * Cm, ShelfHeightCm * Cm, ShelfZ);
        }

        /// <summary>Where bottle i (0 bicarbonate, 1 water, 2 detergent) stands.</summary>
        public static Vector3 BottleSlot(int i)
        {
            return new Vector3(BottleRowX + (i - 1) * BottlePitchCm * Cm, 0f, BottleRowZ);
        }
    }
}
