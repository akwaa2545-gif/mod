#nullable disable
using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using NuclearOption.UI;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
using InputPlayer = Rewired.Player;

namespace NuclearOptionObserver
{
    [DefaultExecutionOrder(-10000)]
    public sealed class FullscreenEarlySampler : MonoBehaviour
    {
        internal Action Tick;
        internal Action LostFocus;
        private void Update() => Tick?.Invoke();
        private void OnApplicationFocus(bool focused) { if (!focused) LostFocus?.Invoke(); }
    }
    // Native methods continue except the exact local pilot input-only roots. No camera/physics update is skipped.
    internal sealed class FullscreenInput : IDisposable
    {
        private static FullscreenInput current;
        private readonly GameBridge bridge;
        private readonly FlightHoldDriver hold;
        private AxisOwner lastOwner;
        private readonly FullscreenState mode;
        private readonly Func<bool> modal;
        private readonly Func<int> fullKey, resetKey, viewKey;
        private readonly Action<string> notify;
        private readonly Action replaceAxes;
        private readonly ReleaseBoundary drain = new ReleaseBoundary();
        private ObservationGate hooks;
        private Harmony harmony;
        private ScopedCameraMask mask;
        private Pilot pilot;
        private PilotPlayerState pilotState;
        private EntityToken targetToken;
        private ControlInputs controls;
        private InputPlayer player;
        private CameraBaseState cameraState;
        private HeldManualInput command;
        private int sampledFrame = -1;
        private bool disposed, requireRelease;
        private Exception failure;
        internal bool Draining => drain.Active;
        internal Exception Failure => failure ?? hooks?.Failure;
        internal int HandledViewFrame { get; private set; } = -1;
        internal FullscreenInput(GameBridge bridge, FullscreenState mode, Func<bool> modal, Func<int> fullKey, Func<int> resetKey, Func<int> viewKey, Action<string> notify, FlightHoldDriver hold)
        { this.bridge = bridge; this.hold = hold; this.mode = mode; this.modal = modal; this.fullKey = fullKey; this.resetKey = resetKey; this.viewKey = viewKey; this.notify = notify; replaceAxes = Neutralize; }
        private static MethodInfo Exact(Type type, string name, int token, Type result, bool isPublic, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, args, null);
            if (method == null || method.DeclaringType != type || method.IsStatic || method.ReturnType != result || method.IsPublic != isPublic || method.MetadataToken != token || (name == "UpdateState" && !method.IsVirtual))
                throw new MissingMethodException("Exact fullscreen guard unavailable: " + type.Name + "." + name);
            return method;
        }
        internal bool TryInstall(out string reason)
        {
            if (!Compatibility.CheckFullscreen(out reason)) return false;
            try
            {
                var cameras = new[] { Exact(typeof(CameraCockpitState), "UpdateState", 0x0600009f, typeof(void), true, typeof(CameraStateManager)), Exact(typeof(CameraOrbitState), "UpdateState", 0x060000e3, typeof(void), true, typeof(CameraStateManager)) };
                var pilotButtons = Exact(typeof(PilotPlayerState), "PlayerControls", 0x0600034f, typeof(void), false);
                var pilotAxes = Exact(typeof(PilotPlayerState), "PlayerAxisControls", 0x0600034e, typeof(void), false);
                var axis = Exact(typeof(InputPlayer), "GetAxis", 0x06001015, typeof(float), true, typeof(string));
                var buttons = new[] { Exact(typeof(InputPlayer), "GetButton", 0x06000fa1, typeof(bool), true, typeof(string)), Exact(typeof(InputPlayer), "GetButtonDown", 0x06000fa3, typeof(bool), true, typeof(string)), Exact(typeof(InputPlayer), "GetButtonTimedPressUp", 0x06000fc3, typeof(bool), true, typeof(string), typeof(float), typeof(float)) };
                hooks = new ObservationGate(() => {
                    harmony = new Harmony(Plugin.Id + ".fullscreen");
                    foreach (var method in cameras) harmony.Patch(method, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(CameraPrefix)), postfix: new HarmonyMethod(typeof(FullscreenInput), nameof(CameraPostfix)), finalizer: new HarmonyMethod(typeof(FullscreenInput), nameof(CameraFinalizer)));
                    harmony.Patch(pilotButtons, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(PilotButtonsPrefix)));
                    harmony.Patch(pilotAxes, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(PilotAxesPrefix)));
                    harmony.Patch(axis, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(AxisPrefix)));
                    foreach (var method in buttons) harmony.Patch(method, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(ButtonPrefix)));
                    return true;
                }, () => Volatile.Write(ref current, this), () => { if (ReferenceEquals(Volatile.Read(ref current), this)) Volatile.Write(ref current, null); }, () => { try { harmony?.UnpatchSelf(); } finally { harmony = null; } });
                if (!hooks.TryStart()) { reason = "Fullscreen guard installation failed; PiP remains available."; return false; }
                hold?.BindAxisBoundary(true);
                reason = "Fullscreen local pilot/cockpit-orbit input guards ready."; return true;
            }
            catch (Exception ex) { failure = ex; Dispose(); reason = "Fullscreen guard setup failed; PiP remains available: " + ex.GetType().Name; return false; }
        }
        private bool Context(bool captured, out Pilot local, out string reason, bool requireTarget = true)
        {
            local = null; reason = "Select a live missile in a ready offline mission first.";
            if (disposed || Failure != null || hooks == null || !hooks.CanObserve || !bridge.IsReady || requireTarget && !bridge.TargetReady) return false;
            reason = "Close native UI/map, return focus and use normal cockpit/orbit view first.";
            if (modal() || !Application.isFocused || CursorManager.GetFlags() != CursorFlags.None || GameplayUI.GameIsPaused || DynamicMap.mapMaximized || LeaderboardMenu.IsOpen() || ModalSurface.HasNativeTextFocus() || RadialMenuMain.IsInUse() || !GameManager.flightControlsEnabled) return false;
            var cameraUI = SceneSingleton<CameraControlUI>.i;
            if (cameraUI != null && cameraUI.isOpen) return false;
            var aircraft = bridge.NativeAircraft; var cam = bridge.NativeCamera;
            if (cam == null || !(ReferenceEquals(cam.currentState, cam.cockpitState) || ReferenceEquals(cam.currentState, cam.orbitState)) || !ReferenceEquals(cam.followingUnit, aircraft)) return false;
            reason = "Fullscreen needs the live local player pilot and attached cockpit.";
            if (aircraft.HasEjected() || aircraft.cockpit == null || aircraft.cockpit.IsDetached() || aircraft.pilots == null) return false;
            foreach (var candidate in aircraft.pilots)
            {
                if (candidate == null || candidate.dead || candidate.ejected || !candidate.playerControlled || !ReferenceEquals(candidate.aircraft, aircraft) || !ReferenceEquals(candidate.currentState, candidate.playerState)) continue;
                if (local != null) return false;
                local = candidate;
            }
            if (local == null || GameManager.playerInput == null || aircraft.GetInputs() == null) return false;
            foreach (var station in aircraft.weaponStations) if (station != null && station.SalvoInProgress) { reason = "Wait for the existing weapon salvo to finish before fullscreen."; return false; }
            if (PlayerSettings.virtualJoystickEnabled && SceneSingleton<FlightHud>.i == null) return false;
            if (aircraft.countermeasureTrigger && aircraft.countermeasureManager == null) return false;
            if (captured && (requireTarget && !Phase1Policy.IsCurrent(targetToken, bridge.TargetToken) || !ReferenceEquals(local.playerState, pilotState) || !ReferenceEquals(pilot, local) || !ReferenceEquals(player, GameManager.playerInput) || !ReferenceEquals(controls, aircraft.GetInputs()) || !ReferenceEquals(cameraState, cam.currentState))) return false;
            reason = ""; return true;
        }
        private bool Capturing()
        {
            if (!mode.Active && !drain.Active) return false;
            try
            {
                if (Context(true, out _, out _, mode.Active)) return true;
                // Target-only loss returns the image immediately, but drains only this still-valid local pilot until held inputs release.
                if (mode.Active && Context(true, out _, out _, false)) { Exit(true); return true; }
                Exit(false); return false;
            }
            catch (Exception ex) { Fail(ex); return false; }
        }
        private void Neutralize()
        {
            WriteAxisCommand(controls, pilotState, new FlightCommand(command.Pitch, command.Roll, command.Yaw, command.Throttle), true);
        }
        private void WriteAxisCommand(ControlInputs destination, PilotPlayerState targetState, FlightCommand value, bool neutralAuxiliary)
        {
            destination.pitch = value.Pitch; destination.roll = value.Roll; destination.yaw = value.Yaw; destination.throttle = value.Throttle;
            if (neutralAuxiliary) { destination.brake = command.Brake; destination.customAxis1 = command.CustomAxis; }
            targetState.pitchInput = targetState.rollInput = targetState.yawInput = 0;
        }
        private void NeutralizeJoystick()
        {
            if (bridge.IsReady && pilot != null && ReferenceEquals(pilot.aircraft, bridge.NativeAircraft) && !pilot.dead && !pilot.ejected && pilot.playerControlled && ReferenceEquals(pilot.currentState, pilotState) && ReferenceEquals(pilot.playerState, pilotState) && PlayerSettings.virtualJoystickEnabled && SceneSingleton<FlightHud>.i != null)
                SceneSingleton<FlightHud>.i.SetVirtualJoystick(Vector3.zero);
        }
        private bool HeldExitInputs() => Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.GetKey((KeyCode)fullKey()) || Input.GetKey((KeyCode)resetKey()) || Input.GetKey((KeyCode)viewKey()) || player != null && ExitActions.AnyHeld(player.GetButton);
        private void Exit(bool userReturn)
        {
            mode.Ensure(false);
            if (userReturn) drain.Begin(); else drain.Cancel();
            bridge.SetPresentation(false, 0, 0);
            NeutralizeJoystick();
        }
        private void Fail(Exception ex) { failure = ex; mode.Ensure(false); drain.Cancel(); try { bridge.SetPresentation(false, 0, 0); NeutralizeJoystick(); } catch { } }
        internal void CancelForManualOverride() { try { Exit(false); } catch (Exception ex) { Fail(ex); } }
        internal void ReturnForHoldSafety() { if (mode.Active) { try { Exit(true); } catch (Exception ex) { Fail(ex); } } }
        internal void CancelContext() { requireRelease = true; if (mode.Active || drain.Active) { try { Exit(false); } catch (Exception ex) { Fail(ex); } } }
        internal void Sample()
        {
            if (disposed || Failure != null) return;
            if (!Application.isFocused) { CancelContext(); return; }
            try
            {
                if (mode.Active && !Context(true, out _, out _)) Exit(Context(true, out _, out _, false));
                if (drain.Active && !Context(true, out _, out _, false)) Exit(false);
                if (sampledFrame == Time.frameCount) return;
                sampledFrame = Time.frameCount;
                if (requireRelease) { if (HeldExitInputs()) return; requireRelease = false; }
                if (drain.Active) drain.Poll(Time.frameCount, HeldExitInputs(), Context(true, out _, out _, false));
                bool modifiers = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
                if (!modifiers && Input.GetKeyDown((KeyCode)fullKey()))
                {
                    if (mode.Active) Exit(true);
                    else if (!drain.Active && Context(false, out var local, out var reason) && (hold == null || hold.CanEnterFullscreen(out reason)))
                    {
                        pilot = local; pilotState = local.playerState; targetToken = bridge.TargetToken; controls = bridge.NativeAircraft.GetInputs(); player = GameManager.playerInput; cameraState = bridge.NativeCamera.currentState;
                        if (!HeldManualInput.TryCapture(controls.throttle, controls.customAxis1, out command)) { notify("Fullscreen entry rejected: invalid throttle/custom input."); return; }
                        mask = new ScopedCameraMask(player, player.GetButton("Free Look"));
                        mode.Toggle(true); bridge.SetVisible(true); NeutralizeJoystick();
                        if (bridge.NativeAircraft.countermeasureTrigger) bridge.NativeAircraft.Countermeasures(false, bridge.NativeAircraft.countermeasureManager.activeIndex);
                    }
                    else { string why; if (!Context(false, out _, out why)) notify("Fullscreen unavailable: " + why); else if (hold != null && !hold.CanEnterFullscreen(out why)) notify(why); else notify("Release exit controls first."); }
                }
                if (mode.Active && !modifiers && Input.GetKeyDown((KeyCode)viewKey())) { Exit(true); bridge.SetVisible(false); HandledViewFrame = Time.frameCount; }
                if (mode.Active && !modifiers && Input.GetKeyDown((KeyCode)resetKey())) mode.ResetOrbit();
                // These configured native view mappings are sampled outside the camera-query mask scope, once per frame.
                if (mode.Active && Input.GetMouseButton(1) && !mode.Orbit(player.GetAxis("Pan View") * .3f, -player.GetAxis("Tilt View") * .3f)) throw new InvalidOperationException("Nonfinite missile orbit input.");
                bridge.SetPresentation(mode.Active, mode.Yaw, mode.Pitch);
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void CameraPrefix(CameraBaseState __instance, CameraStateManager cam, out IDisposable __state)
        {
            __state = null; var observer = Volatile.Read(ref current); if (observer == null) return;
            observer.Sample();
            if (observer.Capturing() && ReferenceEquals(cam, observer.bridge.NativeCamera) && ReferenceEquals(cam.currentState, __instance)) __state = observer.mask?.Enter();
        }
        private static void CameraPostfix(IDisposable __state) => __state?.Dispose();
        private static Exception CameraFinalizer(Exception __exception, IDisposable __state) { __state?.Dispose(); return __exception; }
        private static bool PilotButtonsPrefix(PilotPlayerState __instance) => PilotBoundary(__instance, PilotInputRoot.Buttons);
        private static bool PilotAxesPrefix(PilotPlayerState __instance) => PilotBoundary(__instance, PilotInputRoot.Axes);
        private static bool PilotBoundary(PilotPlayerState state, PilotInputRoot root)
        {
            var observer = Volatile.Read(ref current); if (observer == null) return true; observer.Sample();
            bool manual = observer.hold?.ObserveManual(state) == true;
            if (root == PilotInputRoot.Buttons) return PilotInputBoundary.AllowNative(!manual && observer.Capturing(), state, observer.pilotState, root, observer.replaceAxes);
            FlightCommand held = default;
            bool haveHold = !manual && observer.hold != null && observer.hold.TryCommand(state, out held);
            AxisOwner owner = AxisArbiter.Choose(manual, haveHold, observer.Capturing());
            if (owner == AxisOwner.Hold) { observer.WriteAxisCommand(observer.hold.CurrentInputs, state, held, false); observer.lastOwner = owner; return false; }
            if (owner == AxisOwner.FullscreenNeutral)
            {
                if (observer.lastOwner == AxisOwner.Hold) HeldManualInput.TryCapture(observer.controls.throttle, observer.controls.customAxis1, out observer.command);
                observer.lastOwner = owner;
                return PilotInputBoundary.AllowNative(true, state, observer.pilotState, root, observer.replaceAxes);
            }
            observer.lastOwner = AxisOwner.Native; return true;
        }
        private static bool AxisPrefix(InputPlayer __instance, string actionName, ref float __result)
        {
            var observer = Volatile.Read(ref current);
            if (observer != null && observer.mask != null && observer.mask.Axis(__instance, actionName, out var value) && observer.Capturing()) { __result = value; return false; }
            return true;
        }
        private static bool ButtonPrefix(InputPlayer __instance, string actionName, ref bool __result)
        {
            var observer = Volatile.Read(ref current);
            if (observer != null && observer.mask != null && observer.mask.Button(__instance, actionName, out var value) && observer.Capturing()) { __result = value; return false; }
            return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            try { Exit(false); } catch (Exception ex) { failure = ex; }
            hold?.BindAxisBoundary(false);
            disposed = true; hooks?.Dispose();
        }
    }
}
