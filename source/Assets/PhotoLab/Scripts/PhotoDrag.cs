using UnityEngine;
using UnityEngine.InputSystem;

namespace PhotoLab
{
    /// <summary>
    /// Mouse and touch handling for the photosynthesis bench, in the same manner as the other labs.
    ///
    /// Things are picked up by a tap or a drag and then stay in the hand ("sticky") until used or
    /// put down. In the hand:
    ///   the hole punch   - tap a specimen on the shelf to cut ten disks; tap elsewhere to put it back
    ///   a syringe        - tap the pile of disks to load them, a bottle to draw from it, a cup to
    ///                      pour, or the syringe itself (tap in empty space) to pull a vacuum;
    ///                      tap its rack to put it back
    ///   a filter gel     - tap the lamp head to slide it in; tap elsewhere to put it back
    /// The lamp head drags up and down its post. The timer is a button.
    /// </summary>
    public class PhotoDrag : MonoBehaviour
    {
        public PhotoManager lab;
        public Camera cam;
        public float carryY = 1.2f;
        public float minX = -3.2f, maxX = 3.2f, minZ = -2.2f, maxZ = 1.4f;

        enum Kind { None, Punch, Syringe, Filter, Stack, Bottle, Cup, Specimen, LampHead, Timer, Rack }

        Kind hoverKind = Kind.None;
        BenchItem hoverItem;

        BenchItem held;           // punch, syringe or filter
        Kind heldKind = Kind.None;
        bool sticky;
        Vector2 grabScreen;
        bool slidingLamp;
        bool clickWaiting;            // a tap made while the syringe was busy
        Vector2 clickWaitingAt;
        float lampGrabOffset;
        int generation = -1;
        BenchItem target;         // what the held thing would be used on
        Kind targetKind = Kind.None;
        Vector3 goal;

        InputAction pointAction;
        InputAction pressAction;
        bool pressQueued, releaseQueued;
        Vector2 pressPosition, releasePosition;
        int pagePresses, pageReleases;
        bool pageSeen;

        void OnEnable()
        {
            PhotoBridge.WatchPointer();
            pointAction = new InputAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            pressAction = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
            pressAction.started += OnPress;
            pressAction.canceled += OnRelease;
            pointAction.Enable();
            pressAction.Enable();
        }

        void OnDisable()
        {
            if (pressAction != null) { pressAction.started -= OnPress; pressAction.canceled -= OnRelease; pressAction.Disable(); pressAction.Dispose(); pressAction = null; }
            if (pointAction != null) { pointAction.Disable(); pointAction.Dispose(); pointAction = null; }
        }

        void OnPress(InputAction.CallbackContext context) { pressQueued = true; releaseQueued = false; pressPosition = PositionAt(context); }
        void OnRelease(InputAction.CallbackContext context) { releaseQueued = true; releasePosition = PositionAt(context); }

        Vector2 PositionAt(InputAction.CallbackContext context)
        {
            var pointer = context.control != null ? context.control.device as Pointer : null;
            return pointer != null ? pointer.position.ReadValue() : pointAction.ReadValue<Vector2>();
        }

        void ReadPointer(out Vector2 screen, out bool pressed, out bool released, out bool down)
        {
            PhotoBridge.PointerRecord page;
            if (PhotoBridge.ReadPointer(out page))
            {
                if (!pageSeen) { pageSeen = true; pagePresses = page.pressCount; pageReleases = page.releaseCount; }
                screen = page.position;
                pressed = page.pressCount != pagePresses;
                released = page.releaseCount != pageReleases;
                pagePresses = page.pressCount;
                pageReleases = page.releaseCount;
                if (pressed) pressPosition = page.pressPosition;
                if (released) releasePosition = page.releasePosition;
                down = page.down;
            }
            else
            {
                screen = pointAction.ReadValue<Vector2>();
                pressed = pressQueued;
                released = releaseQueued;
                down = pressAction.IsPressed();
            }
            pressQueued = false;
            releaseQueued = false;
        }

        void Update()
        {
            if (cam == null || lab == null || pointAction == null) return;

            if (lab.Generation != generation)
            {
                generation = lab.Generation;
                held = null; heldKind = Kind.None; hoverItem = null; target = null; targetKind = Kind.None;
                sticky = false; slidingLamp = false; hoverKind = Kind.None; clickWaiting = false;
            }

            Vector2 screen;
            bool pressed, released, down;
            ReadPointer(out screen, out pressed, out released, out down);

            if (held == null && !slidingLamp)
            {
                Vector2 probe = pressed ? pressPosition : screen;
                bool inside = Inside(probe);
                string view = lab.rig != null ? lab.rig.Current : "bench";
                bool moving = lab.rig != null && lab.rig.Moving;
                bool overview = view == "hood";

                Kind kind = Kind.None;
                BenchItem item = null;
                if (inside && !overview && !moving) Pick(probe, out kind, out item);
                SetHover(kind, item);

                string cursor = "default";
                if (overview) cursor = inside && !moving ? "zoom-in" : "default";
                else if (kind == Kind.Punch || kind == Kind.Syringe || kind == Kind.Filter) cursor = "grab";
                else if (kind == Kind.LampHead) cursor = "ns-resize";
                else if (kind == Kind.Timer) cursor = "pointer";
                PhotoBridge.Cursor(cursor);

                if (!pressed || !inside) return;
                if (overview) { if (!moving) lab.SetView("bench"); return; }
                if (kind == Kind.Punch || kind == Kind.Syringe || kind == Kind.Filter) Grab(item, kind);
                else if (kind == Kind.LampHead) StartLampSlide(probe);
                else if (kind == Kind.Timer) { lab.ToggleTimer(); return; }
                if (held == null && !slidingLamp) return;
                sticky = false;
                grabScreen = probe;
            }

            bool letGo = !down;
            Vector2 at = letGo && released ? releasePosition : screen;
            if (slidingLamp)
            {
                SlideLamp(at);
                if (letGo) { slidingLamp = false; lab.LampSettled(); PhotoBridge.Cursor("ns-resize"); }
                return;
            }
            Syringe s = held as Syringe;
            if (s != null && s.busy)
            {
                // mid-pull: the syringe is not following the pointer; a tap made now is kept for when it is free
                if (pressed) { clickWaiting = true; clickWaitingAt = pressPosition; }
                return;
            }
            if (clickWaiting)
            {
                clickWaiting = false;
                if (sticky && !pressed) { pressed = true; pressPosition = clickWaitingAt; }
            }

            if (sticky)
            {
                if (pressed) { Carry(pressPosition, true); Act(); return; }
                Carry(screen, false);
                return;
            }
            if (letGo && (at - grabScreen).magnitude <= TapSlop())
            {
                sticky = true;
                lab.NotifyHover("holding-" + heldKind.ToString().ToLower(), held.id);
                Carry(at, false);
                return;
            }
            Carry(at, letGo);
            if (letGo) { Act(); if (held != null && sticky) lab.NotifyHover("holding-" + heldKind.ToString().ToLower(), held.id); }
        }

        float TapSlop() { return Mathf.Max(8f, cam.pixelHeight * 0.02f); }
        bool Inside(Vector2 p) { return p.x >= 0 && p.y >= 0 && p.x <= cam.pixelWidth && p.y <= cam.pixelHeight; }

        // ---- What is under the pointer -----------------------------------------------------------

        void Pick(Vector2 screen, out Kind kind, out BenchItem item)
        {
            kind = Kind.None; item = null;
            float pad = Mathf.Max(5f, cam.pixelHeight * 0.01f);
            float best = float.MaxValue;
            float score;
            // small things first, so a syringe standing in front of a cup wins
            if (lab.punch != null && !lab.punch.held && Hit(lab.punch.WorldBounds, screen, pad, pad, out score) && score < best) { best = score; kind = Kind.Punch; item = lab.punch; }
            foreach (Syringe sy in lab.syringes)
                if (sy != null && !sy.held && Hit(sy.WorldBounds, screen, pad, 0f, out score) && score < best) { best = score; kind = Kind.Syringe; item = sy; }
            foreach (FilterGel g in lab.filters)
                if (g != null && !g.held && !g.inSlot && Hit(g.WorldBounds, screen, pad, pad, out score) && score < best) { best = score; kind = Kind.Filter; item = g; }
            if (kind != Kind.None) return;
            if (lab.lampHead != null && OverTransform(lab.lampHead, screen, 0f)) { kind = Kind.LampHead; return; }
            if (lab.timerBody != null && Over(lab.timerBody.bounds, screen, 0f)) { kind = Kind.Timer; return; }
        }

        /// <summary>The thing a held item would be used on if let go here.</summary>
        void PickTarget(Vector2 screen, out Kind kind, out BenchItem item)
        {
            kind = Kind.None; item = null;
            float pad = Mathf.Max(6f, cam.pixelHeight * 0.012f);
            float best = float.MaxValue;
            float score;
            if (heldKind == Kind.Punch)
            {
                foreach (Specimen sp in lab.specimens)
                    if (sp != null && Hit(sp.WorldBounds, screen, pad, pad, out score) && score < best) { best = score; kind = Kind.Specimen; item = sp; }
            }
            else if (heldKind == Kind.Syringe)
            {
                if (lab.stack != null && lab.stack.count > 0 && Hit(lab.stack.WorldBounds, screen, pad * 2f, pad * 2f, out score) && score < best) { best = score; kind = Kind.Stack; item = lab.stack; }
                foreach (Bottle b in lab.bottles)
                    if (b != null && Hit(b.WorldBounds, screen, pad, pad, out score) && score < best) { best = score; kind = Kind.Bottle; item = b; }
                foreach (Cup c in lab.cups)
                    if (c != null && Hit(c.WorldBounds, screen, pad, pad, out score) && score < best) { best = score; kind = Kind.Cup; item = c; }
                // the rack competes with the cups on distance, so a tap on it wins even where a cup overlaps it on screen
                if (lab.syringeRack != null && Hit(lab.syringeRack.bounds, screen, pad * 2f, pad * 3f, out score) && score < best) { best = score; kind = Kind.Rack; item = null; }
            }
            else if (heldKind == Kind.Filter)
            {
                if (lab.lampHead != null && OverTransform(lab.lampHead, screen, pad * 2f)) { kind = Kind.LampHead; }
            }
        }

        void SetHover(Kind kind, BenchItem item)
        {
            if (kind == hoverKind && item == hoverItem) return;
            if (hoverItem != null) hoverItem.SetHover(false);
            hoverItem = item;
            if (hoverItem != null) hoverItem.SetHover(true);
            hoverKind = kind;
            lab.NotifyHover(kind.ToString().ToLower(), item != null ? item.id : "");
        }

        // ---- Carrying -------------------------------------------------------------------------------

        void Grab(BenchItem item, Kind kind)
        {
            SetHover(Kind.None, null);
            held = item;
            heldKind = kind;
            item.held = true;
            FilterGel g = item as FilterGel;
            if (g != null && g.inSlot) { g.inSlot = false; lab.RemoveFilter(); }
            goal = item.transform.position;
            PhotoBridge.Cursor("grabbing");
        }

        void Carry(Vector2 screen, bool arrive)
        {
            float follow = arrive ? 1f : 1f - Mathf.Exp(-22f * Time.deltaTime);
            Kind tk; BenchItem ti;
            PickTarget(screen, out tk, out ti);
            if (ti != target)
            {
                if (target != null) target.SetHover(false);
                target = ti;
                if (target != null) target.SetHover(true);
            }
            targetKind = tk;
            Vector3 point;
            if (ti != null) goal = ti.WorldBounds.center + Vector3.up * (ti.WorldBounds.extents.y + 0.35f);
            else if (tk == Kind.LampHead && lab.lampHead != null) goal = lab.lampHead.position + Vector3.down * 0.3f;
            else if (PlanePoint(screen, carryY, out point)) goal = new Vector3(Mathf.Clamp(point.x, minX, maxX), carryY, Mathf.Clamp(point.z, minZ, maxZ));
            held.transform.position = Vector3.Lerp(held.transform.position, goal, follow);
            if (heldKind == Kind.Filter) held.transform.rotation = Quaternion.Slerp(held.transform.rotation, Quaternion.Euler(tk == Kind.LampHead ? 90f : 0f, 0f, 0f), follow);
        }

        /// <summary>Uses what is held on what it is over.</summary>
        void Act()
        {
            BenchItem t = target;
            Kind tk = targetKind;
            if (target != null) target.SetHover(false);
            target = null; targetKind = Kind.None;

            if (heldKind == Kind.Punch)
            {
                if (tk == Kind.Specimen) lab.Punch((Specimen)t);
                PutDown();   // a one-shot tool: back to its place either way
            }
            else if (heldKind == Kind.Syringe)
            {
                Syringe s = (Syringe)held;
                if (tk == Kind.Stack) { lab.LoadDisks(s); sticky = true; return; }
                if (tk == Kind.Bottle) { lab.Draw(s, (Bottle)t); sticky = true; return; }
                if (tk == Kind.Cup) { lab.Pour(s, (Cup)t); sticky = true; return; }
                if (tk == Kind.Rack) { PutDown(); return; }
                // tapped in empty space: a vacuum pull, if there is anything to pull against
                if (s.volumeMl > 0f) { lab.PullVacuum(s); sticky = true; return; }
                PutDown();
            }
            else if (heldKind == Kind.Filter)
            {
                FilterGel g = (FilterGel)held;
                if (tk == Kind.LampHead) { held = null; heldKind = Kind.None; sticky = false; lab.InsertFilter(g); PhotoBridge.Cursor("default"); return; }
                PutDown();
            }
        }

        void PutDown()
        {
            if (held != null) held.GoHome();
            if (heldKind == Kind.Filter) held.transform.rotation = Quaternion.identity;
            held = null; heldKind = Kind.None; sticky = false;
            lab.NotifyHover("", "");
            PhotoBridge.Cursor("default");
        }

        // ---- The lamp on its post ------------------------------------------------------------------

        void StartLampSlide(Vector2 screen)
        {
            float y;
            if (!PostY(screen, out y)) return;
            slidingLamp = true;
            lampGrabOffset = Bench.LampFace(lab.lampDistanceCm).y - y;
            PhotoBridge.Cursor("grabbing");
        }

        void SlideLamp(Vector2 screen)
        {
            float y;
            if (!PostY(screen, out y)) return;
            float faceY = y + lampGrabOffset;
            float cm = (faceY - Bench.CupWaterSurfaceY) / Bench.Cm;
            lab.SetLamp(Mathf.Round(cm));
        }

        /// <summary>Height of the pointer on a vertical plane through the lamp head, facing the camera.</summary>
        bool PostY(Vector2 screen, out float y)
        {
            y = 0f;
            Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            var plane = new Plane(Vector3.back, new Vector3(0f, 0f, Bench.CupRowZ));
            float t;
            if (!plane.Raycast(ray, out t)) return false;
            y = ray.GetPoint(t).y;
            return true;
        }

        // ---- Geometry ------------------------------------------------------------------------------

        bool PlanePoint(Vector2 screen, float y, out Vector3 point)
        {
            Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            point = Vector3.zero;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }

        bool OverTransform(Transform t, Vector2 screen, float pad)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return false;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return Over(b, screen, pad);
        }

        bool Over(Bounds bounds, Vector2 screen, float pad) { float s; return Hit(bounds, screen, pad, pad, out s); }

        bool Hit(Bounds bounds, Vector2 screen, float padX, float padY, out float score)
        {
            score = float.MaxValue;
            Rect r;
            if (!ScreenRect(bounds, out r)) return false;
            r.xMin -= padX; r.xMax += padX; r.yMin -= padY; r.yMax += padY;
            if (!r.Contains(screen)) return false;
            float dx = (screen.x - r.center.x) / Mathf.Max(r.width, 1f), dy = (screen.y - r.center.y) / Mathf.Max(r.height, 1f);
            score = dx * dx + dy * dy;
            return true;
        }

        bool ScreenRect(Bounds bounds, out Rect rect)
        {
            Vector3 c = bounds.center, e = bounds.extents;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            rect = new Rect();
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x), c.y + ((i & 2) == 0 ? -e.y : e.y), c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 s = cam.WorldToScreenPoint(corner);
                if (s.z <= 0f) return false;
                xMin = Mathf.Min(xMin, s.x); xMax = Mathf.Max(xMax, s.x);
                yMin = Mathf.Min(yMin, s.y); yMax = Mathf.Max(yMax, s.y);
            }
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }
    }
}
