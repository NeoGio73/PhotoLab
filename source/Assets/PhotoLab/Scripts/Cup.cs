using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// One of the four cups under the lamp. Each runs its own copy of the model, so a dark control,
    /// a different distance, or a water-only mistake can sit beside the real thing. The disks in it
    /// are made when the syringe is poured in; they rest on the bottom and rise as the model says.
    /// </summary>
    public class Cup : BenchItem
    {
        public int index;
        public PhotosynthesisModel model = new PhotosynthesisModel();
        public Transform water;
        public Component liquid;        // Liquid Volume Pro on the water, if the package is present
        float shownLevel;
        public Transform[] disks = new Transform[0];
        public Material diskMaterial;
        public Material diskWhiteMaterial;

        public bool HasContents { get { return model.diskCount > 0; } }

        public void Fill(Syringe s)
        {
            model.diskCount = Mathf.Clamp(s.disks, 0, Bench.DisksPerCup);
            model.specimenId = string.IsNullOrEmpty(s.specimen) ? "spinach" : s.specimen;
            model.bicarbonatePercent = s.solution == Solution.Bicarbonate ? 0.2f : 0f;
            model.detergentAdded = s.detergent;
            model.infiltration = s.infiltration;
            model.seed = 7 + index * 13 + s.pulls;
            model.Reset();
            MakeDisks();
        }

        public void Wash()
        {
            model.diskCount = 0;
            model.Reset();
            MakeDisks();
        }

        void MakeDisks()
        {
            foreach (Transform d in disks) if (d != null) Destroy(d.gameObject);
            int n = model.Present;
            disks = new Transform[n];
            bool white = model.specimenId == "coleus-white";
            for (int k = 0; k < n; k++)
            {
                float angle = k * Mathf.PI * 2f / Mathf.Max(n, 1) + index * 0.7f;
                float r = (Bench.CupRadiusCm - 1.1f) * Bench.Cm * (k % 2 == 0 ? 0.9f : 0.45f);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Disk " + (k + 1);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                float startY = model.floated[k] ? Bench.CupWaterSurfaceY - Bench.DiskThicknessCm * Bench.Cm : Bench.DiskThicknessCm * Bench.Cm;
                go.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, startY, Mathf.Sin(angle) * r);
                go.transform.localScale = new Vector3(Bench.DiskRadiusCm * 2f * Bench.Cm, Bench.DiskThicknessCm * 0.5f * Bench.Cm, Bench.DiskRadiusCm * 2f * Bench.Cm);
                var rend = go.GetComponent<Renderer>();
                rend.sharedMaterial = white && diskWhiteMaterial != null ? diskWhiteMaterial : diskMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                disks[k] = go.transform;
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            // the water: poured in with the disks (to the 50 mL line), gone when washed up
            float wantLevel = HasContents ? Bench.CupFillMl / Bench.CupVolumeMl : 0f;
            shownLevel = Mathf.Lerp(shownLevel, wantLevel, 1f - Mathf.Exp(-2.5f * dt));
            if (liquid != null)
            {
                if (water != null) water.gameObject.SetActive(shownLevel > 0.015f);
                LiquidBridge.SetLevel(liquid, shownLevel);
                // oxygen bubbles while the disks are photosynthesising
                float net = model.running && HasContents ? Mathf.Max(0f, model.NetRate()) : 0f;
                LiquidBridge.SetBubbles(liquid, Mathf.RoundToInt(net / 0.04f * 14f));   // a few streams at full rate, none in the dark
                LiquidBridge.SetTurbulence(liquid, model.running ? 0.15f : 0.05f);
            }
            else if (water != null)
            {
                water.gameObject.SetActive(shownLevel > 0.01f);
                Vector3 s = water.localScale; s.y = Bench.CupHeightCm * Bench.Cm * shownLevel * 0.5f; water.localScale = s;
                Vector3 p = water.localPosition; p.y = s.y; water.localPosition = p;
            }
            float surface = Bench.CupWaterSurfaceY - Bench.DiskThicknessCm * Bench.Cm;
            float floor = Bench.DiskThicknessCm * Bench.Cm;
            for (int i = 0; i < disks.Length && i < model.floated.Length; i++)
            {
                Transform d = disks[i];
                if (d == null) continue;
                Vector3 p = d.localPosition;
                p.y = Mathf.Lerp(p.y, model.floated[i] ? surface : floor, 1f - Mathf.Exp(-3f * dt));
                d.localPosition = p;
            }
        }
    }
}
