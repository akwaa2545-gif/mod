using System;

namespace NuclearOptionObserver
{
    public interface IPictureBridge
    {
        bool IsReady { get; }
        bool IsCurrent(EntityToken token);
        void Render(EntityToken token);
        void Release();
    }

    // Small pure orchestration seam. The Unity implementation owns only a camera and texture.
    public sealed class MissilePicture : IDisposable
    {
        public const int Width = 480, Height = 270;
        public const double Interval = 0.05;
        private readonly IPictureBridge bridge;
        private EntityToken selected;
        private bool visible, disposed, mayOwnResources;
        private double nextFrame = double.NegativeInfinity;
        public MissilePicture(IPictureBridge bridge) { this.bridge = bridge; }
        public bool TrackingEnded { get; private set; }
        public bool HasSelection { get; private set; }
        public bool Faulted { get; private set; }
        public Exception? LastFailure { get; private set; }

        // Invoked only by an explicit validated manual selection command.
        public bool Track(EntityToken token)
        {
            if (disposed || Faulted || !bridge.IsReady || !bridge.IsCurrent(token)) return false;
            selected = token;
            HasSelection = true;
            TrackingEnded = false;
            return true;
        }
        public void SetVisible(bool value)
        {
            if (disposed) return;
            visible = value;
            if (!visible) ReleaseOwned();
        }
        public void Tick(double monotonicSeconds)
        {
            if (disposed || Faulted) return;
            try
            {
                if (!bridge.IsReady) { End(); return; }
                if (HasSelection && !bridge.IsCurrent(selected)) End();
                if (!Phase1Policy.CanRender(visible, HasSelection, HasSelection)) return;
                if (double.IsNaN(monotonicSeconds) || double.IsInfinity(monotonicSeconds) || monotonicSeconds < nextFrame) return;
                nextFrame = monotonicSeconds + Interval; // Never catch up, and never loop.
                mayOwnResources = true; // Also clean up a partially failed allocation.
                bridge.Render(selected);
            }
            catch (Exception ex)
            {
                Faulted = true;
                LastFailure = ex;
                End();
            }
        }
        private void End()
        {
            TrackingEnded |= HasSelection;
            HasSelection = false;
            ReleaseOwned();
        }
        private void ReleaseOwned()
        {
            if (!mayOwnResources) return;
            mayOwnResources = false;
            try { bridge.Release(); }
            catch (Exception ex) { Faulted = true; LastFailure = ex; }
        }
        public void EndTracking() { if (!disposed) End(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            End();
        }
    }
}
