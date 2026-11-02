#nullable disable
using System;
using System.Text;
using UnityEngine;
using NuclearOption.UI;

namespace NuclearOptionObserver
{
    // Native local-player ownership, exact current player state and bounded failure-only diagnostics.
    internal static class LocalPilotContext
    {
        private static PilotRejection Check(Pilot candidate, Aircraft aircraft) => candidate == null ? PilotRejection.Missing :
            PilotEligibility.Evaluate(true, candidate.dead, candidate.ejected, candidate.player != null && GameManager.IsLocalPlayer(candidate.player),
                ReferenceEquals(candidate.aircraft, aircraft), candidate.playerState != null && ReferenceEquals(candidate.currentState, candidate.playerState));
        internal static bool TryGet(Aircraft aircraft, out Pilot local, out string reason)
        {
            local = null;
            reason = "Local aircraft reports ejected."; if (aircraft.HasEjected()) return false;
            reason = "Local cockpit component is missing."; if (aircraft.cockpit == null) return false;
            reason = "Local cockpit is detached."; if (aircraft.cockpit.IsDetached()) return false;
            reason = "Local aircraft pilot collection is missing."; if (aircraft.pilots == null) return false;
            foreach (var candidate in aircraft.pilots)
            {
                if (Check(candidate, aircraft) != PilotRejection.None) continue;
                if (local != null) { reason = "More than one qualifying local player pilot; refusing ambiguous input ownership."; local = null; return false; }
                local = candidate;
            }
            if (local == null) { reason = "No qualifying local player pilot. " + DescribeCandidates(aircraft); return false; }
            reason = "Native local Rewired player input is missing."; if (GameManager.playerInput == null) return false;
            reason = "Local aircraft control-input object is missing."; if (aircraft.GetInputs() == null) return false;
            reason = "Native virtual joystick HUD unavailable (FlightHud instance missing).";
            if (PlayerSettings.virtualJoystickEnabled && SceneSingleton<FlightHud>.i == null) return false;
            reason = ""; return true;
        }
        private static string TypeName(object value)
        {
            string name = value == null ? "null" : value.GetType().Name;
            return name.Length <= 40 ? name : name.Substring(0, 40);
        }
        private static string DescribeCandidates(Aircraft aircraft)
        {
            var result = new StringBuilder(); int count = 0;
            foreach (var candidate in aircraft.pilots)
            {
                if (count++ >= 3) { result.Append(" More candidates omitted."); break; }
                result.Append("p").Append(count - 1).Append('=').Append(Check(candidate, aircraft));
                if (candidate != null)
                    result.Append("[localPlayer=").Append(GameManager.IsLocalPlayer(candidate.player))
                        .Append(",legacyFlag=").Append(candidate.playerControlled)
                        .Append(",sameAircraft=").Append(ReferenceEquals(candidate.aircraft, aircraft))
                        .Append(",sameState=").Append(ReferenceEquals(candidate.currentState, candidate.playerState))
                        .Append(",current=").Append(TypeName(candidate.currentState))
                        .Append(",player=").Append(TypeName(candidate.playerState)).Append(']');
                result.Append(' ');
            }
            return count == 0 ? "Pilot collection is empty." : result.ToString();
        }
    }
}
