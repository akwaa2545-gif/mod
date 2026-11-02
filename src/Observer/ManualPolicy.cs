using System;
using System.Collections.Generic;

namespace NuclearOptionObserver
{
    public sealed class MissileCandidate<T> where T : class
    {
        public MissileCandidate(T target, ulong id, long epoch, string label)
        { Target = target; Id = id; Epoch = epoch; Label = label; }
        public T Target { get; }
        public ulong Id { get; }
        public long Epoch { get; }
        public string Label { get; }
        public bool MatchesIdentity(T target, ulong id, long epoch) => ReferenceEquals(Target, target) && Id != 0 && Id == id && Epoch == epoch;
    }
    public sealed class MissileRow<T> where T : class
    {
        public MissileRow(MissileCandidate<T> candidate, EntityToken token) { Candidate = candidate; Token = token; }
        public MissileCandidate<T> Candidate { get; }
        public EntityToken Token { get; }
        public string Label => "#" + Candidate.Id + "/" + Token.LifetimeId + " " + Candidate.Label;
    }
    // Refresh discovers only. Selection and visibility can change only through explicit commands or invalidation.
    public sealed class ManualCatalog<T> where T : class
    {
        private readonly ulong context;
        private ulong lifetime;
        private List<MissileRow<T>> rows = new List<MissileRow<T>>();
        public ManualCatalog(ulong context) { if (context == 0) throw new ArgumentOutOfRangeException(nameof(context)); this.context = context; }
        public IReadOnlyList<MissileRow<T>> Rows => rows;
        public MissileRow<T>? Selected { get; private set; }
        public bool Visible { get; private set; }
        public void Refresh(IEnumerable<MissileCandidate<T>> candidates)
        {
            var next = new List<MissileRow<T>>();
            foreach (var candidate in candidates)
            {
                if (candidate.Id == 0 || candidate.Epoch < 0) continue;
                if (next.Exists(r => ReferenceEquals(r.Candidate.Target, candidate.Target))) continue;
                var old = rows.Find(r => ReferenceEquals(r.Candidate.Target, candidate.Target) && r.Candidate.Id == candidate.Id && r.Candidate.Epoch == candidate.Epoch);
                if (old == null && lifetime == ulong.MaxValue) throw new InvalidOperationException("Observer lifetime exhausted.");
                next.Add(new MissileRow<T>(candidate, old?.Token ?? new EntityToken(context, ++lifetime)));
            }
            next.Sort((a, b) => a.Candidate.Id.CompareTo(b.Candidate.Id));
            rows = next;
            if (Selected != null) Selected = rows.Find(r => Phase1Policy.IsCurrent(Selected.Token, r.Token));
        }
        public MissileRow<T>? Observe(MissileCandidate<T> candidate)
        {
            if (candidate.Id == 0 || candidate.Epoch < 0) return null;
            var candidates = new List<MissileCandidate<T>>();
            foreach (var row in rows) if (!ReferenceEquals(row.Candidate.Target, candidate.Target)) candidates.Add(row.Candidate);
            candidates.Add(candidate);
            Refresh(candidates); // Only the known candidate changes; never a game/global discovery scan.
            return rows.Find(r => candidate.MatchesIdentity(r.Candidate.Target, r.Candidate.Id, r.Candidate.Epoch));
        }
        public bool Select(EntityToken token, bool show = false)
        {
            var row = rows.Find(r => Phase1Policy.IsCurrent(token, r.Token));
            if (row == null) return false;
            Selected = row;
            if (show) Visible = true;
            return true;
        }
        public void Clear() => Selected = null;
        public void ToggleVisible() => Visible = !Visible;
        public MissileRow<T>? Adjacent(int direction)
        {
            if (rows.Count == 0) return null;
            int current = Selected == null ? -1 : rows.FindIndex(r => Phase1Policy.IsCurrent(Selected.Token, r.Token));
            int index = current < 0 ? direction < 0 ? rows.Count - 1 : 0 : (current + (direction < 0 ? -1 : 1) + rows.Count) % rows.Count;
            return rows[index];
        }

    }
    public sealed class RefreshCadence
    {
        private double next = double.NegativeInfinity;
        public bool Due(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < next) return false;
            next = now + 0.5;
            return true;
        }
    }
    public readonly struct ViewLayout
    {
        public const int StatusHeight = 40;
        private ViewLayout(float x, float y, float width, float height, float scale) { X = x; Y = y; Width = width; Height = height; Scale = scale; }
        public float X { get; } public float Y { get; } public float Width { get; } public float Height { get; } public float Scale { get; }
        public bool BlocksPointer(bool visible, float screenX, float screenY, int screenHeight)
        {
            if (!visible || float.IsNaN(screenX) || float.IsInfinity(screenX) || float.IsNaN(screenY) || float.IsInfinity(screenY)) return false;
            float guiY = screenHeight - screenY;
            return screenX >= X && screenX < X + Width && guiY >= Y && guiY < Y + Height + StatusHeight;
        }
        internal static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        public static ViewLayout Clamp(float x, float y, float width, float scale, int sw, int sh)
        {
            sw = Math.Max(1, sw); sh = Math.Max(StatusHeight + 1, sh);
            scale = Math.Max(0.75f, Math.Min(1.5f, Finite(scale, 1)));
            width = Math.Max(1, Math.Min(Math.Min(sw, (sh - StatusHeight) * 16f / 9f), Math.Max(240, Finite(width, 480)) * scale));
            float height = width * 9f / 16f;
            return new ViewLayout(Math.Max(0, Math.Min(sw - width, Finite(x, 16))), Math.Max(0, Math.Min(sh - height - StatusHeight, Finite(y, 16))), width, height, scale);
        }
    }
    public sealed class ObserverPreferences
    {
        public bool Enabled { get; private set; } public int Schema => 2; public bool StartVisible => false;
        public float X { get; private set; } public float Y { get; private set; } public float Width { get; private set; } public float Scale { get; private set; }
        public int PanelKey { get; private set; } public int ViewKey { get; private set; } public int CycleKey { get; private set; }
        public static ObserverPreferences Normalize(bool enabled, int schema, float x, float y, float width, float scale, int panel, int view, int cycle)
        {
            if (!ShortcutPolicy.Valid(panel, view, cycle)) { panel = 289; view = 290; cycle = 291; }
            return new ObserverPreferences { Enabled = enabled, X = Math.Max(0, ViewLayout.Finite(x, 16)), Y = Math.Max(0, ViewLayout.Finite(y, 16)), Width = Math.Max(240, Math.Min(1280, ViewLayout.Finite(width, 480))), Scale = Math.Max(.75f, Math.Min(1.5f, ViewLayout.Finite(scale, 1))), PanelKey = panel, ViewKey = view, CycleKey = cycle };
        }
    }
    public static class ShortcutPolicy
    {
        public static bool ValidFive(int panel, int view, int next, int full, int reset) => Valid(panel, view, next) && Valid(full, reset, panel) && full != view && full != next && reset != view && reset != next;
        // Inspected Unity EventModifiers: Shift/Control/Alt/Command are low four bits; FunctionKey is 64.
        public static bool NoChordModifiers(int flags) => (flags & 15) == 0;
        // Unity KeyCode F1..F12. Excludes firing/mouse/confirm/pause keys. Native user conflicts still require user review.
        public static bool Valid(int panel, int view, int cycle) => panel >= 282 && panel <= 293 && view >= 282 && view <= 293 && cycle >= 282 && cycle <= 293 && panel != view && panel != cycle && view != cycle;
    }
    public static class InputPolicy
    {
        public static bool PanelAllowed(bool ready, bool paused, bool menu, bool cursor, bool editing, bool weaponSafe = true) => ready && paused && menu && cursor && !editing && weaponSafe;
        public static bool GameplayShortcutAllowed(bool ready, bool menu, bool cursor, bool editing) => ready && !menu && !cursor && !editing;
    }
    // Resource-attempt ownership is set before creation so partial failures roll back exactly once.
    public sealed class ModalSession : IDisposable
    {
        private readonly Func<bool> open;
        private readonly Action close;
        private readonly Action? onClosed;
        private bool attempted, disposed;
        public ModalSession(Func<bool> open, Action close, Action? onClosed = null) { this.open = open; this.close = close; this.onClosed = onClosed; }
        public bool IsOpen { get; private set; }
        public Exception? Failure { get; private set; }
        public bool TryOpen()
        {
            if (disposed) return false;
            if (IsOpen) return true;
            attempted = true;
            try { if (open()) { IsOpen = true; return true; } }
            catch (Exception ex) { Failure = ex; }
            Close(); return false;
        }
        public bool Maintain(bool allowed) { if (!allowed) Close(); return IsOpen; }
        public void Close()
        {
            bool wasOpen = IsOpen;
            IsOpen = false;
            if (!attempted) return;
            attempted = false;
            try { close(); } catch (Exception ex) { Failure = ex; }
            if (wasOpen) { try { onClosed?.Invoke(); } catch (Exception ex) { Failure = ex; } }
        }
        public void Dispose() { if (disposed) return; disposed = true; Close(); }
    }
}
