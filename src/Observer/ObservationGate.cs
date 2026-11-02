using System;
using System.Threading;

namespace NuclearOptionObserver
{
    // Pure ordering/rollback seam used by Plugin. Delegates are the only hook/event/resource boundaries.
    // TryStart/EnsureOperational/Dispose run on the owning main thread. Prefix only records faults.
    public sealed class ObservationGate : IDisposable
    {
        private readonly Func<bool> installHook;
        private readonly Action beginObservation, endObservation, removeHook;
        private bool hookAttempted, observationAttempted, started, disposed;
        private int prefixFailed;
        private Exception? failure;
        public ObservationGate(Func<bool> installHook, Action beginObservation, Action endObservation, Action removeHook)
        {
            this.installHook = installHook;
            this.beginObservation = beginObservation;
            this.endObservation = endObservation;
            this.removeHook = removeHook;
        }
        public bool CanObserve => !Volatile.Read(ref disposed) && started && Volatile.Read(ref prefixFailed) == 0;
        public Exception? Failure => failure;
        public bool TryStart()
        {
            if (disposed) return false;
            if (started) return EnsureOperational();
            try
            {
                hookAttempted = true; // A throwing installer can leave partial owned setup.
                if (!installHook()) return Reject(new MissingMethodException("Exact missile lifecycle hook unavailable; observation not begun."));
                if (Volatile.Read(ref prefixFailed) != 0) { Dispose(); return false; }
                observationAttempted = true; // Also unwind a partially completed subscription.
                beginObservation();
                if (Volatile.Read(ref prefixFailed) != 0) { Dispose(); return false; }
                started = true;
                return true;
            }
            catch (Exception ex) { return Reject(ex); }
        }
        private bool Reject(Exception ex)
        {
            Interlocked.CompareExchange(ref failure, ex, null);
            Dispose();
            return false;
        }
        public bool EnsureOperational()
        {
            if (CanObserve) return true;
            if (Volatile.Read(ref prefixFailed) != 0) Dispose();
            return false;
        }
        // No cleanup/resource work in a possibly external lifecycle callback; the next main-thread gate check unwinds.
        public void ObservePrefix(Action updateEpoch)
        {
            if (Volatile.Read(ref disposed)) return;
            try { updateEpoch(); }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref failure, ex, null);
                Interlocked.Exchange(ref prefixFailed, 1);
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            Volatile.Write(ref disposed, true); // Reject callbacks/queue entries before cleanup starts.
            started = false;
            if (observationAttempted)
            {
                try { endObservation(); }
                catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
            }
            if (hookAttempted)
            {
                try { removeHook(); }
                catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
            }
        }
    }
}
