#nullable disable
using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Rewired;
using UnityEngine;
using NuclearOption.UI;

namespace NuclearOptionObserver
{
    // Owns lifecycle/sensing/commands, but NEVER writes ControlInputs: the existing shared axis boundary is the sole writer.
    internal sealed class FlightHoldDriver : IDisposable
    {
        private static FlightHoldDriver current;
        private readonly GameBridge bridge;
        private readonly Func<bool> fullscreen, releaseDrain;
        private readonly Action exitManual, exitSafety;
        private readonly HoldLifecycle lifecycle = new HoldLifecycle();
        private readonly FlightHoldCore core = new FlightHoldCore();
        private readonly TerrainAdvisor terrain = new TerrainAdvisor();
        private ObservationGate throttleHook;
        private Harmony harmony;
        private AccessTools.FieldRef<PilotPlayerState, float> simulatedThrottle;
        private AccessTools.FieldRef<PilotPlayerState, bool> collective;
        private Pilot pilot;
        private PilotPlayerState state;
        private ControlInputs inputs;
        private Player player;
        private HoldAirframe airframe;
        private ManualOverridePolicy manual;
        private bool disposed, axisReady, ownedOutput;
        private float lastFixed = float.NegativeInfinity, deadband = .15f;
        private double retryCache;
        private FlightCommand lastCommand;
        private string setupReason = "Hold input arbiter is not ready.";
        internal bool Enabled { get; private set; }
        internal bool TerrainEnabled { get; private set; } = true;
        internal bool Active => lifecycle.Active;
        internal bool Available => !disposed && axisReady && throttleHook != null && throttleHook.CanObserve;
        internal string Status { get; private set; } = "Flight hold OFF";
        internal HoldTargets Targets => core.Targets;
        internal bool HasTargets => core.HasTargets;
        internal ControlInputs CurrentInputs => inputs;
        internal FlightHoldDriver(GameBridge bridge, Func<bool> fullscreen, Func<bool> releaseDrain, Action exitManual, Action exitSafety)
        { this.bridge = bridge; this.fullscreen = fullscreen; this.releaseDrain = releaseDrain; this.exitManual = exitManual; this.exitSafety = exitSafety; }
        internal bool TryInstall(out string reason)
        {
            if (!Compatibility.CheckFlightHold(out reason)) { setupReason = reason; return false; }
            try
            {
                var type = typeof(PilotPlayerState);
                var throttleField = type.GetField("simulatedThrottle", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                var collectiveField = type.GetField("collective", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (throttleField == null || throttleField.FieldType != typeof(float) || throttleField.IsStatic || throttleField.MetadataToken != 0x040005e1 || collectiveField == null || collectiveField.FieldType != typeof(bool) || collectiveField.IsStatic || collectiveField.MetadataToken != 0x040005dd)
                    throw new MissingFieldException("Exact matched throttle handover fields unavailable.");
                simulatedThrottle = AccessTools.FieldRefAccess<PilotPlayerState, float>(throttleField);
                collective = AccessTools.FieldRefAccess<PilotPlayerState, bool>(collectiveField);
                var method = type.GetMethod("PlayerThrottleAxis1Controls", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (method == null || !method.IsPrivate || method.IsStatic || method.ReturnType != typeof(void) || method.MetadataToken != 0x06000350) throw new MissingMethodException("Exact throttle producer unavailable.");
                throttleHook = new ObservationGate(() => { harmony = new Harmony(Plugin.Id + ".hold.throttle"); harmony.Patch(method, prefix: new HarmonyMethod(typeof(FlightHoldDriver), nameof(ThrottlePrefix))); return true; },
                    () => Volatile.Write(ref current, this), () => { if (ReferenceEquals(Volatile.Read(ref current), this)) Volatile.Write(ref current, null); }, () => { try { harmony?.UnpatchSelf(); } finally { harmony = null; } });
                if (!throttleHook.TryStart()) { setupReason = "Hold throttle hook failed; existing viewing remains available."; reason = setupReason; return false; }
                reason = "Experimental hold throttle/sensor identities ready."; setupReason = "Shared axis boundary unavailable."; return true;
            }
            catch (Exception ex) { setupReason = "Hold setup blocked: " + ex.GetType().Name + ": " + ex.Message; reason = setupReason; throttleHook?.Dispose(); return false; }
        }
        internal void BindAxisBoundary(bool ready)
        { if (!ready && lifecycle.Active) Disarm("Axis boundary released", false, true); axisReady = ready; }
        internal void Settings(bool enabled, bool avoidance, float manualDeadband)
        {
            deadband = FlightEnvelope.Finite(manualDeadband) ? Mathf.Clamp(manualDeadband, .05f, .5f) : .15f;
            if (TerrainEnabled != avoidance) { TerrainEnabled = avoidance; terrain.Reset(); airframe?.ResetTerrain(); }
            if (Enabled == enabled) return;
            Enabled = enabled;
            if (!enabled) { Disarm("Flight hold OFF", false, true); lifecycle.Evaluate(false, HoldReason.None, Time.frameCount, false, false); }
            else { lifecycle.Rearm(); Status = "Enabled; waiting for map/external/fullscreen reason."; }
        }
        internal void CancelContext() => Disarm("Focus/context lost; hold released.", lifecycle.Active, false);
        internal void Rearm() { lifecycle.Rearm(); Status = "Rearmed; will capture current targets on an eligible trigger."; }
        private bool Owner(bool capture, out string reason)
        {
            reason = "Local offline pilot/aircraft/focus unavailable.";
            if (disposed || !bridge.IsReady || !Application.isFocused) return false;
            var aircraft = bridge.NativeAircraft;
            if (!aircraft.LocalSim || aircraft.HasEjected() || aircraft.cockpit == null || aircraft.cockpit.IsDetached() || aircraft.pilots == null) return false;
            if ((CursorManager.GetFlags() & (CursorFlags.NotInGame | CursorFlags.Loading | CursorFlags.EmptyScene | CursorFlags.SelectionMenu | CursorFlags.CameraControlUI)) != 0) return false;
            if (!GameManager.flightControlsEnabled && !GameplayUI.GameIsPaused) return false;
            Pilot found = null;
            foreach (var p in aircraft.pilots)
                if (p != null && p.playerControlled && !p.dead && !p.ejected && ReferenceEquals(p.aircraft, aircraft) && ReferenceEquals(p.currentState, p.playerState)) { if (found != null) return false; found = p; }
            if (found == null || GameManager.playerInput == null || aircraft.GetInputs() == null) return false;
            if (PlayerSettings.virtualJoystickEnabled && SceneSingleton<FlightHud>.i == null) return false;
            if (pilot != null && (!ReferenceEquals(pilot, found) || !ReferenceEquals(state, found.playerState) || !ReferenceEquals(inputs, aircraft.GetInputs()) || !ReferenceEquals(player, GameManager.playerInput))) return false;
            if (capture && pilot == null) { pilot = found; state = found.playerState; inputs = aircraft.GetInputs(); player = GameManager.playerInput; }
            reason = ""; return true;
        }
        private bool SameHandoverOwner() => bridge.IsReady && pilot != null && !pilot.dead && !pilot.ejected && pilot.playerControlled && ReferenceEquals(pilot.aircraft, bridge.NativeAircraft) && ReferenceEquals(pilot.currentState, state) && ReferenceEquals(pilot.playerState, state) && ReferenceEquals(inputs, bridge.NativeAircraft.GetInputs()) && ReferenceEquals(player, GameManager.playerInput);
        private void ClearVirtualJoystick()
        { if (SameHandoverOwner() && PlayerSettings.virtualJoystickEnabled && SceneSingleton<FlightHud>.i != null) SceneSingleton<FlightHud>.i.SetVirtualJoystick(Vector3.zero); }
        private void Handover()
        {
            if (!ownedOutput) return; ownedOutput = false;
            if (SameHandoverOwner() && simulatedThrottle != null && collective != null && ThrottleHandover.Convert(inputs.throttle, collective(state), PlayerSettings.invertCollective, PlayerSettings.throttleUseNegative, out var value)) simulatedThrottle(state) = value;
            ClearVirtualJoystick();
        }
        private void Disarm(string reason, bool latch, bool seed)
        {
            if (seed) Handover(); else ownedOutput = false;
            core.Release(); terrain.Reset(); lastFixed = float.NegativeInfinity;
            if (latch) lifecycle.Fault();
            Status = reason;
        }
        private HoldReason Reasons()
        {
            HoldReason reasons = DynamicMap.mapMaximized ? HoldReason.Map : HoldReason.None;
            var cam = bridge.NativeCamera;
            if (cam != null && ReferenceEquals(cam.followingUnit, bridge.NativeAircraft) && (ReferenceEquals(cam.currentState, cam.orbitState) || ReferenceEquals(cam.currentState, cam.chaseState) || ReferenceEquals(cam.currentState, cam.TVState))) reasons |= HoldReason.ExternalView;
            if (fullscreen()) reasons |= HoldReason.MissileFullscreen;
            return reasons;
        }
        private RawFlightInput ReadRaw()
        {
            bool ui = GameplayUI.GameIsPaused || ModalSurface.HasNativeTextFocus() || (CursorManager.GetFlags() & (CursorFlags.Chat | CursorFlags.GameMenu)) != 0;
            bool available = !ui && HoldBindings.FlightMapsEnabled(player);
            return new RawFlightInput { Enabled = available, Pitch = player.GetAxisRaw("Pitch"), Roll = player.GetAxisRaw("Roll"), Yaw = player.GetAxisRaw("Yaw"),
                Throttle = player.GetAxisRaw("Throttle"), Custom = player.GetAxisRaw("Custom Axis 1"), RelativeThrottle = PlayerSettings.throttleUseRelative, AxisModifier = player.GetButton("Axis Modifier"),
                VirtualJoystick = ManualOverridePolicy.VirtualStick(PlayerSettings.virtualJoystickEnabled, CameraStateManager.cameraMode == CameraMode.cockpit, ui || DynamicMap.mapMaximized || RadialMenuMain.IsInUse(), Input.GetMouseButton(1), player.GetButton("Free Look"), player.GetAxis("Pan View"), player.GetAxis("Tilt View")),
                Brake = player.GetButton("Brake"), Gear = player.GetButtonDown("Gear"), Engine = player.GetButtonDown("Toggle Engine"), Eject = player.GetButtonDown("Eject"), Assist = player.GetButtonDown("Flight Assist") };
        }
        private bool Read(out FlightTelemetry f, out string reason)
        {
            f = default;
            if (!Owner(true, out reason)) return false;
            if (airframe == null)
            {
                if (Time.time < retryCache) { reason = Status; return false; }
                retryCache = Time.time + 1;
                var candidate = new HoldAirframe(bridge.NativeAircraft, pilot);
                if (!candidate.Prepare(out reason)) { candidate.Dispose(); return false; }
                airframe = candidate;
            }
            return airframe.Read(out f, out reason);
        }
        private bool Terrain(FlightTelemetry f, out TerrainDemand demand, out string reason)
        {
            demand = default;
            if (!airframe.SampleTerrain(f, TerrainEnabled, out reason)) { if (string.IsNullOrEmpty(reason)) reason = "Terrain sensing unavailable."; return false; }
            return terrain.Assess(TerrainEnabled, f, airframe.Terrain, Time.time, out demand, out reason);
        }
        internal void Update()
        {
            if (disposed || !Enabled) return;
            try
            {
                if (!Available) { Status = "Blocked: " + setupReason; return; }
                if (!Owner(true, out var why)) { Disarm(why, lifecycle.Active, false); return; }
                if (GameplayUI.GameIsPaused) { lifecycle.Evaluate(true, Reasons(), Time.frameCount, true, true); Status = lifecycle.Active ? "Paused; captured hold targets frozen." : Status; return; }
                var reasons = Reasons();
                if (reasons == HoldReason.None)
                {
                    bool was = lifecycle.Active; lifecycle.Evaluate(true, reasons, Time.frameCount, false, true);
                    if (was && !lifecycle.Active) Disarm("Manual view restored", false, true);
                    if (!lifecycle.Active && !lifecycle.Latched) Status = "Enabled; waiting for supported map/external/fullscreen view.";
                    return;
                }
                if (lifecycle.Latched) return;
                bool valid = Read(out var f, out why) && FlightEnvelope.Check(f, !lifecycle.Active, out why);
                if (valid && !lifecycle.Active)
                {
                    var raw = ReadRaw(); valid = raw.Enabled;
                    if (!valid) why = "Waiting for enabled native flight input maps.";
                    if (valid && new ManualOverridePolicy(raw, deadband).Detect(raw)) { valid = false; why = "Release active manual flight/recovery inputs before hold capture."; }
                }
                bool activeBefore = lifecycle.Active;
                bool engage = lifecycle.Evaluate(true, reasons, Time.frameCount, false, valid);
                if (!valid) { if (activeBefore) { Disarm("Blocked: " + why, true, true); exitSafety(); } else Status = "Blocked: " + why; return; }
                if (engage)
                {
                    if (!Terrain(f, out _, out why) || !core.Capture(f, out why)) { Disarm("Blocked: " + why, true, true); exitSafety(); return; }
                    manual = new ManualOverridePolicy(ReadRaw(), deadband); ClearVirtualJoystick(); lastFixed = float.NegativeInfinity;
                }
                if (lifecycle.Active) Status = "ACTIVE " + reasons + " | terrain " + (TerrainEnabled ? terrain.Avoiding ? "CLIMB" : "ON" : "OFF");
            }
            catch (Exception ex) { Disarm("Hold fault: " + ex.GetType().Name, true, true); exitSafety(); }
        }
        internal bool CanEnterFullscreen(out string reason)
        {
            reason = ""; if (!Enabled) return true;
            if (!Available) { reason = "Flight Hold unavailable: " + setupReason + "; disable it for neutral fullscreen."; return false; }
            if (!HoldBindings.MouseViewDisjoint(GameManager.playerInput, out reason)) return false;
            return Read(out var f, out reason) && FlightEnvelope.Check(f, !lifecycle.Active, out reason);
        }
        internal bool ObserveManual(PilotPlayerState actual)
        {
            if (!Enabled || !Available || !ManualOverridePolicy.ShouldObserve(lifecycle.Active, fullscreen(), releaseDrain())) return false;
            try
            {
                if (!Owner(true, out _) || !ReferenceEquals(actual, state) || GameplayUI.GameIsPaused) return false;
                var raw = ReadRaw();
                bool intentional = raw.Enabled && (manual ?? new ManualOverridePolicy(raw, deadband)).Detect(raw);
                string mapReason = "";
                bool conflict = !intentional && Input.GetMouseButton(1) && !HoldBindings.MouseViewDisjoint(player, out mapReason);
                var response = ManualOverridePolicy.Response(intentional, conflict);
                if (response == HoldInputResponse.Manual)
                { Disarm("Manual/raw flight override; latched until trigger cycle/rearm. If mappings changed, combined sources take priority.", true, true); exitManual(); return true; }
                if (response == HoldInputResponse.SafetyRelease) { Disarm(mapReason, true, true); exitSafety(); }
                return false;
            }
            catch (Exception ex) { Disarm("Manual input verification fault: " + ex.GetType().Name, true, true); exitSafety(); return false; }
        }
        internal bool TryCommand(PilotPlayerState actual, out FlightCommand command)
        {
            command = default; if (!Enabled || !Available) return false;
            Update();
            if (ObserveManual(actual) || !lifecycle.Active || !ReferenceEquals(actual, state) || GameplayUI.GameIsPaused) return false;
            try
            {
                if (lastFixed == Time.fixedTime) { command = lastCommand; return true; }
                if (!Read(out var f, out var reason) || !Terrain(f, out var terrainDemand, out reason) || !core.Step(f, Time.fixedDeltaTime, terrainDemand, out command, out reason))
                { Disarm("Hold released: " + reason, true, true); exitSafety(); return false; }
                lastFixed = Time.fixedTime; lastCommand = command; ownedOutput = true; return true;
            }
            catch (Exception ex) { Disarm("Hold command fault: " + ex.GetType().Name, true, true); exitSafety(); return false; }
        }
        private bool AllowThrottle(PilotPlayerState actual)
        {
            if (!Enabled || !Available) return true;
            Update(); ObserveManual(actual);
            return !lifecycle.Active || GameplayUI.GameIsPaused || !ReferenceEquals(actual, state) || !Owner(false, out _);
        }
        private static bool ThrottlePrefix(PilotPlayerState __instance)
        {
            var driver = Volatile.Read(ref current); if (driver == null) return true;
            try { return driver.AllowThrottle(__instance); }
            catch (Exception ex) { try { driver.Disarm("Throttle guard fault: " + ex.GetType().Name, true, true); driver.exitSafety(); } catch { } return true; }
        }
        public void Dispose()
        {
            if (disposed) return;
            try { Disarm("Hold stopped", false, true); } catch (Exception ex) { Status = "Hold cleanup fault: " + ex.GetType().Name; } finally { disposed = true; axisReady = false; throttleHook?.Dispose(); airframe?.Dispose(); airframe = null; }
        }
    }
}
