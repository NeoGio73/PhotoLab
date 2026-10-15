using UnityEngine;

namespace PhotoLab
{
    public enum Solution { None, Bicarbonate, Water }

    /// <summary>
    /// A 10 mL syringe. It is carried in the hand; clicked on a stack of disks it takes them in,
    /// on a bottle it draws 5 mL, on itself it pulls a vacuum (finger over the tip, plunger out,
    /// and back), and on a cup it pours everything out. The disks inside show at the top of the
    /// liquid until enough vacuum has pulled the air out of them, then they sink to the plunger.
    /// </summary>
    public class Syringe : BenchItem
    {
        public Transform plunger;
        public Transform liquid;        // a cylinder scaled to the volume
        public Component liquidVolume;  // Liquid Volume Pro on that cylinder, if present
        public Transform disksInside;   // a short stack that sits in the liquid
        public float plungerRestY;      // local y of the plunger when the barrel is empty
        public float plungerFullY;      // local y when 5 mL is drawn
        public float liquidTopY;        // local y of the liquid surface at 5 mL

        public int disks;
        public string specimen = "";
        public Solution solution = Solution.None;
        public float volumeMl;
        public bool detergent;
        public int pulls;
        public float infiltration;      // fraction of the disks that have sunk, 0-1
        public bool busy;

        float pullT = -1f;
        const float PullSeconds = 0.9f;

        public bool Loaded { get { return disks > 0 || volumeMl > 0f; } }
        public bool Sunk { get { return infiltration >= 0.999f; } }

        public void Empty()
        {
            disks = 0; specimen = ""; solution = Solution.None; volumeMl = 0f; detergent = false; pulls = 0; infiltration = 0f;
        }

        public void Pull()
        {
            if (busy) return;
            busy = true;
            pullT = 0f;
            pulls++;
        }

        /// <summary>How well the disks have been infiltrated after this many pulls. Without detergent the
        /// water never wets the leaf and nothing sinks however hard the plunger is pulled.</summary>
        public static float InfiltrationAfter(int pulls, bool detergent, int seed)
        {
            if (!detergent || pulls == 0) return 0f;
            // one pull sinks a few disks, two most, three all; a touch of chance so repeats differ
            float f = 1f - Mathf.Exp(-pulls * 1.1f);
            var rng = new System.Random(seed * 31 + pulls);
            f += ((float)rng.NextDouble() - 0.5f) * 0.12f;
            if (pulls >= 3) f = 1f;
            return Mathf.Clamp01(f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float fill = Mathf.Clamp01(volumeMl / 5f);
            float targetPlunger = Mathf.Lerp(plungerRestY, plungerFullY, fill);
            if (pullT >= 0f)
            {
                pullT += dt / PullSeconds;
                float s = Mathf.Sin(Mathf.Clamp01(pullT) * Mathf.PI);   // out and back
                targetPlunger += s * (plungerFullY - plungerRestY) * 0.6f;
                if (pullT >= 1f)
                {
                    pullT = -1f;
                    busy = false;
                    infiltration = InfiltrationAfter(pulls, detergent, id != null ? id.GetHashCode() : 0);
                }
            }
            if (plunger != null)
            {
                Vector3 p = plunger.localPosition;
                p.y = Mathf.Lerp(p.y, targetPlunger, pullT >= 0f ? 1f : 1f - Mathf.Exp(-10f * dt));
                plunger.localPosition = p;
            }
            if (liquidVolume != null)
            {
                liquid.gameObject.SetActive(fill > 0.01f);
                LiquidBridge.SetLevel(liquidVolume, fill * 0.5f);   // 5 mL fills half the 10 mL barrel
            }
            else if (liquid != null)
            {
                Vector3 s = liquid.localScale;
                float full = (liquidTopY - 0.3f * Bench.Cm) * 0.5f;
                s.y = Mathf.Max(0.0001f, full * fill);
                liquid.localScale = s;
                Vector3 p = liquid.localPosition;
                p.y = 0.3f * Bench.Cm + s.y;
                liquid.localPosition = p;
                liquid.gameObject.SetActive(fill > 0.01f);
            }
            if (disksInside != null)
            {
                disksInside.gameObject.SetActive(disks > 0);
                float top = 0.3f * Bench.Cm + (liquidTopY - 0.3f * Bench.Cm) * fill;
                float bottom = 0.5f * Bench.Cm;
                float want = fill > 0.01f ? Mathf.Lerp(top - 0.4f * Bench.Cm, bottom, infiltration) : bottom;
                Vector3 p = disksInside.localPosition;
                p.y = Mathf.Lerp(p.y, want, 1f - Mathf.Exp(-4f * dt));
                disksInside.localPosition = p;
            }
        }
    }
}
