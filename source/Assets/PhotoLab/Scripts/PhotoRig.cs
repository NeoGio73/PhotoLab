using System.Collections;
using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// The camera's named views: the whole fume hood, the bench, the cups under the lamp, the
    /// lamp head and filter, the specimen shelf, the seagrass tank and the meter. Each view is
    /// framed by what has to fit in it, so it holds up at any canvas shape. The lenses pick a view
    /// and switch an overlay on; the view itself is just a place to look from.
    /// </summary>
    public class PhotoRig : MonoBehaviour
    {
        [System.Serializable]
        public class View
        {
            public string name;
            public Vector3 position;
            public Vector3 target;
            public float halfWidth = 3f;    // what must fit, measured at the target
            public float halfHeight = 2f;
        }

        public Camera cam;
        public View[] views = new View[0];
        public float travelTime = 0.65f;
        public float hoodTravelTime = 1.2f;
        public float introHold = 0.7f;
        public float introTravelTime = 2.0f;
        public bool calm;

        struct Pose { public Vector3 position; public Quaternion rotation; public float fov; }

        Pose from;
        float t = 1f;
        float duration = 0.65f;
        string current = "bench";
        bool introPlayed;

        public string Current { get { return current; } }
        public bool Moving { get { return t < 1f; } }

        void Start() { Apply(Solve(current)); }

        View Find(string view)
        {
            foreach (View v in views) if (v != null && v.name == view) return v;
            return null;
        }

        public bool IsView(string view) { return Find(view) != null; }

        public void Go(string view, bool instant) { Go(view, instant, -1f); }

        public void Go(string view, bool instant, float seconds)
        {
            if (!IsView(view)) return;
            if (view == current && !instant) return;
            if (calm) instant = true;
            from = new Pose { position = cam.transform.position, rotation = cam.transform.rotation, fov = cam.fieldOfView };
            if (seconds <= 0f) seconds = view == "hood" || current == "hood" ? hoodTravelTime : travelTime;
            duration = seconds;
            current = view;
            t = instant ? 1f : 0f;
            if (instant) Apply(Solve(current));
            PhotoBridge.Notify("{\"type\":\"view\",\"view\":\"" + current + "\"}");
            if (instant) PhotoBridge.Notify("{\"type\":\"arrived\",\"view\":\"" + current + "\"}");
        }

        public void PlayIntro()
        {
            if (introPlayed) return;
            introPlayed = true;
            if (calm || !isActiveAndEnabled) { Go("bench", true); return; }
            Go("hood", true);
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            float waited = 0f;
            while (waited < introHold)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                yield return null;
            }
            if (current == "hood" && !Moving) Go("bench", false, introTravelTime);
        }

        void LateUpdate()
        {
            Pose to = Solve(current);
            if (t < 1f)
            {
                t = Mathf.Min(1f, t + Mathf.Min(Time.unscaledDeltaTime, 0.1f) / Mathf.Max(duration, 0.01f));
                float e = t * t * t * (t * (6f * t - 15f) + 10f);
                Apply(new Pose
                {
                    position = Vector3.Lerp(from.position, to.position, e),
                    rotation = Quaternion.Slerp(from.rotation, to.rotation, e),
                    fov = Mathf.Exp(Mathf.Lerp(Mathf.Log(from.fov), Mathf.Log(to.fov), e))
                });
                if (t >= 1f) PhotoBridge.Notify("{\"type\":\"arrived\",\"view\":\"" + current + "\"}");
            }
            else Apply(to);
        }

        Pose Solve(string view)
        {
            View v = Find(view) ?? Find("bench");
            if (v == null) return new Pose { position = cam.transform.position, rotation = cam.transform.rotation, fov = cam.fieldOfView };
            float distance = Mathf.Max(0.01f, Vector3.Distance(v.position, v.target));
            float aspect = cam != null && cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : 1.5f;
            float need = Mathf.Max(v.halfHeight, v.halfWidth / Mathf.Max(aspect, 0.1f));
            return new Pose
            {
                position = v.position,
                rotation = Quaternion.LookRotation(v.target - v.position, Vector3.up),
                fov = Mathf.Clamp(2f * Mathf.Atan(need / distance) * Mathf.Rad2Deg, 1f, 100f)
            };
        }

        void Apply(Pose pose)
        {
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            cam.fieldOfView = pose.fov;
        }

        /// <summary>Used by the scene builder and editor screenshots: jump to a view without telling the page.</summary>
        public void Snap(string view)
        {
            if (!IsView(view)) return;
            current = view;
            t = 1f;
            Apply(Solve(current));
        }
    }
}
