using System;

namespace NuclearOptionObserver
{
    public static class MapCapturePolicy
    {
        public static bool CanCapture(bool context, bool ctrl, bool shift, bool alt, bool command, bool otherMouse) =>
            context && ctrl && !shift && !alt && !command && !otherMouse;
    }
    public sealed class MapHit<T> where T : class
    {
        public MapHit(object map, object icon, T target) { Map = map; Icon = icon; Target = target; }
        public object Map { get; }
        public object Icon { get; }
        public T Target { get; }
    }
    // One physical Ctrl+left gesture, shared by both native hook paths and LateUpdate.
    // Invalidated captures remain consumed no-ops until their paired mouse release has passed.
    public sealed class MapGesture<T> where T : class
    {
        private MapHit<T>? captured;
        private int downSeen = int.MinValue, pressFrame, releaseFrame = -1;
        private bool cancelled, dispatched;
        public ulong Serial { get; private set; }
        public bool HasCapture => captured != null;
        public void Observe(int frame, bool down, bool held, bool up, bool contextReady, bool modifierGesture, Func<MapHit<T>?> capture)
        {
            if (captured != null && ((releaseFrame >= 0 && frame > releaseFrame) || (!down && !held && !up && frame > pressFrame))) Reset();
            if (captured != null)
            {
                if (!contextReady) cancelled = true;
                if (up) releaseFrame = frame;
            }
            if (!down || downSeen == frame) return;
            downSeen = frame; // Even a rejected press cannot be captured later by a different callback/modifier state.
            if (captured != null || !contextReady || !modifierGesture) return;
            var hit = capture();
            if (hit == null) return;
            if (Serial == ulong.MaxValue) throw new InvalidOperationException("Observer map gesture counter exhausted.");
            ++Serial;
            captured = hit; pressFrame = frame; releaseFrame = up ? frame : -1;
            cancelled = false; dispatched = false;
        }
        public bool SuppressMapSelect(object map) => captured != null && ReferenceEquals(captured.Map, map);
        public bool SuppressIcon(object map, object icon, int source) => captured != null &&
            (source == 1 && SuppressMapSelect(map) || source == 0 && ReferenceEquals(captured.Icon, icon));
        public bool TryTake(out MapHit<T>? hit)
        {
            hit = null;
            if (captured == null || cancelled || dispatched) return false;
            dispatched = true; hit = captured; return true;
        }
        public void CancelIfMapChanged(object map) { if (captured != null && !ReferenceEquals(captured.Map, map)) Cancel(); }
        public void Cancel() { if (captured != null) cancelled = true; }
        public void Reset() { captured = null; cancelled = false; dispatched = false; releaseFrame = -1; }
    }
}
