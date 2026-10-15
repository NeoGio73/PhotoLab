using System.Collections.Generic;
using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// Runs the leaf disk assay bench. Four cups each run their own copy of the model, sharing the
    /// lamp (distance, filter), the room and the clock. The student does the protocol on the bench
    /// with the mouse (see PhotoDrag): punch disks from a specimen, load them into a syringe, draw
    /// a solution, add detergent, pull a vacuum, pour into a cup, slide a filter in, set the lamp,
    /// start the timer. Mistakes are not stopped; they show up in the data.
    ///
    /// The page talks to it with unity.SendMessage("Lab", method, value); every public method here
    /// that takes a string is one of those entry points. Lenses are free: each is a camera view
    /// plus an overlay, and every overlay reads the same models.
    /// </summary>
    public class PhotoManager : MonoBehaviour
    {
        public static readonly string[] Lenses = { "bench", "physics", "chemistry", "biology", "microbiology", "math", "earth" };

        [Header("Shared settings")]
        public string filterId = "clear";
        public float lampDistanceCm = Bench.LampDefaultDistanceCm;
        public float ambientCelsius = 22f;
        public float depthM = 0.5f, kdPerM = 0.4f, salinityPpt = 35f;
        public float labMinutesPerSecond = 10f / 60f;
        public float reportInterval = 0.25f;
        public bool running;
        public float minutes;

        [Header("Scene parts (set by the scene builder)")]
        public PhotoRig rig;
        public Transform lampHead;
        public Light lampLight;
        public Component beam;          // Volumetric Light Beam under the lamp head, if the package is present
        public Renderer filterInSlot;
        public Cup[] cups = new Cup[0];
        public Syringe[] syringes = new Syringe[0];
        public FilterGel[] filters = new FilterGel[0];
        public Bottle[] bottles = new Bottle[0];
        public Specimen[] specimens = new Specimen[0];
        public HolePunch punch;
        public DiskStack stack;
        public Renderer timerBody;
        public Renderer syringeRack;
        public TextMesh parDisplay;
        public TextMesh timerDisplay;
        public GameObject[] overlays = new GameObject[0];

        string lens = "bench";
        int selectedCup;
        float sinceReport;
        bool dirty = true;
        Material filterMaterial;
        readonly List<string> notebook = new List<string>();

        public string Lens { get { return lens; } }
        public int SelectedCup { get { return selectedCup; } }
        public int Generation { get; private set; }

        void Start()
        {
            PhotoBridge.WatchPointer();
            foreach (Cup c in cups) if (c != null) { c.model.diskCount = 0; c.model.Reset(); }
            // Liquid Volume does not refresh its material until a property changes; nudge every liquid once
            foreach (Component lv in GetComponentsInChildren(typeof(Component), true))
                if (lv != null && lv.GetType().Name == "LiquidVolume") { float l = LiquidBridge.GetLevel(lv); LiquidBridge.SetLevel(lv, l + 0.001f); LiquidBridge.SetLevel(lv, l); }
            ApplyLens(lens, true);
            Report();
            if (rig != null) rig.PlayIntro();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (running)
            {
                float labDt = dt * labMinutesPerSecond;
                minutes += labDt;
                foreach (Cup c in cups) if (c != null && c.HasContents) { Share(c.model); c.model.Advance(labDt); }
            }
            Animate();
            sinceReport += dt;
            if (dirty || (running && sinceReport >= reportInterval)) Report();
        }

        /// <summary>Push the bench-wide settings into one cup's model.</summary>
        void Share(PhotosynthesisModel m)
        {
            m.filterId = filterId;
            m.lampDistanceCm = lampDistanceCm;
            m.ambientCelsius = ambientCelsius;
            m.depthM = depthM; m.kdPerM = kdPerM; m.salinityPpt = salinityPpt;
            m.running = running;
            if (!running && minutes <= 0f) m.waterCelsius = ambientCelsius;
        }

        // ---- What the page hears ------------------------------------------------------------------

        [System.Serializable]
        public class CupSummary
        {
            public int index;
            public bool present;
            public string specimen;
            public int disks, floated, sunk, neverSank;
            public float bicarbonatePercent, infiltration, et50, netRate;
            public bool detergent;
        }

        [System.Serializable]
        public class SyringeSummary
        {
            public string id;
            public int disks, pulls;
            public string specimen, solution;
            public float volumeMl, infiltration;
            public bool detergent, held;
        }

        [System.Serializable]
        public class BenchState
        {
            public string type = "state";
            public int cup;
            public string lens;
            public bool running;
            public float minutes;
            public PhotosynthesisModel.Snapshot model;   // the selected cup
            public CupSummary[] cups;
            public SyringeSummary[] syringes;
            public int stackDisks;
            public string stackSpecimen;
        }

        void Report()
        {
            dirty = false;
            sinceReport = 0f;
            var state = new BenchState { cup = selectedCup, lens = lens, running = running, minutes = minutes };
            Cup sel = selectedCup < cups.Length ? cups[selectedCup] : null;
            if (sel != null) { Share(sel.model); state.model = sel.model.Snap(); state.model.minutes = minutes; }
            state.cups = new CupSummary[cups.Length];
            for (int i = 0; i < cups.Length; i++)
            {
                Cup c = cups[i];
                if (c == null) continue;
                Share(c.model);
                state.cups[i] = new CupSummary
                {
                    index = i, present = c.HasContents, specimen = c.model.specimenId, disks = c.model.Present,
                    floated = c.model.FloatedCount, sunk = c.model.SunkCount, neverSank = c.model.NeverSank,
                    bicarbonatePercent = c.model.bicarbonatePercent, infiltration = c.model.infiltration,
                    detergent = c.model.detergentAdded, et50 = c.model.Et50(), netRate = c.model.NetRate()
                };
            }
            state.syringes = new SyringeSummary[syringes.Length];
            for (int i = 0; i < syringes.Length; i++)
            {
                Syringe s = syringes[i];
                if (s == null) continue;
                state.syringes[i] = new SyringeSummary
                {
                    id = s.id, disks = s.disks, pulls = s.pulls, specimen = s.specimen, solution = s.solution.ToString().ToLower(),
                    volumeMl = s.volumeMl, infiltration = s.infiltration, detergent = s.detergent, held = s.held
                };
            }
            state.stackDisks = stack != null ? stack.count : 0;
            state.stackSpecimen = stack != null ? stack.specimen : "";
            PhotoBridge.Notify(JsonUtility.ToJson(state));
        }

        /// <summary>A line for the student's notebook: what just happened on the bench.</summary>
        public void Note(string text)
        {
            notebook.Add(text);
            PhotoBridge.Notify("{\"type\":\"note\",\"minutes\":" + minutes.ToString("F2") + ",\"text\":\"" + text.Replace("\"", "'") + "\"}");
            dirty = true;
        }

        public void NotifyHover(string item, string id)
        {
            PhotoBridge.Notify("{\"type\":\"hover\",\"item\":\"" + item + "\",\"id\":\"" + id + "\"}");
        }

        // ---- Entry points for the page ------------------------------------------------------------

        public void SetLens(string name) { ApplyLens(name, false); }
        public void GoView(string view) { if (rig != null) rig.Go(view, false); }
        public void SetSpeed(string value) { float v; if (float.TryParse(value, out v)) labMinutesPerSecond = Mathf.Max(0f, v) / 60f; }
        public void SelectCup(string index) { int i; if (int.TryParse(index, out i)) selectedCup = Mathf.Clamp(i, 0, cups.Length - 1); dirty = true; }
        public void SetAmbient(string celsius) { float v; if (float.TryParse(celsius, out v)) ambientCelsius = v; dirty = true; }
        public void SetDepth(string metres) { float v; if (float.TryParse(metres, out v)) depthM = Mathf.Clamp(v, 0f, 10f); dirty = true; }
        public void SetTurbidity(string kd) { float v; if (float.TryParse(kd, out v)) kdPerM = Mathf.Clamp(v, 0.05f, 5f); dirty = true; }
        public void SetSalinity(string ppt) { float v; if (float.TryParse(ppt, out v)) salinityPpt = Mathf.Clamp(v, 0f, 50f); dirty = true; }
        public void SetLampDistance(string cm) { float v; if (float.TryParse(cm, out v)) { SetLamp(v); LampSettled(); } }
        public void SetFilter(string id) { FilterGel g = FindFilter(id); if (g != null) InsertFilter(g); else if (id == "clear") RemoveFilter(); }
        public void StartRun(string unused) { StartTimer(); }
        public void StopRun(string unused) { StopTimer(); }
        public void ResetRun(string unused) { ResetTimer(); }
        public void WashUp(string unused) { Wash(); }

        // ---- The protocol, as the bench does it --------------------------------------------------------

        public void Punch(Specimen sp)
        {
            if (sp == null || stack == null) return;
            if (stack.count > 0) { Note("There are already " + stack.count + " disks waiting; load them into a syringe first."); return; }
            stack.Set(Bench.DisksPerCup, sp.specimenId);
            PhotosynthesisModel.Specimen s = Find(sp.specimenId);
            Note("Punched " + Bench.DisksPerCup + " disks from " + (s != null ? s.name : sp.specimenId) + ".");
        }

        public void LoadDisks(Syringe s)
        {
            if (s == null || stack == null || stack.count == 0) return;
            if (s.disks > 0) { Note("Syringe " + s.id + " already holds disks."); return; }
            s.disks = stack.count;
            s.specimen = stack.specimen;
            s.infiltration = 0f; s.pulls = 0;
            stack.Take();
            Note("Syringe " + s.id + ": plunger out, " + s.disks + " disks dropped into the barrel, plunger back in.");
        }

        public void Draw(Syringe s, Bottle b)
        {
            if (s == null || b == null) return;
            if (b.detergent)
            {
                if (s.volumeMl <= 0f) { Note("Syringe " + s.id + " is empty; draw a solution before the detergent."); return; }
                s.detergent = true;
                Note("One drop of detergent into syringe " + s.id + ".");
                return;
            }
            if (s.volumeMl > 0f) { Note("Syringe " + s.id + " already holds " + s.volumeMl.ToString("F0") + " mL of " + Name(s.solution) + "."); return; }
            s.solution = b.solution;
            s.volumeMl = 5f;
            s.infiltration = 0f; s.pulls = 0;
            Note("Syringe " + s.id + ": drew 5 mL of " + Name(b.solution) + (s.disks > 0 ? " over the disks." : "."));
        }

        public void PullVacuum(Syringe s)
        {
            if (s == null || s.busy) return;
            if (s.volumeMl <= 0f) { Note("Nothing to pull against: syringe " + s.id + " is empty."); return; }
            int before = Mathf.RoundToInt(s.infiltration * s.disks);
            s.Pull();
            Note("Syringe " + s.id + ": finger over the tip, plunger pulled, tapped, released (pull " + s.pulls + ").");
            StartCoroutine(AfterPull(s, before));
        }

        System.Collections.IEnumerator AfterPull(Syringe s, int before)
        {
            while (s.busy) yield return null;
            if (s.disks == 0) yield break;
            int after = Mathf.RoundToInt(s.infiltration * s.disks);
            if (after == s.disks) Note("All " + s.disks + " disks have sunk in syringe " + s.id + ".");
            else if (after > before) Note((after - before) + " more disk" + (after - before == 1 ? "" : "s") + " sank in syringe " + s.id + " (" + after + " of " + s.disks + ").");
            else Note("No disks sank in syringe " + s.id + (s.detergent ? "." : ": the water is beading off the leaf."));
        }

        public void Pour(Syringe s, Cup c)
        {
            if (s == null || c == null || !s.Loaded) return;
            if (c.HasContents) { Note("Cup " + (c.index + 1) + " is in use; wash up first."); return; }
            c.Fill(s);
            Share(c.model);
            string what = s.disks + " disks in " + s.volumeMl.ToString("F0") + " mL of " + Name(s.solution) + (s.detergent ? " with detergent" : "");
            Note("Poured syringe " + s.id + " into cup " + (c.index + 1) + ": " + what + ". Topped up to 50 mL.");
            if (c.model.NeverSank > 0) Note(c.model.NeverSank + " disk" + (c.model.NeverSank == 1 ? "" : "s") + " in cup " + (c.index + 1) + " floated at once and will not be counted.");
            s.Empty();
            selectedCup = c.index;
            dirty = true;
        }

        public void InsertFilter(FilterGel g)
        {
            if (g == null) return;
            foreach (FilterGel other in filters) if (other != null && other != g && other.inSlot) { other.inSlot = false; other.GoHome(); }
            g.inSlot = true;
            g.held = false;
            filterId = g.id;
            PhotosynthesisModel.Filter f = FindFilterDef(g.id);
            Note("Slid the " + (f != null ? f.name : g.id) + " under the lamp.");
            dirty = true;
        }

        public void RemoveFilter()
        {
            foreach (FilterGel g in filters) if (g != null && g.inSlot) { g.inSlot = false; g.GoHome(); g.transform.rotation = Quaternion.identity; }
            if (filterId != "clear") Note("Took the filter out.");
            filterId = "clear";
            dirty = true;
        }

        public void SetLamp(float cm)
        {
            float v = Mathf.Clamp(cm, Bench.LampMinDistanceCm, Bench.LampMaxDistanceCm);
            if (Mathf.Abs(v - lampDistanceCm) < 0.01f) return;
            lampDistanceCm = v;
            dirty = true;
        }

        public void LampSettled()
        {
            Note("Lamp set " + lampDistanceCm.ToString("F0") + " cm above the cups.");
        }

        public void StartTimer()
        {
            if (running) return;
            bool any = false;
            foreach (Cup c in cups) if (c != null && c.HasContents) any = true;
            if (!any) Note("The timer is running on an empty bench: no cup has disks in it yet.");
            running = true;
            foreach (Cup c in cups) if (c != null) c.model.running = true;
            Note("Timer started.");
        }

        public void StopTimer()
        {
            if (!running) return;
            running = false;
            foreach (Cup c in cups) if (c != null) c.model.running = false;
            Note("Timer paused at " + Clock() + ".");
        }

        public void ToggleTimer() { if (running) StopTimer(); else StartTimer(); }

        public void ResetTimer()
        {
            running = false;
            minutes = 0f;
            foreach (Cup c in cups) if (c != null) { Share(c.model); c.model.Reset(); }
            Note("Timer reset; the cups are back to the start of the run.");
        }

        public void Wash()
        {
            running = false;
            minutes = 0f;
            foreach (Cup c in cups) if (c != null) c.Wash();
            foreach (Syringe s in syringes) if (s != null) { s.Empty(); s.GoHome(); }
            if (stack != null) stack.Take();
            RemoveFilter();
            Generation++;
            Note("Washed up: cups and syringes empty.");
        }

        // ---- Lenses --------------------------------------------------------------------------------------

        void ApplyLens(string name, bool instant)
        {
            if (System.Array.IndexOf(Lenses, name) < 0) name = "bench";
            lens = name;
            foreach (GameObject o in overlays) if (o != null) o.SetActive(o.name == "Overlay " + lens);
            if (rig != null) rig.Go(ViewFor(lens), instant);
            PhotoBridge.Notify("{\"type\":\"lens\",\"lens\":\"" + lens + "\"}");
            dirty = true;
        }

        public void SetView(string view) { if (rig != null) rig.Go(view, false); }

        static string ViewFor(string lens)
        {
            switch (lens)
            {
                case "physics": return "lamp";
                case "chemistry": return "cups";
                case "biology": return "cups";
                case "microbiology": return "shelf";
                case "math": return "meter";
                case "earth": return "tank";
                default: return "bench";
            }
        }

        // ---- What the bench shows ----------------------------------------------------------------------------

        void Animate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (lampHead != null)
            {
                Vector3 face = Bench.LampFace(lampDistanceCm);
                Vector3 want = new Vector3(lampHead.position.x, face.y + Bench.LampHeadHeightCm * 0.5f * Bench.Cm, lampHead.position.z);
                lampHead.position = Vector3.Lerp(lampHead.position, want, 1f - Mathf.Exp(-8f * dt));
            }
            PhotosynthesisModel.Filter filter = FindFilterDef(filterId) ?? PhotosynthesisModel.Filters[0];
            PhotosynthesisModel probe = cups.Length > 0 && cups[0] != null ? cups[0].model : null;
            float par = 0f;
            if (probe != null) { Share(probe); par = probe.ParAtSurface(); }
            if (lampLight != null)
            {
                lampLight.color = Color.Lerp(Color.white, new Color(filter.tint.r, filter.tint.g, filter.tint.b), filter.tint.a);
                lampLight.intensity = Mathf.Clamp(par / 300f, 0.3f, 6f);
            }
            if (beam != null)
            {
                // brighter when the lamp is close, dimmer through a dark gel; reaches down to the water
                BeamBridge.SetIntensity(beam, Mathf.Clamp(par / 400f, 0.3f, 1.2f) * 0.7f);
                BeamBridge.SetLength(beam, lampDistanceCm * Bench.Cm + 0.4f);
            }
            if (filterInSlot != null)
            {
                if (filterMaterial == null) filterMaterial = filterInSlot.material;
                filterInSlot.enabled = filterId != "clear";
                Color c = new Color(filter.tint.r, filter.tint.g, filter.tint.b, Mathf.Max(0.12f, filter.tint.a * 0.6f));
                filterMaterial.color = c;
                if (filterMaterial.HasProperty("_BaseColor")) filterMaterial.SetColor("_BaseColor", c);
            }
            // a gel in the slot rides with the lamp head
            foreach (FilterGel g in filters)
                if (g != null && g.inSlot && lampHead != null && !g.held)
                {
                    g.transform.position = lampHead.position + new Vector3(0f, -Bench.LampHeadHeightCm * 0.5f * Bench.Cm - 0.6f * Bench.Cm, 0f);
                    g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }
            if (parDisplay != null) parDisplay.text = par.ToString("F0") + "\nµmol/m²/s";
            if (timerDisplay != null) timerDisplay.text = Clock();
        }

        string Clock()
        {
            int total = Mathf.FloorToInt(minutes * 60f);
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        // ---- Lookups -----------------------------------------------------------------------------------------

        static PhotosynthesisModel.Specimen Find(string id)
        {
            foreach (PhotosynthesisModel.Specimen s in PhotosynthesisModel.Specimens) if (s.id == id) return s;
            return null;
        }

        static PhotosynthesisModel.Filter FindFilterDef(string id)
        {
            foreach (PhotosynthesisModel.Filter f in PhotosynthesisModel.Filters) if (f.id == id) return f;
            return null;
        }

        FilterGel FindFilter(string id)
        {
            foreach (FilterGel g in filters) if (g != null && g.id == id) return g;
            return null;
        }

        static string Name(Solution s)
        {
            switch (s)
            {
                case Solution.Bicarbonate: return "0.2% NaHCO3";
                case Solution.Water: return "DI water";
                default: return "nothing";
            }
        }
    }
}
