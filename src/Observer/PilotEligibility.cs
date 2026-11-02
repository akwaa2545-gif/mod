namespace NuclearOptionObserver
{
    internal enum PilotRejection { None, Missing, Dead, Ejected, NotLocalPlayer, OtherAircraft, OtherState }
    // Native local-player identity plus existing alive/aircraft/state gates. The unused serialized playerControlled flag is not authority.
    internal static class PilotEligibility
    {
        internal static PilotRejection Evaluate(bool present, bool dead, bool ejected, bool localPlayer, bool sameAircraft, bool sameState) {
            if (!present) return PilotRejection.Missing;
            if (dead) return PilotRejection.Dead;
            if (ejected) return PilotRejection.Ejected;
            if (!localPlayer) return PilotRejection.NotLocalPlayer;
            if (!sameAircraft) return PilotRejection.OtherAircraft;
            if (!sameState) return PilotRejection.OtherState;
            return PilotRejection.None;
        }
    }
}
