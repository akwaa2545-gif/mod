using System;

namespace NuclearOptionObserver
{
    // Transient viewing state only. It owns no native pilot/camera/input/cursor state.
    internal sealed class FullscreenState
    {
        internal bool Active { get; private set; }
        internal float Yaw { get; private set; }
        internal float Pitch { get; private set; }
        internal bool Toggle(bool allowed)
        {
            if (Active) { Ensure(false); return true; }
            if (!allowed) return false;
            Active = true; ResetOrbit(); return true;
        }
        internal bool CanRetainManualSelection(bool sameOwner, bool targetReady) => Active && sameOwner && targetReady;
        internal bool Orbit(float horizontalDegrees, float verticalDegrees)
        {
            if (!Active || float.IsNaN(horizontalDegrees) || float.IsInfinity(horizontalDegrees) || float.IsNaN(verticalDegrees) || float.IsInfinity(verticalDegrees)) return false;
            Yaw = ((Yaw + horizontalDegrees) % 360 + 540) % 360 - 180;
            Pitch = Math.Max(-80, Math.Min(80, Pitch + verticalDegrees));
            return true;
        }
        internal void ResetOrbit() { Yaw = 0; Pitch = 0; }
        internal void Ensure(bool allowed) { if (!allowed) { Active = false; ResetOrbit(); } }
    }
    internal readonly struct RenderSize
    {
        private RenderSize(int width, int height) { Width = width; Height = height; }
        internal int Width { get; }
        internal int Height { get; }
        internal static RenderSize ForScreen(bool fullscreen, int width, int height)
        {
            if (!fullscreen) return new RenderSize(480, 270);
            width = Math.Max(1, width); height = Math.Max(1, height);
            double scale = Math.Min(1, Math.Min(1280d / width, 720d / height));
            return new RenderSize(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        }
    }
    internal readonly struct FullscreenKeys
    {
        private FullscreenKeys(int fullscreen, int reset) { Fullscreen = fullscreen; Reset = reset; }
        internal int Fullscreen { get; }
        internal int Reset { get; }
        internal static FullscreenKeys Normalize(int panel, int view, int next, int full, int reset)
        {
            if (!Available(full, panel, view, next, -1)) full = Free(panel, view, next, reset);
            if (!Available(reset, panel, view, next, full)) reset = Free(panel, view, next, full);
            return new FullscreenKeys(full, reset);
        }
        private static bool Available(int key, int panel, int view, int next, int other) => key >= 282 && key <= 293 && key != panel && key != view && key != next && key != other;
        private static int Free(int panel, int view, int next, int other)
        { for (int key = 282; key <= 293; key++) if (Available(key, panel, view, next, other)) return key; throw new InvalidOperationException("No unused Observer function key."); }
    }
}
