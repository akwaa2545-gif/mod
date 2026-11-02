namespace NuclearOptionObserver
{
    public readonly struct EntityToken
    {
        public EntityToken(ulong sceneGeneration, ulong lifetimeId)
        { SceneGeneration = sceneGeneration; LifetimeId = lifetimeId; }
        public ulong SceneGeneration { get; }
        public ulong LifetimeId { get; }
    }

    // Each fact is required independently. Missing observations remain false.
    public readonly struct OfflineSnapshot
    {
        public const uint SinglePlayer = 1u << 0, ManagerLoaded = 1u << 1,
            NotLoading = 1u << 2, ManagerPresent = 1u << 3,
            ServerPresent = 1u << 4, ClientPresent = 1u << 5,
            ServerActive = 1u << 6, NotListening = 1u << 7,
            ClientConnected = 1u << 8, ClientHost = 1u << 9,
            SameLocalClient = 1u << 10, LocalNetworkPlayerPresent = 1u << 11,
            ExactlyOneParticipant = 1u << 12, SameAuthenticatedPlayer = 1u << 13,
            PlayerReady = 1u << 14, AircraftReady = 1u << 15, CameraReady = 1u << 16;
        public const int FactCount = 17;
        private const uint Required = (1u << FactCount) - 1;
        public OfflineSnapshot(uint facts) { Facts = facts; }
        public uint Facts { get; }
        public static OfflineSnapshot Ready => new OfflineSnapshot(Required);
        public OfflineSnapshot Without(uint fact) => new OfflineSnapshot(Facts & ~fact);
    }

    public static class Phase1Policy
    {
        public static bool CanActivate(bool compatibleBuild, OfflineSnapshot snapshot) =>
            compatibleBuild && snapshot.Facts == OfflineSnapshot.Ready.Facts;
        public static bool IsCurrent(EntityToken selected, EntityToken live) =>
            selected.SceneGeneration != 0 && selected.LifetimeId != 0 &&
            selected.SceneGeneration == live.SceneGeneration && selected.LifetimeId == live.LifetimeId;
        public static bool CanRender(bool visible, bool active, bool targetAlive) => visible && active && targetAlive;
    }

    public readonly struct RegistrationStamp
    {
        public RegistrationStamp(ulong context, long epoch, long phase)
        { Context = context; Epoch = epoch; Phase = phase; }
        public ulong Context { get; }
        public long Epoch { get; }
        public long Phase { get; }
        public bool IsAcceptable(ulong context, long epoch, long phase) =>
            Context != 0 && Context == context && Epoch >= 0 && Epoch == epoch && Phase < phase;
    }
}
