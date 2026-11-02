using System;

namespace NuclearOptionObserver
{
    [Flags] internal enum HoldReason { None = 0, Map = 1, ExternalView = 2, MissileFullscreen = 4 }
    internal enum AxisOwner { Native, Hold, FullscreenNeutral }
    internal static class AxisArbiter
    { internal static AxisOwner Choose(bool manual, bool hold, bool fullscreen) => manual ? AxisOwner.Native : hold ? AxisOwner.Hold : fullscreen ? AxisOwner.FullscreenNeutral : AxisOwner.Native; }
    internal readonly struct HoldHudLayout
    {
        private HoldHudLayout(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;}
        internal float X{get;} internal float Y{get;} internal float Width{get;} internal float Height{get;}
        internal static HoldHudLayout Clamp(int width,int height){float w=Math.Max(1,width),h=Math.Max(1,height);return new HoldHudLayout(Math.Min(8,Math.Max(0,w-640)),Math.Max(0,h-88),Math.Min(640,w),Math.Min(80,h));}
        internal bool BlocksPointer(bool visible,float x,float y,int height)=>visible&&x>=X&&x<=X+Width&&height-y>=Y&&height-y<=Y+Height;
    }
    internal sealed class HoldLifecycle
    {
        private int emptyFrame = -1;
        internal bool Active { get; private set; }
        internal bool Latched { get; private set; }
        internal int Engagement { get; private set; }
        internal bool Evaluate(bool enabled, HoldReason reasons, int frame, bool paused, bool eligible)
        {
            if (!enabled) { Active = false; Latched = false; emptyFrame = -1; return false; }
            if (paused) return false;
            if (reasons == HoldReason.None)
            {
                if (emptyFrame < 0) emptyFrame = frame;
                else if (frame > emptyFrame) { Active = false; Latched = false; }
                return false;
            }
            emptyFrame = -1;
            if (!eligible) { if (Active) Fault(); return false; }
            if (Active || Latched) return false;
            Active = true; ++Engagement; return true;
        }
        internal void Override() { Active = false; Latched = true; }
        internal void Fault() { Active = false; Latched = true; }
        internal void Rearm() { if (!Active) { Latched = false; emptyFrame = -1; } }
    }
    internal readonly struct ThrottleHandover
    {
        internal static bool Convert(float command, bool collective, bool invert, bool negative, out float value)
        {
            value = 0;
            if (float.IsNaN(command) || float.IsInfinity(command)) return false;
            float t = Math.Max(0, Math.Min(1, command));
            if (collective && invert) t = 1 - t;
            value = negative ? 2 * t - 1 : t; return true;
        }
    }
    internal struct RawFlightInput
    {
        internal float Pitch, Roll, Yaw, Throttle, Custom;
        internal bool Enabled, RelativeThrottle, AxisModifier, Brake, Gear, Engine, Eject, Assist;
        internal bool VirtualJoystick { get; set; }
    }
    internal enum HoldInputResponse { None, Manual, SafetyRelease }
    internal sealed class ManualOverridePolicy
    {
        internal static bool ShouldObserve(bool active,bool fullscreen,bool drain)=>active||fullscreen||drain;
        internal static HoldInputResponse Response(bool manual,bool conflict)=>manual?HoldInputResponse.Manual:conflict?HoldInputResponse.SafetyRelease:HoldInputResponse.None;
        internal static bool VirtualStick(bool enabled,bool cockpit,bool mapOrUi,bool rmb,bool freeLook,float pan,float tilt)=>enabled&&cockpit&&!mapOrUi&&!rmb&&!freeLook&&(Math.Abs(pan)>.15f||Math.Abs(tilt)>.15f);
        private readonly RawFlightInput entry;
        private readonly float deadband;
        internal ManualOverridePolicy(RawFlightInput entry, float deadband = .15f) { this.entry = entry; this.deadband = Math.Max(.05f, Math.Min(.5f, deadband)); }
        internal bool Detect(RawFlightInput current)
        {
            if (!current.Enabled) return false; // Disabled native maps do not turn their zeros into throttle movement.
            if (!Finite(current.Pitch) || !Finite(current.Roll) || !Finite(current.Yaw) || !Finite(current.Throttle) || !Finite(current.Custom)) return true;
            return Math.Abs(current.Pitch) > deadband || Math.Abs(current.Roll) > deadband || Math.Abs(current.Yaw) > deadband ||
                current.VirtualJoystick || current.Brake || current.Gear || current.Engine || current.Eject || current.Assist ||
                (current.RelativeThrottle || current.AxisModifier ? Math.Abs(current.Throttle) > .1f : Math.Abs(current.Throttle - entry.Throttle) > .08f) ||
                Math.Abs(current.Custom - entry.Custom) > .05f;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
