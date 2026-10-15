using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// The pile of freshly punched disks beside the hole punch. The punch makes ten at a time from
    /// whichever specimen it was used on; a syringe clicked on the pile takes the whole pile.
    /// </summary>
    public class DiskStack : BenchItem
    {
        public int count;
        public string specimen = "";
        public Transform pile;          // a cylinder scaled to the count
        public Renderer pileRenderer;
        public Material green, white;

        public void Set(int n, string specimenId)
        {
            count = n;
            specimen = specimenId;
            if (pileRenderer != null && green != null)
                pileRenderer.sharedMaterial = specimenId == "coleus-white" && white != null ? white : green;
        }

        public void Take() { count = 0; specimen = ""; }

        void Update()
        {
            if (pile == null) return;
            pile.gameObject.SetActive(count > 0);
            float h = Mathf.Max(0.3f, count * 0.08f) * Bench.Cm;      // a loose scatter in the dish, kept visible
            Vector3 s = pile.localScale;
            s.y = Mathf.Max(0.0001f, h * 0.5f);
            pile.localScale = s;
            Vector3 p = pile.localPosition;
            p.y = s.y;
            pile.localPosition = p;
        }
    }
}
