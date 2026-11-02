using System;
using System.Collections.Generic;
using System.Threading;

namespace NuclearOptionObserver
{
    public sealed class Registration<T> where T : class
    {
        public Registration(T target, RegistrationStamp stamp, EntityToken token, ulong entityId = 0)
        { Target = target; Stamp = stamp; Token = token; EntityId = entityId; }
        public ulong EntityId { get; }
        public T Target { get; }
        public RegistrationStamp Stamp { get; }
        public EntityToken Token { get; }
    }
    // One process-wide sequence, independent of target type and native/pool identity.
    internal static class RegistrationLifetimes
    {
        private static long next;
        internal static ulong Next()
        {
            long value = Interlocked.Increment(ref next);
            if (value <= 0) throw new InvalidOperationException("Observer lifetime counter exhausted; restart required.");
            return (ulong)value;
        }
    }
    // Production callback→deferred-acceptance path, with no Unity or uploaded assembly dependency.
    public sealed class RegistrationQueue<T> where T : class
    {
        private readonly LifetimeEpochs<T> epochs;
        private readonly Func<bool> canObserve;
        private readonly ulong context;
        private readonly object inboxLock = new object();
        private readonly Queue<Registration<T>> inbox = new Queue<Registration<T>>();
        private long phase;
        public RegistrationQueue(LifetimeEpochs<T> epochs, Func<bool> canObserve, ulong context)
        { this.epochs = epochs; this.canObserve = canObserve; this.context = context; }
        public void Enqueue(T target, bool registration, ulong entityId = 0)
        {
            if (ReferenceEquals(target, null)) return;
            lock (inboxLock)
            {
                if (!canObserve()) return;
                long capturedEpoch = epochs.Invalidate(target); // Re-registration ends queued and selected old lifetimes now.
                if (registration)
                    inbox.Enqueue(new Registration<T>(target, new RegistrationStamp(context, capturedEpoch, phase), new EntityToken(context, RegistrationLifetimes.Next()), entityId));
            }
        }
        public Registration<T>[] TakeReady()
        {
            lock (inboxLock)
            {
                if (!canObserve()) { inbox.Clear(); return Array.Empty<Registration<T>>(); }
                long currentPhase = Interlocked.Increment(ref phase);
                var ready = new List<Registration<T>>();
                int count = inbox.Count;
                while (count-- > 0)
                {
                    var candidate = inbox.Dequeue();
                    if (candidate.Stamp.Phase >= currentPhase) { inbox.Enqueue(candidate); continue; }
                    if (candidate.Stamp.IsAcceptable(context, epochs.Read(candidate.Target), currentPhase)) ready.Add(candidate);
                }
                return ready.ToArray();
            }
        }
        public bool IsCurrent(Registration<T> candidate) => canObserve() &&
            candidate.Stamp.IsAcceptable(context, epochs.Read(candidate.Target), Interlocked.Read(ref phase));
        public void Clear() { lock (inboxLock) inbox.Clear(); }
    }
}
