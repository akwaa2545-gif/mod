using System;
using System.Threading;

namespace NuclearOptionObserver
{
    // Scope is current-thread only; no input maps/global game flags are changed.
    internal sealed class ScopedCameraMask
    {
        private readonly object player;
        private readonly bool freeLook;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private int depth;
        internal ScopedCameraMask(object player, bool freeLook) { this.player = player; this.freeLook = freeLook; }
        internal IDisposable? Enter()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) return null;
            Interlocked.Increment(ref depth); return new Lease(this);
        }
        private bool Owns(object queryPlayer) => Thread.CurrentThread.ManagedThreadId == thread && Volatile.Read(ref depth) > 0 && ReferenceEquals(player, queryPlayer);
        internal bool Axis(object queryPlayer, string action, out float value)
        {
            value = 0;
            return Owns(queryPlayer) && (action == "Pan View" || action == "Tilt View" || action == "Zoom View" || action == "FOV" || action == "Move Vertical" || action == "Move Longitudinal" || action == "Move Lateral");
        }
        internal bool Button(object queryPlayer, string action, out bool value)
        {
            value = false;
            if (!Owns(queryPlayer)) return false;
            if (action == "Free Look") { value = freeLook; return true; }
            return action == "Center" || action == "Switch View" || action == "Change Spectate Faction" || action == "Spectate Next Aircraft" || action == "Cycle Look At";
        }
        private sealed class Lease : IDisposable
        {
            private readonly ScopedCameraMask owner;
            private int disposed;
            internal Lease(ScopedCameraMask owner) { this.owner = owner; }
            public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Decrement(ref owner.depth); }
        }
    }
    internal readonly struct HeldManualInput
    {
        internal float Pitch => 0; internal float Roll => 0; internal float Yaw => 0; internal float Brake => 0;
        private HeldManualInput(float throttle, float custom) { Throttle = throttle; CustomAxis = custom; }
        internal float Throttle { get; } internal float CustomAxis { get; }
        internal static bool TryCapture(float throttle, float custom, out HeldManualInput command)
        {
            command = default;
            if (float.IsNaN(throttle) || float.IsInfinity(throttle) || float.IsNaN(custom) || float.IsInfinity(custom)) return false;
            command = new HeldManualInput(throttle, custom); return true;
        }
    }
    internal enum PilotInputRoot { Buttons, Axes }
    internal static class PilotInputBoundary
    {
        internal static bool AllowNative(bool captured, object actual, object owner, PilotInputRoot root, Action replaceAxes) { if (!captured || !ReferenceEquals(actual,owner)) return true; if (root == PilotInputRoot.Axes) replaceAxes(); return false; }
    }
    internal static class ExitActions
    {
        // Actual button queries in the suppressed local pilot roots (including throttle modifier)
        // and scoped cockpit/orbit queries. No absolute flight axes: those must not hold a drain forever.
        private static readonly string[] actions = {
            "Brake", "Next Weapon", "Previous Weapon", "Turret Control", "Gear", "Eject", "Fire", "Countermeasures",
            "Next Countermeasure", "Flight Assist", "Radar", "Nav Lights", "Toggle Engine", "Link Guns", "Axis Modifier",
            "Center", "Switch View", "Change Spectate Faction", "Spectate Next Aircraft", "Cycle Look At", "Free Look"
        };
        internal static bool AnyHeld(Func<string, bool> held)
        { foreach (var action in actions) if (held(action)) return true; return false; }
    }
    internal sealed class ReleaseBoundary
    {
        private int releaseFrame = -1;
        internal bool Active { get; private set; }
        internal void Begin() { Active = true; releaseFrame = -1; }
        internal bool Poll(int frame, bool held, bool valid)
        {
            if (!valid) Cancel();
            if (!Active) return false;
            if (held) releaseFrame = -1;
            else if (releaseFrame < 0) releaseFrame = frame;
            else if (frame > releaseFrame) Cancel();
            return Active;
        }
        internal void Cancel() { Active = false; releaseFrame = -1; }
    }
}
