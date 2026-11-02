using System;

namespace NuclearOptionObserver
{
    public static class OwnLaunchPolicy
    {
        public static bool Accept(bool current, bool eligible, bool sameOwner, ulong ownerId, ulong aircraftId, ulong capturedId, ulong liveId) =>
            current && eligible && sameOwner && ownerId != 0 && ownerId == aircraftId && capturedId != 0 && capturedId == liveId;
    }
    // Own-aircraft event inbox only: discovery scans never call this. Callbacks perform managed bookkeeping only.
    public sealed class LaunchObservation<T> : IDisposable where T : class
    {
        private readonly object sync = new object();
        private readonly RegistrationQueue<T> registrations;
        private bool enabled, disposed;
        private Exception? failure;
        public LaunchObservation(LifetimeEpochs<T> epochs, Func<bool> ready, ulong context)
        { registrations = new RegistrationQueue<T>(epochs, () => enabled && !disposed && ready(), context); }
        public bool Enabled { get { lock (sync) return enabled && !disposed; } }
        public Exception? Failure { get { lock (sync) return failure; } }
        public void SetEnabled(bool value)
        {
            lock (sync)
            {
                if (disposed || failure != null || enabled == value) return;
                enabled = value;
                registrations.Clear(); // Toggle changes never consume earlier events or existing discoveries.
            }
        }
        public void Observe(T target, bool registration, ulong entityId = 0)
        {
            lock (sync)
            {
                if (!enabled || disposed || failure != null) return;
                try { registrations.Enqueue(target, registration, entityId); }
                catch (Exception ex) { failure = ex; enabled = false; registrations.Clear(); } // Never throw through a native event.
            }
        }
        public Registration<T>[] TakeReady()
        { lock (sync) return enabled && !disposed ? registrations.TakeReady() : Array.Empty<Registration<T>>(); }
        public bool IsCurrent(Registration<T> candidate)
        { lock (sync) return enabled && !disposed && registrations.IsCurrent(candidate); }
        public void Dispose()
        { lock (sync) { if (disposed) return; disposed = true; enabled = false; registrations.Clear(); } }
    }
}
