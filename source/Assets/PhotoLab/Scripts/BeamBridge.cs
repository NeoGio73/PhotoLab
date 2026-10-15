using System.Reflection;
using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// The lab's hand on the Volumetric Light Beam under the lamp. The component's own runtime
    /// type is used for the lookups, so a domain reload cannot leave a stale type behind. Without
    /// the package every call is a no-op.
    /// </summary>
    public static class BeamBridge
    {
        static System.Type type;
        static FieldInfo color, fallOffEnd;
        static PropertyInfo intensity;
        static MethodInfo update;

        static void Bind(Component b)
        {
            if (b == null || b.GetType() == type) return;
            type = b.GetType();
            color = type.GetField("color");
            fallOffEnd = type.GetField("fallOffEnd");
            intensity = type.GetProperty("intensityGlobal");
            update = type.GetMethod("UpdateAfterManualPropertyChange", BindingFlags.Public | BindingFlags.Instance);
        }

        static void Changed(Component b)
        {
            if (update != null) update.Invoke(b, null);
        }

        public static void SetColor(Component b, Color c)
        {
            if (b == null) return;
            Bind(b);
            if (color == null || (Color)color.GetValue(b) == c) return;
            color.SetValue(b, c);
            Changed(b);
        }

        public static void SetIntensity(Component b, float v)
        {
            if (b == null) return;
            Bind(b);
            if (intensity == null || Mathf.Abs((float)intensity.GetValue(b, null) - v) < 0.002f) return;
            intensity.SetValue(b, v, null);
            Changed(b);
        }

        public static void SetLength(Component b, float metres)
        {
            if (b == null) return;
            Bind(b);
            if (fallOffEnd == null || Mathf.Abs((float)fallOffEnd.GetValue(b) - metres) < 0.005f) return;
            fallOffEnd.SetValue(b, metres);
            Changed(b);
        }
    }
}
