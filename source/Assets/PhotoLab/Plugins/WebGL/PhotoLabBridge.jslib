mergeInto(LibraryManager.library, {
  PhotoLabNotify: function (ptr) {
    var json = UTF8ToString(ptr);
    try {
      if (window.PhotoLab && window.PhotoLab.onUnityEvent) window.PhotoLab.onUnityEvent(json);
    } catch (e) {
      console.error(e);
    }
  },

  PhotoLabCursor: function (ptr) {
    var css = UTF8ToString(ptr);
    var canvas = Module.canvas || document.getElementById("unity-canvas");
    if (canvas) canvas.style.cursor = css;
  },

  // Watches the pointer on the page itself. The lab needs to know exactly where the pointer was
  // at the instant the button went down or came up; the engine's own input only says where the
  // pointer is by the time the next frame is drawn, which on a slow computer is somewhere else.
  // Positions are in canvas pixels measured from the bottom-left corner, as the engine counts them.
  PhotoLabPointerInit: function () {
    var canvas = Module.canvas || document.getElementById("unity-canvas");
    if (!canvas || Module.photoLabPointer) return;
    var state = Module.photoLabPointer = {
      x: -1, y: -1, down: 0,
      downCount: 0, downX: 0, downY: 0,
      upCount: 0, upX: 0, upY: 0,
      pointerId: null
    };
    function place(e) {
      var rect = canvas.getBoundingClientRect();
      var sx = rect.width > 0 ? canvas.width / rect.width : 1;
      var sy = rect.height > 0 ? canvas.height / rect.height : 1;
      state.x = (e.clientX - rect.left) * sx;
      state.y = canvas.height - (e.clientY - rect.top) * sy;
    }
    function release() {
      state.upX = state.x;
      state.upY = state.y;
      state.upCount += 1;
      state.down = 0;
      state.pointerId = null;
    }
    canvas.addEventListener("pointerdown", function (e) {
      if (e.button !== 0 || state.down) return;
      place(e);
      state.downX = state.x;
      state.downY = state.y;
      state.downCount += 1;
      state.down = 1;
      state.pointerId = e.pointerId;
      // keep receiving this pointer when it leaves the canvas or the window mid-drag
      try { canvas.setPointerCapture(e.pointerId); } catch (err) { /* older browsers */ }
    }, true);
    window.addEventListener("pointermove", function (e) {
      if (state.down ? e.pointerId === state.pointerId : e.isPrimary !== false) place(e);
    }, true);
    function lift(e) {
      if (!state.down || e.pointerId !== state.pointerId) return;
      place(e);
      release();
    }
    window.addEventListener("pointerup", lift, true);
    window.addEventListener("pointercancel", lift, true);
    window.addEventListener("blur", function () { if (state.down) release(); });
  },

  // Fills nine numbers: x, y, down (0 or 1), press count, press x, press y, release count,
  // release x, release y. Returns 0 until PhotoLabPointerInit has run.
  PhotoLabPointerRead: function (ptr) {
    var s = Module.photoLabPointer;
    if (!s) return 0;
    var i = ptr >> 2;
    HEAPF32[i] = s.x;
    HEAPF32[i + 1] = s.y;
    HEAPF32[i + 2] = s.down;
    HEAPF32[i + 3] = s.downCount;
    HEAPF32[i + 4] = s.downX;
    HEAPF32[i + 5] = s.downY;
    HEAPF32[i + 6] = s.upCount;
    HEAPF32[i + 7] = s.upX;
    HEAPF32[i + 8] = s.upY;
    return 1;
  }
});
