#nullable disable
using System;
using System.Collections.Generic;
using Rewired;

namespace NuclearOptionObserver
{
    // Conservative live proof, not a remapper or combined-axis amplitude subtraction.
    internal static class HoldBindings
    {
        private static readonly string[] view = { "Pan View", "Tilt View" }, flight = { "Pitch", "Roll", "Yaw" };
        internal static bool MouseViewDisjoint(Player player, out string reason)
        {
            reason = "Mouse view/flight mapping cannot be verified.";
            if (!ReInput.isReady || player == null) return false;
            foreach (var name in view) if (ReInput.mapping.GetActionId(name) < 0) return false;
            foreach (var name in flight) if (ReInput.mapping.GetActionId(name) < 0) return false;
            var views = new List<(ControllerMap Map, ActionElementMap Element, string Name)>();
            var flights = new List<(ControllerMap Map, ActionElementMap Element, string Name)>();
            int maps = 0, elements = 0;
            foreach (var map in player.controllers.maps.GetAllMaps(ControllerType.Mouse))
            {
                if (++maps > 32) return false;
                if (map == null || !map.enabled || map.controllerType != ControllerType.Mouse) continue;
                foreach (var name in view) foreach (var element in map.GetElementMapsWithAction(name, true)) { if (++elements > 128) return false; if (element != null) views.Add((map, element, name)); }
                foreach (var name in flight) foreach (var element in map.GetElementMapsWithAction(name, true)) { if (++elements > 128) return false; if (element != null) flights.Add((map, element, name)); }
            }
            if (views.Count == 0) { reason = "No enabled Mouse Pan/Tilt mapping: combined hold/orbit unsupported."; return false; }
            foreach (var v in views) foreach (var f in flights)
                if (v.Map.controllerId == f.Map.controllerId && v.Element.CheckForAssignmentConflict(f.Element))
                { reason = "Mouse mapping overlaps " + v.Name + "/" + f.Name + "; separate view and flight bindings or disable Flight Hold."; return false; }
            reason = ""; return true;
        }
        internal static bool FlightMapsEnabled(Player player)
        {
            if (!ReInput.isReady || player == null) return false;
            int count = 0;
            foreach (var map in player.controllers.maps.GetAllMaps())
            {
                if (++count > 128) return false;
                if (map == null || !map.enabled) continue;
                if (map.GetElementMapsWithAction("Pitch", true).Length > 0 || map.GetElementMapsWithAction("Roll", true).Length > 0 || map.GetElementMapsWithAction("Throttle", true).Length > 0) return true;
            }
            return false;
        }
    }
}
