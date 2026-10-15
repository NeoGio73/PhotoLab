using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// What every thing on the bench that can be pointed at has in common: an id, a home position,
    /// a hover glow, and the box it takes up on screen. The drag handler never looks at the
    /// specific parts; it hit-tests these boxes.
    /// </summary>
    public class BenchItem : MonoBehaviour
    {
        public string id;
        public Vector3 home;
        public bool held;

        Renderer[] renderers;
        MaterialPropertyBlock block;
        bool hover;
        Component highlight;                 // Highlight Plus, if the package is in the project
        System.Reflection.PropertyInfo highlighted;

        protected virtual void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            highlight = GetComponent("HighlightEffect");
            if (highlight != null) highlighted = highlight.GetType().GetProperty("highlighted");
        }

        public Bounds WorldBounds
        {
            get
            {
                if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
                var b = new Bounds(transform.position, Vector3.zero);
                bool first = true;
                foreach (Renderer r in renderers)
                {
                    if (r == null || !r.enabled || r.GetComponent<TextMesh>() != null) continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                return b;
            }
        }

        public void SetHover(bool on)
        {
            if (on == hover) return;
            hover = on;
            if (highlight != null && highlighted != null) { highlighted.SetValue(highlight, on, null); return; }
            if (renderers == null) return;
            foreach (Renderer r in renderers)
            {
                if (r == null || r.GetComponent<TextMesh>() != null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", on ? new Color(0.25f, 0.25f, 0.2f) : Color.black);
                r.SetPropertyBlock(block);
                if (on) r.material.EnableKeyword("_EMISSION");
            }
        }

        public void GoHome()
        {
            transform.position = home;
            held = false;
        }
    }
}
