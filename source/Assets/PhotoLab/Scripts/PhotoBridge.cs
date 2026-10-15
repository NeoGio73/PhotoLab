using System.Runtime.InteropServices;
using UnityEngine;

namespace PhotoLab
{
    /// <summary>
    /// The link between the 3D lab and the web page it is embedded in: messages to the page, the
    /// mouse cursor, and the page's own record of the pointer. Does nothing outside a browser build.
    /// Same shape as the other labs' bridges so the page code can be shared.
    /// </summary>
    public static class PhotoBridge
    {
        public struct PointerRecord
        {
            public Vector2 position;
            public bool down;
            public int pressCount;
            public Vector2 pressPosition;
            public int releaseCount;
            public Vector2 releasePosition;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PhotoLabNotify(string json);
        [DllImport("__Internal")] static extern void PhotoLabCursor(string css);
        [DllImport("__Internal")] static extern void PhotoLabPointerInit();
        [DllImport("__Internal")] static extern int PhotoLabPointerRead(float[] nine);

        static readonly float[] pointerNumbers = new float[9];
#endif

        /// <summary>Raised for every message, so the editor and tests can listen in.</summary>
        public static event System.Action<string> Sent;

        static string lastCursor;

        public static void Notify(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PhotoLabNotify(json);
#endif
            if (Sent != null) Sent(json);
        }

        public static void Cursor(string css)
        {
            if (css == lastCursor) return;
            lastCursor = css;
#if UNITY_WEBGL && !UNITY_EDITOR
            PhotoLabCursor(css);
#endif
        }

        public static void WatchPointer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PhotoLabPointerInit();
#endif
        }

        public static bool ReadPointer(out PointerRecord pointer)
        {
            pointer = new PointerRecord();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PhotoLabPointerRead(pointerNumbers) == 0) return false;
            pointer.position = new Vector2(pointerNumbers[0], pointerNumbers[1]);
            pointer.down = pointerNumbers[2] > 0.5f;
            pointer.pressCount = (int)pointerNumbers[3];
            pointer.pressPosition = new Vector2(pointerNumbers[4], pointerNumbers[5]);
            pointer.releaseCount = (int)pointerNumbers[6];
            pointer.releasePosition = new Vector2(pointerNumbers[7], pointerNumbers[8]);
            return true;
#else
            return false;
#endif
        }
    }
}
