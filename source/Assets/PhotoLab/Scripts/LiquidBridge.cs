using System.Reflection;
using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// The lab's hand on Liquid Volume Pro, which lives in the project's default assembly and so
    /// cannot be referenced from this one. The component is found by name and its own runtime
    /// type is used for the property lookups, so the editor's domain reloads cannot leave a stale
    /// type behind. If the package is not in the project every call is a no-op.
    /// </summary>
    public static class LiquidBridge
    {
        static System.Type type;
        static PropertyInfo level, bubbles, color1, color2, turbulence;

        static void Bind(Component lv)
        {
            if (lv == null || lv.GetType() == type) return;
            type = lv.GetType();
            level = type.GetProperty("level");
            bubbles = type.GetProperty("bubblesAmount");
            color1 = type.GetProperty("liquidColor1");
            color2 = type.GetProperty("liquidColor2");
            turbulence = type.GetProperty("turbulence1");
        }

        public static Component Find(GameObject go)
        {
            if (go == null) return null;
            Component lv = go.GetComponent("LiquidVolume");
            if (lv != null) Bind(lv);
            return lv;
        }

        public static void SetLevel(Component lv, float value)
        {
            if (lv == null) return;
            Bind(lv);
            if (level != null) level.SetValue(lv, Mathf.Clamp01(value), null);
        }

        public static float GetLevel(Component lv)
        {
            if (lv == null) return 0f;
            Bind(lv);
            return level != null ? (float)level.GetValue(lv, null) : 0f;
        }

        public static void SetBubbles(Component lv, int amount)
        {
            if (lv == null) return;
            Bind(lv);
            if (bubbles != null) bubbles.SetValue(lv, Mathf.Max(0, amount), null);
        }

        public static void SetColor(Component lv, Color c)
        {
            if (lv == null) return;
            Bind(lv);
            if (color1 != null) color1.SetValue(lv, c, null);
            if (color2 != null) color2.SetValue(lv, c, null);
        }

        public static void SetTurbulence(Component lv, float t)
        {
            if (lv == null) return;
            Bind(lv);
            if (turbulence != null) turbulence.SetValue(lv, Mathf.Clamp01(t), null);
        }
    }
}
