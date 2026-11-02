using System;

namespace NuclearOptionObserver
{
    // Small data-only seam: no Unity, cockpit pose, previous camera pose or missile roll input.
    internal readonly struct ChaseVector
    {
        internal ChaseVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        internal float X { get; }
        internal float Y { get; }
        internal float Z { get; }
    }
    internal readonly struct ChasePose
    {
        internal ChasePose(ChaseVector position, ChaseVector forward, ChaseVector up) { Position = position; Forward = forward; Up = up; }
        internal ChaseVector Position { get; }
        internal ChaseVector Forward { get; }
        internal ChaseVector Up { get; }
    }
    internal static class ChaseView
    {
        internal static bool TryOrbit(ChaseVector target, ChaseVector forward, float yaw, float pitch, out ChasePose pose, out string reason)
        {
            pose = default; reason = "Nonfinite orbit input.";
            if (!Finite(yaw) || !Finite(pitch)) return false;
            if (!TryCreate(target, forward, out var baseline, out reason)) return false;
            if (yaw == 0 && pitch == 0) { pose = baseline; return true; }
            double x = baseline.Position.X - target.X, y = baseline.Position.Y - target.Y, z = baseline.Position.Z - target.Z;
            double radius = Math.Sqrt(x*x + y*y + z*z), radians = Math.PI / 180;
            double azimuth = Math.Atan2(x, z) + (yaw % 360) * radians;
            double elevation = Math.Max(-Math.PI/2 + .001, Math.Min(Math.PI/2 - .001, Math.Atan2(y, Math.Sqrt(x*x+z*z)) + Math.Max(-80, Math.Min(80,pitch))*radians));
            var position = new ChaseVector(target.X + (float)(radius*Math.Sin(azimuth)*Math.Cos(elevation)), target.Y + (float)(radius*Math.Sin(elevation)), target.Z + (float)(radius*Math.Cos(azimuth)*Math.Cos(elevation)));
            return LookAt(target, position, out pose, out reason);
        }
        internal static bool TryCreate(ChaseVector target, ChaseVector forward, out ChasePose pose, out string reason)
        {
            pose = default;
            reason = "Nonfinite missile position or forward.";
            if (!Finite(target) || !Finite(forward)) return false;
            reason = "Missile forward is zero or too small.";
            if (!Normalize(forward, out var direction)) return false;
            // World-up offset, not missile/cockpit up. Banking cannot orbit the chase camera.
            var position = new ChaseVector(target.X - direction.X * 7f, target.Y - direction.Y * 7f + 1.5f, target.Z - direction.Z * 7f);
            return LookAt(target, position, out pose, out reason);
        }
        private static bool LookAt(ChaseVector target, ChaseVector position, out ChasePose pose, out string reason)
        {
            pose = default;
            reason = "Chase offset is nonfinite or unrepresentable at this position.";
            if (!Finite(position) || !Normalize(new ChaseVector(target.X - position.X, target.Y - position.Y, target.Z - position.Z), out var look)) return false;
            // Project world up perpendicular to the actual target-centered look direction.
            var right = new ChaseVector(look.Z, 0, -look.X);
            if (LengthSquared(right) < 0.000001)
            {
                // The horizon is undefined near vertical; fixed world-right gives a deterministic,
                // roll-independent basis without amplifying tiny horizontal direction noise.
                right = new ChaseVector(1 - look.X * look.X, -look.X * look.Y, -look.X * look.Z);
            }
            reason = "Cannot construct a finite stabilized view basis.";
            if (!Normalize(right, out right) || !Normalize(Cross(look, right), out var up)) return false;
            pose = new ChasePose(position, look, up);
            reason = "";
            return true;
        }
        private static bool Finite(ChaseVector v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static double LengthSquared(ChaseVector v) => (double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z;
        private static bool Normalize(ChaseVector v, out ChaseVector result)
        {
            result = default;
            double lengthSquared = LengthSquared(v);
            if (!Finite(v) || lengthSquared < 0.000000000001) return false;
            double length = Math.Sqrt(lengthSquared);
            result = new ChaseVector((float)(v.X / length), (float)(v.Y / length), (float)(v.Z / length));
            return Finite(result);
        }
        private static ChaseVector Cross(ChaseVector a, ChaseVector b) =>
            new ChaseVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }
}
