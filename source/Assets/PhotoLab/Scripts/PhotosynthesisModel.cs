using System;
using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// The one model every lens reads from. The lamp, the filter, the bicarbonate, the water
    /// temperature, the specimen and (for seagrass) the water column all feed one rate of oxygen
    /// evolution per disk; the disks float when the oxygen they have made exceeds a buoyancy
    /// threshold that varies a little from disk to disk. Nothing in here touches the scene: the
    /// manager calls Advance() each frame and reads a Snapshot for the page and the overlays.
    ///
    /// Units: light in µmol photons m⁻² s⁻¹ (PAR); rates in µmol O₂ per disk per minute;
    /// concentrations in mM; temperatures in °C; distances in cm; depth in m.
    /// Photosynthesis follows the hyperbolic tangent light response of Jassby and Platt (1976).
    /// </summary>
    [Serializable]
    public class PhotosynthesisModel
    {
        // ---- Spectrum: six 50 nm bins across PAR ---------------------------------------------
        public static readonly float[] BinCentreNm = { 425f, 475f, 525f, 575f, 625f, 675f };
        public static readonly string[] BinNames = { "violet-blue", "blue", "green", "yellow", "orange-red", "red" };
        // Fraction of a warm-white LED lamp's photons in each bin (sums to 1).
        static readonly float[] LampSpectrum = { 0.20f, 0.13f, 0.17f, 0.21f, 0.18f, 0.11f };

        [Serializable]
        public class Filter
        {
            public string id, name;
            public float[] transmittance;   // per bin, 0-1
            public Color tint;
        }

        public static readonly Filter[] Filters =
        {
            new Filter { id = "clear", name = "no filter", transmittance = new[] { 1f, 1f, 1f, 1f, 1f, 1f }, tint = new Color(1f, 1f, 1f, 0.15f) },
            new Filter { id = "blue", name = "blue gel", transmittance = new[] { 0.80f, 0.70f, 0.18f, 0.05f, 0.03f, 0.03f }, tint = new Color(0.2f, 0.4f, 1f, 0.6f) },
            new Filter { id = "green", name = "green gel", transmittance = new[] { 0.05f, 0.25f, 0.80f, 0.45f, 0.08f, 0.04f }, tint = new Color(0.2f, 0.9f, 0.3f, 0.6f) },
            new Filter { id = "red", name = "red gel", transmittance = new[] { 0.02f, 0.02f, 0.04f, 0.20f, 0.85f, 0.90f }, tint = new Color(1f, 0.2f, 0.15f, 0.6f) }
        };

        [Serializable]
        public class Specimen
        {
            public string id, name;
            public float pmax;            // µmol O₂ per disk per minute, saturating
            public float alpha;           // initial slope, µmol O₂ per disk per minute per (µmol m⁻² s⁻¹)
            public float rd;              // dark respiration, µmol O₂ per disk per minute
            public float kBicarb;         // mM of DIC at half the maximum rate
            public float tOpt, tWidth;    // °C
            public float[] action;        // relative quantum efficiency per bin, 0-1
            public float floatThreshold;  // µmol O₂ a typical disk must make to float
            public float floatSpread;     // log-normal spread of that threshold between disks
            public bool marine;           // lives in the tank: seawater chemistry and a water column
            public bool noChlorophyll;    // white coleus tissue: nothing to absorb
        }

        public static readonly Specimen[] Specimens =
        {
            new Specimen { id = "spinach", name = "spinach", pmax = 0.040f, alpha = 0.00030f, rd = 0.0025f, kBicarb = 5f, tOpt = 25f, tWidth = 12f,
                action = new[] { 0.85f, 0.80f, 0.45f, 0.55f, 0.95f, 1.00f }, floatThreshold = 0.22f, floatSpread = 0.22f },
            new Specimen { id = "coleus", name = "coleus (green tissue)", pmax = 0.030f, alpha = 0.00024f, rd = 0.0025f, kBicarb = 5f, tOpt = 26f, tWidth = 12f,
                action = new[] { 0.80f, 0.75f, 0.40f, 0.50f, 0.95f, 1.00f }, floatThreshold = 0.22f, floatSpread = 0.25f },
            new Specimen { id = "coleus-white", name = "coleus (white tissue)", pmax = 0.0f, alpha = 0f, rd = 0.0025f, kBicarb = 5f, tOpt = 26f, tWidth = 12f,
                action = new[] { 0f, 0f, 0f, 0f, 0f, 0f }, floatThreshold = 0.22f, floatSpread = 0.25f, noChlorophyll = true },
            new Specimen { id = "seagrass", name = "turtle grass (Thalassia)", pmax = 0.028f, alpha = 0.00022f, rd = 0.0030f, kBicarb = 12f, tOpt = 28f, tWidth = 10f,
                action = new[] { 0.80f, 0.78f, 0.50f, 0.55f, 0.92f, 1.00f }, floatThreshold = 0.20f, floatSpread = 0.28f, marine = true }
        };

        // ---- Lamp ------------------------------------------------------------------------------
        public float lampParAt10cm = 900f;    // µmol m⁻² s⁻¹ at 10 cm, no filter
        public float lampHeatAt10cm = 5.5f;   // °C the water rises above ambient at 10 cm, in time
        public float heatTauMinutes = 8f;

        // ---- Inputs the student controls -------------------------------------------------------
        public string specimenId = "spinach";
        public string filterId = "clear";
        public float lampDistanceCm = Bench.LampDefaultDistanceCm;
        public float bicarbonatePercent = 0.2f;   // % w/v NaHCO₃ in the cup
        public float ambientCelsius = 22f;
        public bool detergentAdded = true;
        public float infiltration = 1f;           // fraction of disks that sank after the syringe step
        public int diskCount = Bench.DisksPerCup;   // how many disks went into the cup
        // Seagrass tank only: a simulated water column above the blades.
        public float depthM = 0.5f;
        public float kdPerM = 0.4f;               // light attenuation coefficient
        public float salinityPpt = 35f;

        // ---- Run state -----------------------------------------------------------------------------
        public bool running;
        public float minutes;
        public float waterCelsius = 22f;
        public float[] oxygenPerDisk = new float[Bench.DisksPerCup];   // µmol made so far
        public float[] thresholdPerDisk = new float[Bench.DisksPerCup];
        public bool[] floated = new bool[Bench.DisksPerCup];
        public bool[] excluded = new bool[Bench.DisksPerCup];           // floated at t = 0: never infiltrated
        public float[] floatMinutes = new float[Bench.DisksPerCup];
        public int seed = 7;

        public Specimen CurrentSpecimen
        {
            get { foreach (Specimen s in Specimens) if (s.id == specimenId) return s; return Specimens[0]; }
        }

        public Filter CurrentFilter
        {
            get { foreach (Filter f in Filters) if (f.id == filterId) return f; return Filters[0]; }
        }

        public static float PhotonEnergyEv(float nm) { return 1239.84f / nm; }

        // ---- Light -------------------------------------------------------------------------------

        /// <summary>PAR at the lamp distance with no filter (inverse square from the 10 cm value).</summary>
        public float ParUnfiltered()
        {
            float d = Mathf.Max(Bench.LampMinDistanceCm, lampDistanceCm);
            return lampParAt10cm * (10f / d) * (10f / d);
        }

        /// <summary>Photon flux reaching the water in each bin, after the filter.</summary>
        public float[] BinFlux()
        {
            float par = ParUnfiltered();
            Filter f = CurrentFilter;
            var bins = new float[6];
            for (int i = 0; i < 6; i++) bins[i] = par * LampSpectrum[i] * f.transmittance[i];
            return bins;
        }

        /// <summary>What the quantum sensor reads at the water surface: all PAR photons after the filter.</summary>
        public float ParAtSurface()
        {
            float sum = 0f;
            foreach (float b in BinFlux()) sum += b;
            return sum;
        }

        /// <summary>Light at the leaf: at the surface for a cup, or attenuated through the water column for the tank.</summary>
        public float ParAtLeaf()
        {
            float par = ParAtSurface();
            if (CurrentSpecimen.marine) par *= Mathf.Exp(-Mathf.Max(0f, kdPerM) * Mathf.Max(0f, depthM));
            return par;
        }

        /// <summary>Photons weighted by how well the specimen's pigments use each colour.</summary>
        public float ParEffective()
        {
            float[] bins = BinFlux();
            Specimen s = CurrentSpecimen;
            float sum = 0f;
            for (int i = 0; i < 6; i++) sum += bins[i] * s.action[i];
            if (s.marine) sum *= Mathf.Exp(-Mathf.Max(0f, kdPerM) * Mathf.Max(0f, depthM));
            return sum;
        }

        // ---- Chemistry -------------------------------------------------------------------------

        /// <summary>Dissolved inorganic carbon in the cup or tank, mM.</summary>
        public float DicMm()
        {
            float fromBicarb = bicarbonatePercent * 10f / 84.007f * 1000f;   // % w/v → g/L → mol/L → mM
            return (CurrentSpecimen.marine ? 2.1f : 0.02f) + fromBicarb;     // seawater DIC, or air-equilibrated water
        }

        public float PH()
        {
            // A sodium bicarbonate solution sits near (pKa1 + pKa2)/2 whatever its strength;
            // seawater buffers near 8.1 and drifts up as the carbon is drawn down.
            if (CurrentSpecimen.marine) return 8.1f;
            if (bicarbonatePercent <= 0.0005f) return 5.6f;   // DI water in equilibrium with the air
            return 8.3f;
        }

        /// <summary>Fractions of DIC present as CO₂(aq), HCO₃⁻ and CO₃²⁻ at the solution's pH.</summary>
        public void CarbonateFractions(out float co2, out float hco3, out float co3)
        {
            bool marine = CurrentSpecimen.marine;
            float pK1 = marine ? 5.86f : 6.35f;
            float pK2 = marine ? 8.92f : 10.33f;
            float h = Mathf.Pow(10f, -PH());
            float k1 = Mathf.Pow(10f, -pK1), k2 = Mathf.Pow(10f, -pK2);
            float denom = h * h + h * k1 + k1 * k2;
            co2 = h * h / denom;
            hco3 = h * k1 / denom;
            co3 = k1 * k2 / denom;
        }

        public float BicarbonateFactor()
        {
            float c = DicMm();
            return c / (CurrentSpecimen.kBicarb + c);
        }

        public float TemperatureFactor()
        {
            Specimen s = CurrentSpecimen;
            float x = (waterCelsius - s.tOpt) / s.tWidth;
            return Mathf.Exp(-x * x);
        }

        public float SalinityFactor()
        {
            if (!CurrentSpecimen.marine) return 1f;
            float x = (salinityPpt - 35f) / 15f;
            return Mathf.Exp(-x * x);
        }

        // ---- Rates ------------------------------------------------------------------------------

        public float GrossRate()
        {
            Specimen s = CurrentSpecimen;
            if (s.pmax <= 0f) return 0f;
            float light = (float)Math.Tanh(s.alpha * ParEffective() / s.pmax);
            return s.pmax * light * BicarbonateFactor() * TemperatureFactor() * SalinityFactor();
        }

        public float NetRate() { return GrossRate() - CurrentSpecimen.rd; }

        /// <summary>Net rate scaled to a square metre of leaf: µmol O₂ m⁻² s⁻¹, for the earth-science lens.</summary>
        public float NetRatePerM2PerSecond()
        {
            float diskM2 = Mathf.PI * (Bench.DiskRadiusCm / 100f) * (Bench.DiskRadiusCm / 100f);
            return NetRate() / 60f / diskM2;
        }

        // ---- The run ----------------------------------------------------------------------------

        public void Reset()
        {
            running = false;
            minutes = 0f;
            waterCelsius = ambientCelsius;
            var rng = new System.Random(seed);
            Specimen s = CurrentSpecimen;
            float sunkFraction = detergentAdded ? Mathf.Clamp01(infiltration) : 0f;
            int count = Mathf.Clamp(diskCount, 0, Bench.DisksPerCup);
            int sunk = Mathf.RoundToInt(sunkFraction * count);
            for (int i = 0; i < Bench.DisksPerCup; i++)
            {
                if (i >= count) { thresholdPerDisk[i] = 1f; oxygenPerDisk[i] = 0f; floated[i] = true; excluded[i] = true; floatMinutes[i] = 0f; continue; }
                // Box-Muller: a normal deviate for the log of the threshold.
                double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                thresholdPerDisk[i] = s.floatThreshold * Mathf.Exp(s.floatSpread * (float)z);
                oxygenPerDisk[i] = 0f;
                floated[i] = i >= sunk;
                excluded[i] = i >= sunk;
                floatMinutes[i] = excluded[i] ? 0f : -1f;
            }
        }

        public void Start()
        {
            if (minutes <= 0f) Reset();
            running = true;
        }

        public void Stop() { running = false; }

        /// <summary>Move the run on by some minutes of lab time.</summary>
        public void Advance(float dtMinutes)
        {
            if (!running || dtMinutes <= 0f) return;
            minutes += dtMinutes;
            // the lamp warms the water toward ambient + heating, first order
            float d = Mathf.Max(Bench.LampMinDistanceCm, lampDistanceCm);
            float heating = lampHeatAt10cm * (10f / d) * (10f / d) * CurrentFilter.transmittance[5];   // red/IR end carries the heat
            float target = ambientCelsius + heating;
            waterCelsius += (target - waterCelsius) * (1f - Mathf.Exp(-dtMinutes / heatTauMinutes));

            float rate = NetRate();
            for (int i = 0; i < Bench.DisksPerCup; i++)
            {
                if (floated[i]) continue;
                oxygenPerDisk[i] = Mathf.Max(0f, oxygenPerDisk[i] + rate * dtMinutes);
                if (oxygenPerDisk[i] >= thresholdPerDisk[i])
                {
                    floated[i] = true;
                    floatMinutes[i] = minutes;
                }
            }
        }

        public int FloatedCount { get { int n = 0; for (int i = 0; i < floated.Length; i++) if (floated[i] && !excluded[i]) n++; return n; } }
        public int ExcludedCount { get { int n = 0; for (int i = 0; i < excluded.Length; i++) if (excluded[i]) n++; return n; } }
        public int SunkCount { get { int n = 0; for (int i = 0; i < floated.Length; i++) if (!floated[i]) n++; return n; } }
        public int Present { get { return Mathf.Clamp(diskCount, 0, Bench.DisksPerCup); } }
        /// <summary>Disks that were in the cup but never sank: floated at t = 0 for lack of infiltration.</summary>
        public int NeverSank { get { int n = 0; for (int i = 0; i < Present; i++) if (excluded[i]) n++; return n; } }

        /// <summary>Median float time of the disks that were counted, or -1 until half of them are up.</summary>
        public float Et50()
        {
            int counted = Present - NeverSank;
            if (counted == 0) return -1f;
            var times = new System.Collections.Generic.List<float>();
            for (int i = 0; i < Present; i++) if (!excluded[i] && floated[i]) times.Add(floatMinutes[i]);
            times.Sort();
            int half = (counted + 1) / 2;
            if (times.Count < half) return -1f;
            if (counted % 2 == 1 || times.Count < half + 1) return times[half - 1];
            return 0.5f * (times[half - 1] + times[half]);
        }

        // ---- Snapshot for the page and the overlays ---------------------------------------------

        [Serializable]
        public class Snapshot
        {
            public string type = "state";
            public bool running;
            public float minutes;
            public string specimen, filter;
            public float lampDistanceCm, bicarbonatePercent, ambientCelsius, waterCelsius;
            public bool detergentAdded;
            public float infiltration, depthM, kdPerM, salinityPpt;
            // physics
            public float parUnfiltered, parSurface, parLeaf, parEffective;
            public float[] binNm, binFlux, binEnergyEv;
            // chemistry
            public float dicMm, pH, fracCO2, fracHCO3, fracCO3, bicarbonateFactor, temperatureFactor, salinityFactor;
            // biology
            public float grossRate, respiration, netRate, lightFactor;
            // the disks
            public int floated, sunk, excluded, present;
            public float[] floatMinutes;
            public float et50;
            // scale-up
            public float netPerM2PerSecond, gramsCarbonPerM2PerDay;
        }

        public Snapshot Snap()
        {
            Specimen s = CurrentSpecimen;
            var snap = new Snapshot
            {
                running = running, minutes = minutes, specimen = s.id, filter = filterId,
                lampDistanceCm = lampDistanceCm, bicarbonatePercent = bicarbonatePercent,
                ambientCelsius = ambientCelsius, waterCelsius = waterCelsius, detergentAdded = detergentAdded,
                infiltration = infiltration, depthM = depthM, kdPerM = kdPerM, salinityPpt = salinityPpt,
                parUnfiltered = ParUnfiltered(), parSurface = ParAtSurface(), parLeaf = ParAtLeaf(), parEffective = ParEffective(),
                binNm = BinCentreNm, binFlux = BinFlux(), binEnergyEv = new float[6],
                dicMm = DicMm(), pH = PH(), bicarbonateFactor = BicarbonateFactor(), temperatureFactor = TemperatureFactor(), salinityFactor = SalinityFactor(),
                grossRate = GrossRate(), respiration = s.rd, netRate = NetRate(),
                lightFactor = s.pmax > 0f ? (float)Math.Tanh(s.alpha * ParEffective() / s.pmax) : 0f,
                floated = FloatedCount, sunk = SunkCount, excluded = NeverSank, present = Present,
                floatMinutes = (float[])floatMinutes.Clone(), et50 = Et50(),
                netPerM2PerSecond = NetRatePerM2PerSecond()
            };
            for (int i = 0; i < 6; i++) snap.binEnergyEv[i] = PhotonEnergyEv(BinCentreNm[i]);
            CarbonateFractions(out snap.fracCO2, out snap.fracHCO3, out snap.fracCO3);
            // one O₂ out for one CO₂ fixed; 12 h of light a day; 12.011 g per mol C
            snap.gramsCarbonPerM2PerDay = snap.netPerM2PerSecond * 1e-6f * 43200f * 12.011f;
            return snap;
        }

        public string SnapJson() { return JsonUtility.ToJson(Snap()); }
    }
}
