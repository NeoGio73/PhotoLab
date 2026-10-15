using System.Reflection;
using UnityEngine;

namespace PhotoLab.EditorTools
{
    /// <summary>
    /// Adds the bought packages' components at build time. Highlight Plus lives in its own
    /// assembly and Volumetric Light Beam in the default one, neither of which this assembly
    /// references, so both are found by name. When a package is missing the scene builds without it.
    /// </summary>
    public static class PhotoPackages
    {
        static System.Type Find(string fullName)
        {
            foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = a.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static void Set(object target, string member, object value)
        {
            System.Type t = target.GetType();
            FieldInfo f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(target, value); return; }
            PropertyInfo p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(target, value, null);
        }

        static object Enum(System.Type owner, string enumName, string value)
        {
            System.Type e = Find(enumName) ?? owner.Assembly.GetType(enumName);
            if (e == null) return null;
            try { return System.Enum.Parse(e, value); } catch { return null; }
        }

        /// <summary>A soft cone of light under the lamp's spot light. Returns the component, or null without the package.</summary>
        public static Component AddBeam(GameObject lightGo)
        {
            System.Type t = Find("VLB.VolumetricLightBeamSD");
            if (t == null) { Debug.LogWarning("Photo lab: Volumetric Light Beam not installed; the lamp has no visible beam."); return null; }
            Component beam = lightGo.GetComponent(t) ?? lightGo.AddComponent(t);
            // the beam follows the manager's own numbers, not the Light's
            Set(beam, "colorFromLight", false);
            Set(beam, "intensityFromLight", false);
            Set(beam, "spotAngleFromLight", false);
            Set(beam, "fallOffEndFromLight", false);
            Set(beam, "color", Color.white);
            Set(beam, "spotAngle", 55f);
            Set(beam, "coneRadiusStart", 0.5f);
            Set(beam, "fallOffStart", 0f);
            Set(beam, "fallOffEnd", 2.0f);
            Set(beam, "intensityModeAdvanced", false);
            Set(beam, "intensityGlobal", 0.35f);
            Set(beam, "noiseIntensity", 0.25f);
            object noise = Enum(t, "VLB.NoiseMode", "WorldSpace");
            if (noise != null) Set(beam, "noiseMode", noise);
            Set(beam, "trackChangesDuringPlaytime", true);
            return beam;
        }

        /// <summary>A Highlight Plus outline on a bench item, off until the drag handler turns it on.</summary>
        public static void AddHighlight(GameObject go)
        {
            System.Type t = Find("HighlightPlus.HighlightEffect");
            if (t == null) return;
            Component h = go.GetComponent(t) ?? go.AddComponent(t);
            Set(h, "highlighted", false);
            Set(h, "outline", 1f);
            Set(h, "outlineColor", new Color(1f, 0.85f, 0.25f, 1f));
            Set(h, "outlineWidth", 0.35f);
            Set(h, "glow", 0.5f);
            Set(h, "glowWidth", 0.4f);
            Set(h, "glowHQColor", new Color(1f, 0.85f, 0.25f, 1f));
            Set(h, "overlay", 0f);
            Set(h, "innerGlow", 0f);
            object group = Enum(t, "HighlightPlus.TargetOptions", "Children");
            if (group != null) Set(h, "effectGroup", group);
            object seeThrough = Enum(t, "HighlightPlus.SeeThroughMode", "Never");
            if (seeThrough != null) Set(h, "seeThrough", seeThrough);
        }
    }
}
