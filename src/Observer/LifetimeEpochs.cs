using System.Runtime.CompilerServices;
using System.Threading;

namespace NuclearOptionObserver
{
    // Reference identity, not Unity/native IDs. Weak keys avoid retaining every missile ever seen.
    // OnEnable prefix performs only managed bookkeeping: no Unity/resource operations.
    public sealed class LifetimeEpochs<T> where T : class
    {
        private sealed class Entry { public long Value; }
        private readonly ConditionalWeakTable<T, Entry> entries = new ConditionalWeakTable<T, Entry>();
        public long Read(T target) => Interlocked.Read(ref entries.GetValue(target, _ => new Entry()).Value);
        public long Invalidate(T target)
        {
            var entry = entries.GetValue(target, _ => new Entry());
            return Interlocked.Increment(ref entry.Value); // Return this invalidation, not a later concurrent epoch.
        }
        public void ObserveEnable(T target) => Invalidate(target);
    }
}
