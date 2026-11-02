#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using NuclearOption.UI;
using UnityEngine.SceneManagement;

namespace NuclearOptionObserver
{
    [BepInPlugin(Id, "Nuclear Option Observer (untested)", "0.5.1")]
    [DefaultExecutionOrder(100)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "io.github.nuclearoption.observer";
        private static LifetimeEpochs<Missile> observedEpochs;
        private static ObservationGate prefixGate;
        private ObservationGate gate;
        private readonly ReasonThrottle readinessLog = new ReasonThrottle();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private ConfigEntry<bool> enabledConfig, autoShowConfig;
        private MapWatch mapWatch;
        private FlightHoldDriver flightHold;
        private ConfigEntry<bool> holdEnabled, terrainAvoidance;
        private ConfigEntry<float> overrideDeadband;
        private bool holdSection;
        private bool mapUnavailable;
        private ConfigEntry<int> schemaConfig, panelKey, viewKey, cycleKey, fullscreenKey, resetKey;
        private readonly FullscreenState fullscreen = new FullscreenState();
        private FullscreenInput fullscreenInput;
        private FullscreenEarlySampler earlySampler;
        private bool fullscreenUnavailable;
        private ConfigEntry<float> xConfig, yConfig, widthConfig, scaleConfig;
        private ModalSurface modal;
        private string notice = "";
        private double noticeUntil;
        private Vector2 scroll;
        private int captureKey;
        private Rect panelRect;
        private DateTime lastConfigWrite;
        private double nextConfigCheck;
        private long sceneVersion;
        private bool compatible, failed;
        private GameBridge bridge;
        private Harmony harmony;

        private void Awake()
        {
            modal = new ModalSurface(OnPanelClosed); // All successful-open close paths save once, including OnGUI guard closure.
            enabledConfig = Config.Bind("PiP", "Enabled", false, "Opt-in to offline-only manual missile PiP. Existing true is preserved; view always starts hidden. Pause with Esc, then F8 (configurable).");
            autoShowConfig = Config.Bind("MissileCamera", "AutoShowOwnLaunch", false, "When true, select and show future NEW own-aircraft missile registrations. OFF preserves manual behavior; enabling never selects existing missiles.");
            holdEnabled = Config.Bind("FlightHold", "Enabled", false, "EXPERIMENTAL / UNTESTED. Capture current course, HUD speed and sea altitude automatically in supported map/external/fullscreen views. Manual flight input cancels. Not a crash-avoidance guarantee.");
            terrainAvoidance = Config.Bind("FlightHold", "TerrainAvoidance", true, "Only effective while Flight Hold is active. OFF preserves ordinary captured course/speed/altitude hold but disables terrain sampling/avoidance climb.");
            overrideDeadband = Config.Bind("FlightHold", "ManualOverrideDeadband", .15f, "Raw pitch/roll/yaw manual-override deadband, clamped 0.05..0.5. No target speed/altitude entry is needed.");
            schemaConfig = Config.Bind("Manual", "Schema", 0, "2 = saved manual layout/key schema. AutoShowOwnLaunch is a separate opt-in, default false.");
            xConfig = Config.Bind("Window", "X", 16f, "PiP left edge in screen pixels, clamped after resolution changes.");
            yConfig = Config.Bind("Window", "Y", 16f, "PiP top edge in screen pixels.");
            widthConfig = Config.Bind("Window", "Width", 480f, "Base PiP width, 16:9 aspect ratio, before UI scale.");
            scaleConfig = Config.Bind("Window", "Scale", 1f, "UI/PiP scale 0.75 to 1.5.");
            panelKey = Config.Bind("Keys", "Panel", (int)KeyCode.F8, "Distinct F1..F12 Unity KeyCode number; paused-only. Verify no native binding conflict.");
            viewKey = Config.Bind("Keys", "View", (int)KeyCode.F9, "Toggle PiP open/close during safe gameplay or the paused Observer menu. Own-launch auto-show is separate opt-in.");
            cycleKey = Config.Bind("Keys", "Next", (int)KeyCode.F10, "Distinct F1..F12; manually select next missile during safe gameplay or the paused Observer menu. Does not open a hidden view.");
            fullscreenKey = Config.Bind("Keys", "Fullscreen", (int)KeyCode.F11, "Distinct function key: selected missile fullscreen/PiP. Requires normal cockpit/orbit and safe local input guards.");
            resetKey = Config.Bind("Keys", "ResetOrbit", (int)KeyCode.F7, "Distinct function key: reset missile orbit. Default F7 avoids Steam F12 screenshots.");
            earlySampler = gameObject.AddComponent<FullscreenEarlySampler>();
            earlySampler.Tick = () => { if (enabled) fullscreenInput?.Sample(); };
            earlySampler.LostFocus = () => { fullscreenInput?.CancelContext(); flightHold?.CancelContext(); };
            Config.SaveOnConfigSet = false;
            NormalizeConfig();
            Config.Save();
            lastConfigWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
            compatible = Compatibility.Check(out var reason);
            if (!compatible) Logger.LogError("Observer disabled: " + reason);
            else Logger.LogInfo("Observer 0.5.1 EXPERIMENTAL candidate: " + reason + " Configured Enabled=" + enabledConfig.Value + ". Freecam/unit-control/retargeting absent.");
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) => Interlocked.Increment(ref sceneVersion);
        private void SceneUnloaded(Scene scene) => Interlocked.Increment(ref sceneVersion);
        private void ActiveSceneChanged(Scene previous, Scene next) => Interlocked.Increment(ref sceneVersion);

        private void LateUpdate()
        {
            try
            {
                PollConfig();
                if (!compatible || failed || !enabledConfig.Value)
                { StopObservation(); return; }
                if (gate != null && !gate.EnsureOperational())
                {
                    failed = true;
                    Logger.LogError("Observer lifecycle gate failed; disabled until restart: " + gate.Failure);
                    StopObservation();
                    return;
                }
                if (!GameBridge.TryGetAnchor(out var anchor, out var reason))
                {
                    if (readinessLog.ShouldLog(reason, clock.Elapsed.TotalSeconds)) Logger.LogInfo("PiP inactive: " + reason);
                    StopObservation();
                    return;
                }
                if (bridge != null && !bridge.Matches(anchor)) StopObservation();
                if (bridge == null)
                {
                    var epochs = new LifetimeEpochs<Missile>();
                    ObservationGate startingGate = null;
                    startingGate = new ObservationGate(
                        () => InstallLifecycleHook(epochs),
                        () => {
                            bridge = new GameBridge(anchor, epochs, () => Interlocked.Read(ref sceneVersion), () => startingGate.CanObserve);
                            bridge.SetAutoShowOwnLaunch(autoShowConfig.Value);
                            bridge.BeginObservation();
                        },
                        () => { var old = bridge; bridge = null; old?.Dispose(); },
                        RemoveLifecycleHook);
                    gate = startingGate;
                    Volatile.Write(ref prefixGate, gate);
                    if (!gate.TryStart())
                    {
                        failed = true;
                        Logger.LogError("Observer lifecycle setup failed closed; disabled until restart: " + gate.Failure);
                        StopObservation();
                        return;
                    }
                    if (!mapUnavailable)
                    {
                        mapWatch = new MapWatch(bridge, () => modal.IsOpen || fullscreen.Active, CurrentViewLayout, HoldHudBlocksPointer);
                        if (!mapWatch.TryInstall(out var mapReason)) { mapUnavailable = true; Notify(mapReason); mapWatch.Dispose(); mapWatch = null; }
                    }
                    flightHold = new FlightHoldDriver(bridge, () => fullscreen.Active, () => fullscreenInput?.Draining == true, () => fullscreenInput?.CancelForManualOverride(), () => fullscreenInput?.ReturnForHoldSafety());
                    if (!flightHold.TryInstall(out var holdReason)) Logger.LogWarning(holdReason);
                    ApplyHoldSettings();
                    if (!fullscreenUnavailable)
                    {
                        fullscreenInput = new FullscreenInput(bridge, fullscreen, () => modal.IsOpen, () => fullscreenKey.Value, () => resetKey.Value, () => viewKey.Value, Notify, flightHold);
                        if (!fullscreenInput.TryInstall(out var fullReason)) { fullscreenUnavailable = true; Notify(fullReason); fullscreenInput.Dispose(); fullscreenInput = null; }
                    }
                    Logger.LogInfo("PiP offline guard passed; manual global missile selector ready, view hidden. Esc then panel key (default F8).");
                }
                bridge.SetAutoShowOwnLaunch(autoShowConfig.Value);
                ApplyHoldSettings();
                fullscreenInput?.Sample();
                flightHold?.Update();
                HandleInput();
                mapWatch?.Tick();
                if (mapWatch?.Failure != null && !mapUnavailable) { mapUnavailable = true; Notify("Map watch unavailable until restart; list controls remain available: " + mapWatch.Failure.GetType().Name); }
                bridge.Tick(clock.Elapsed.TotalSeconds);
                fullscreenInput?.Sample(); // Revalidate target/context after automatic/manual selection changes, without sampling delta twice.
                if (fullscreenInput?.Failure != null && !fullscreenUnavailable) { fullscreenUnavailable = true; Notify("Fullscreen input guard failed; PiP remains available until restart: " + fullscreenInput.Failure.GetType().Name); }
                if (bridge.Faulted)
                {
                    failed = true;
                    Logger.LogError("Observer render/allocation failed; disabled until restart: " + bridge.Failure);
                    StopObservation();
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Logger.LogError("Observer failed closed; disabled until restart: " + ex);
                StopObservation();
            }
        }
        private void PollConfig()
        {
            if (clock.Elapsed.TotalSeconds < nextConfigCheck) return;
            nextConfigCheck = clock.Elapsed.TotalSeconds + 1;
            var changed = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
            if (changed == lastConfigWrite) return;
            Config.Reload(); // Main thread only; no watcher callbacks touching Unity.
            NormalizeConfig();
            lastConfigWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
        }
        private bool InstallLifecycleHook(LifetimeEpochs<Missile> epochs)
        {
            var method = typeof(Missile).GetMethod(nameof(Missile.OnEnable),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void) || !method.IsVirtual || method.IsStatic ||
                method.DeclaringType != typeof(Missile) || method.MetadataToken != 0x06001a34)
                return false; // Exact hook absent: pure gate prevents all observation subscriptions.
            harmony = new Harmony(Id);
            Volatile.Write(ref observedEpochs, epochs);
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Plugin), nameof(ObserveMissileEnable)));
            return true;
        }
        // Observation-only void prefix. Native OnEnable always continues; no Unity API or game mutation here.
        private static void ObserveMissileEnable(Missile __instance)
        {
            Volatile.Read(ref prefixGate)?.ObservePrefix(() => Volatile.Read(ref observedEpochs)?.ObserveEnable(__instance));
        }
        private void NormalizeConfig()
        {
            var p = ObserverPreferences.Normalize(enabledConfig.Value, schemaConfig.Value, xConfig.Value, yConfig.Value,
                widthConfig.Value, scaleConfig.Value, panelKey.Value, viewKey.Value, cycleKey.Value);
            schemaConfig.Value = p.Schema; xConfig.Value = p.X; yConfig.Value = p.Y; widthConfig.Value = p.Width; scaleConfig.Value = p.Scale;
            panelKey.Value = p.PanelKey; viewKey.Value = p.ViewKey; cycleKey.Value = p.CycleKey;
            var extra = FullscreenKeys.Normalize(p.PanelKey, p.ViewKey, p.CycleKey, fullscreenKey.Value, resetKey.Value);
            fullscreenKey.Value = extra.Fullscreen; resetKey.Value = extra.Reset;
            overrideDeadband.Value = ViewLayout.Finite(overrideDeadband.Value, .15f);
            overrideDeadband.Value = Math.Max(.05f, Math.Min(.5f, overrideDeadband.Value));
        }
        private void ApplyHoldSettings() => flightHold?.Settings(holdEnabled.Value, terrainAvoidance.Value, overrideDeadband.Value);
        private bool HoldHudVisible => holdEnabled != null && holdEnabled.Value && bridge != null && bridge.IsReady;
        private HoldHudLayout HoldHudBounds() => HoldHudLayout.Clamp(Screen.width, Screen.height);
        private bool HoldHudBlocksPointer(Vector2 pointer) => HoldHudBounds().BlocksPointer(HoldHudVisible, pointer.x, pointer.y, Screen.height);
        private void OnPanelClosed() { captureKey = 0; SaveSettings(); }
        private void SaveSettings() { Config.Save(); lastConfigWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath); }
        private void Notify(string text) { notice = text; noticeUntil = clock.Elapsed.TotalSeconds + 8; Logger.LogInfo(text); }
        private bool Editing => captureKey != 0 || ModalSurface.HasNativeTextFocus();
        private void HandleInput()
        {
            bool wasOpen = modal.IsOpen;
            if (wasOpen && !modal.Maintain(out var lostReason)) { captureKey = 0; if (!string.IsNullOrEmpty(lostReason)) Notify(lostReason); }
            bool modifiers = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (Input.GetKeyDown((KeyCode)panelKey.Value) && !Editing && !modifiers)
            {
                if (modal.IsOpen) ClosePanel();
                else if (!modal.TryOpen(out var reason)) { Notify(reason); if (modal.Failure != null) throw new InvalidOperationException("Observer modal setup/cleanup failed.", modal.Failure); }
            }
            if (modal.Failure != null) throw new InvalidOperationException("Observer modal cleanup failed.", modal.Failure);
            if (fullscreenUnavailable && !Editing && !modifiers && Input.GetKeyDown((KeyCode)fullscreenKey.Value)) Notify("Fullscreen guards unavailable until restart; ordinary PiP remains usable.");
            if (captureKey != 0 || modifiers) return;
            bool safeFlight = InputPolicy.GameplayShortcutAllowed(bridge.IsReady && Application.isFocused, LeaderboardMenu.IsOpen(), Cursor.visible,
                ModalSurface.HasNativeTextFocus() || UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy);
            bool missileShortcuts = InputPolicy.MissileShortcutAllowed(Editing, modifiers, modal.IsOpen, safeFlight);
            if (missileShortcuts && Input.GetKeyDown((KeyCode)viewKey.Value) && fullscreenInput?.HandledViewFrame != Time.frameCount) bridge.ToggleVisible();
            if (missileShortcuts && Input.GetKeyDown((KeyCode)cycleKey.Value)) { if (fullscreenInput != null) fullscreenInput.NextMissile(); else bridge.Cycle(1); }
        }
        private void ClosePanel() { captureKey = 0; modal.Close(); }
        private ViewLayout CurrentViewLayout() => ViewLayout.Clamp(xConfig.Value, yConfig.Value, widthConfig.Value, scaleConfig.Value, Screen.width, Screen.height);
        private void OnGUI()
        {
            if (!compatible || failed || enabledConfig == null || !enabledConfig.Value || bridge == null) return;
            try
            {
                fullscreenInput?.Sample();
                if (!bridge.IsReady) { modal.Close(); return; }
                var layout = CurrentViewLayout();
                if (fullscreen.Active && bridge.Visible && Event.current.type == EventType.Repaint)
                {
                    var area = new Rect(0, 0, Screen.width, Screen.height);
                    GUI.DrawTexture(area, Texture2D.blackTexture, ScaleMode.StretchToFill, false);
                    if (bridge.Texture != null) GUI.DrawTexture(area, bridge.Texture, ScaleMode.ScaleToFit, false);
                    GUI.Label(new Rect(16, 16, Math.Max(1, Screen.width - 32), 64), bridge.Status + "\n" + (KeyCode)fullscreenKey.Value + " return | Hold RMB: orbit | " + (KeyCode)resetKey.Value + " reset | Esc then " + (KeyCode)panelKey.Value + " settings. Aircraft sim continues. " + (flightHold?.Active == true ? "Experimental Flight Hold ACTIVE." : "Flight Hold inactive; neutral inputs are NOT autopilot."));
                }
                if (!fullscreen.Active && bridge.Visible && Event.current.type == EventType.Repaint)
                {
                    var target = bridge.Texture;
                    GUI.Box(new Rect(layout.X, layout.Y, layout.Width, layout.Height + ViewLayout.StatusHeight), "");
                    if (target != null) GUI.DrawTexture(new Rect(layout.X, layout.Y, layout.Width, layout.Height), target, ScaleMode.ScaleToFit, false);
                    GUI.Label(new Rect(layout.X + 4, layout.Y + layout.Height, layout.Width - 8, ViewLayout.StatusHeight), bridge.Status);
                }
                if (HoldHudVisible && Event.current.type == EventType.Repaint)
                {
                    var h = HoldHudBounds();
                    GUI.Box(new Rect(h.X, h.Y, h.Width, h.Height), "");
                    GUI.Label(new Rect(h.X + 4, h.Y + 2, Math.Max(1, h.Width - 8), h.Height - 4), "EXPERIMENTAL Flight Hold | terrain " + (terrainAvoidance.Value ? "ON" : "OFF") + "\n" + (flightHold?.Status ?? "Waiting for input setup.") + "\nManual flight input cancels. Not guaranteed to avoid ground.");
                }
                if (fullscreenInput?.Draining == true) GUI.Label(new Rect(16, Screen.height - 64, Math.Max(1, Screen.width - 32), 48), "Release held mouse/flight/view buttons to restore controls.");
                if (clock.Elapsed.TotalSeconds < noticeUntil) GUI.Label(new Rect(20, Screen.height - 88, Math.Max(1, Screen.width - 40), 64), notice);
                if (!modal.IsOpen || !modal.Maintain(out _)) return;
                var matrix = GUI.matrix;
                try
                {
                    float scale = layout.Scale;
                    GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                    float sw = Screen.width / scale, sh = Screen.height / scale;
                    panelRect = new Rect(Math.Max(0, (sw - Math.Min(680, sw)) / 2), Math.Max(0, (sh - Math.Min(650, sh)) / 2), Math.Min(680, sw), Math.Min(650, sh));
                    GUI.Window(0x4f4253, panelRect, DrawPanel, "Nuclear Option Observer — Mod Menu 0.5.1 EXPERIMENTAL");
                }
                finally { GUI.matrix = matrix; }
            }
            catch (Exception ex)
            {
                failed = true;
                Logger.LogError("Observer overlay failed closed: " + ex);
                StopObservation();
            }
        }
        private void DrawPanel(int id)
        {
            float w = panelRect.width - 24;
            if (GUI.Button(new Rect(12, 25, w / 3 - 4, 28), "Missile Camera")) { holdSection = false; captureKey = 0; }
            if (GUI.Button(new Rect(16 + w / 3, 25, w / 3 - 4, 28), "Flight Hold")) { holdSection = true; captureKey = 0; }
            if (GUI.Button(new Rect(20 + 2 * w / 3, 25, w / 3 - 8, 28), "Close menu")) { ClosePanel(); return; }
            if (holdSection) { DrawFlightHold(w); return; }
            if (GUI.Button(new Rect(12, 61, w / 3 - 4, 28), bridge.Visible ? "Hide view" : "Show view")) bridge.ToggleVisible();
            if (GUI.Button(new Rect(16 + w / 3, 61, w / 3 - 4, 28), "Clear tracking")) bridge.ClearSelection();
            if (GUI.Button(new Rect(20 + w * 2 / 3, 61, w / 3 - 8, 28), "Close panel")) { ClosePanel(); return; }
            if (GUI.Button(new Rect(12, 95, w / 2 - 4, 24), "Previous missile")) bridge.Cycle(-1);
            if (GUI.Button(new Rect(16 + w / 2, 95, w / 2 - 4, 24), "Next missile")) bridge.Cycle(1);
            bool autoShow = GUI.Toggle(new Rect(12, 124, w, 26), autoShowConfig.Value, "Auto-show NEW own-aircraft launches (OFF keeps manual behavior)");
            if (autoShow != autoShowConfig.Value) { autoShowConfig.Value = autoShow; bridge.SetAutoShowOwnLaunch(autoShow); SaveSettings(); }
            GUI.Label(new Rect(12, 154, w, 24), bridge.Status);
            float listHeight = Math.Max(40, panelRect.height - 445);
            var rows = bridge.Catalog.Rows;
            scroll = GUI.BeginScrollView(new Rect(12, 182, w, listHeight), scroll, new Rect(0, 0, Math.Max(1, w - 20), Math.Max(listHeight, rows.Count * 30)));
            for (int i = 0; i < rows.Count; i++) if (GUI.Button(new Rect(0, i * 30, Math.Max(1, w - 24), 28), rows[i].Label)) { GUI.FocusControl(null); bridge.Select(rows[i].Token); }
            GUI.EndScrollView();
            float y = 190 + listHeight;
            GUI.Label(new Rect(12, y, w, 24), rows.Count + " live missile(s). List/next/previous do not change visibility.");
            y += 27;
            float x = GUI.HorizontalSlider(new Rect(100, y + 6, w - 112, 20), xConfig.Value, 0, Math.Max(0, Screen.width - 100));
            GUI.Label(new Rect(12, y, 85, 24), "Left " + x.ToString("0")); if (x != xConfig.Value) xConfig.Value = x;
            y += 27;
            float top = GUI.HorizontalSlider(new Rect(100, y + 6, w - 112, 20), yConfig.Value, 0, Math.Max(0, Screen.height - 100));
            GUI.Label(new Rect(12, y, 85, 24), "Top " + top.ToString("0")); if (top != yConfig.Value) yConfig.Value = top;
            y += 27;
            float width = GUI.HorizontalSlider(new Rect(100, y + 6, w - 112, 20), widthConfig.Value, 240, 1280);
            GUI.Label(new Rect(12, y, 85, 24), "Width " + width.ToString("0")); if (width != widthConfig.Value) widthConfig.Value = width;
            y += 27;
            float scale = GUI.HorizontalSlider(new Rect(100, y + 6, w - 112, 20), scaleConfig.Value, .75f, 1.5f);
            GUI.Label(new Rect(12, y, 85, 24), "Scale " + scale.ToString("0.00")); if (scale != scaleConfig.Value) scaleConfig.Value = scale;
            y += 27;
            var keys = new[] { panelKey, viewKey, cycleKey, fullscreenKey, resetKey }; var names = new[] { "Settings", "Toggle PiP", "Next missile", "Fullscreen", "Reset orbit" };
            for (int i = 0; i < 5; i++) if (GUI.Button(new Rect(12 + (i % 3) * w / 3, y + (i / 3) * 28, w / 3 - 4, 25), names[i] + ": " + (captureKey == i + 1 ? "press F-key" : ((KeyCode)keys[i].Value).ToString()))) { GUI.FocusControl(null); captureKey = i + 1; }
            if (captureKey != 0 && Event.current.type == EventType.KeyDown)
            {
                int key = (int)Event.current.keyCode;
                int p = captureKey == 1 ? key : panelKey.Value, v = captureKey == 2 ? key : viewKey.Value, n = captureKey == 3 ? key : cycleKey.Value, f = captureKey == 4 ? key : fullscreenKey.Value, r = captureKey == 5 ? key : resetKey.Value;
                if (ShortcutPolicy.ValidFive(p, v, n, f, r) && ShortcutPolicy.NoChordModifiers((int)Event.current.modifiers)) { panelKey.Value = p; viewKey.Value = v; cycleKey.Value = n; fullscreenKey.Value = f; resetKey.Value = r; captureKey = 0; SaveSettings(); }
                else if (Event.current.keyCode == KeyCode.Escape) captureKey = 0;
                Event.current.Use();
            }
            GUI.Label(new Rect(12, y + 56, w, 42), "Distinct F1–F12 only; no modifiers. Check native/other-mod conflicts. Close panel, then Esc to resume.");
        }
        private void DrawFlightHold(float w)
        {
            GUI.Label(new Rect(12, 65, w, 40), "EXPERIMENTAL / UNTESTED — normal forward flight only. No crash-avoidance guarantee.");
            bool enabledHold = GUI.Toggle(new Rect(12, 110, w, 28), holdEnabled.Value, "Enable automatic current-course / speed / altitude hold");
            bool avoid = GUI.Toggle(new Rect(12, 146, w, 28), terrainAvoidance.Value, "Terrain avoidance (only while hold is ACTIVE)");
            if (enabledHold != holdEnabled.Value || avoid != terrainAvoidance.Value)
            { holdEnabled.Value = enabledHold; terrainAvoidance.Value = avoid; ApplyHoldSettings(); SaveSettings(); }
            GUI.Label(new Rect(12, 184, w, 64), flightHold?.Status ?? "Hold unavailable until local input setup is ready.");
            if (flightHold?.HasTargets == true)
            {
                var t = flightHold.Targets;
                float course = (float)((Math.Atan2(t.CourseX, t.CourseZ) * 180 / Math.PI + 360) % 360);
                GUI.Label(new Rect(12, 250, w, 40), "Captured: " + t.Speed.ToString("0") + " m/s HUD speed | " + t.Altitude.ToString("0") + " m sea altitude | course " + course.ToString("0") + "°");
            }
            if (GUI.Button(new Rect(12, 300, Math.Min(w, 310), 30), "Rearm after manual override")) flightHold?.Rearm();
            GUI.Label(new Rect(12, 345, w, 155), "Targets are captured automatically on map, supported external view or missile fullscreen. Overlapping views do not recapture.\n\nPitch/roll/yaw, throttle movement, brake, gear, vector or engine controls return manual control. Trigger must close/reopen, or use Rearm.\n\nTerrain OFF keeps ordinary hold, with NO avoidance climb. Stalls, damage, hover/tilt, low clearance and unsupported mappings can block/release hold.");
        }
        private void StopObservation()
        {
            captureKey = 0;

            modal?.Close();
            fullscreenInput?.Dispose();
            if (fullscreenInput?.Failure != null) fullscreenUnavailable = true;
            fullscreenInput = null;
            flightHold?.Dispose();
            flightHold = null;
            fullscreen.Ensure(false);
            mapWatch?.Dispose();
            if (mapWatch?.Failure != null && !mapUnavailable) { mapUnavailable = true; Notify("Map watch cleanup failed; map feature unavailable until restart."); }
            mapWatch = null;
            if (modal?.Failure != null && !failed) { failed = true; Logger.LogError("Observer modal/save cleanup failed: " + modal.Failure); }
            var oldGate = gate;
            if (oldGate == null) return;
            var priorFailure = oldGate.Failure;
            oldGate.Dispose(); // Reverse order: bridge subscription/resources, then owned hook.
            gate = null;
            if (oldGate.Failure != null && !ReferenceEquals(oldGate.Failure, priorFailure))
            {
                failed = true;
                Logger.LogError("Observer cleanup failed; restart required: " + oldGate.Failure);
            }
        }
        private void RemoveLifecycleHook()
        {
            try { if (harmony != null) harmony.UnpatchSelf(); } // This ID only; never unpatch other mods.
            finally
            {
                harmony = null;
                Volatile.Write(ref observedEpochs, null);
                Volatile.Write(ref prefixGate, null);
            }
        }
        private void OnDisable() => StopObservation();
        private void OnDestroy()
        {
            StopObservation();
            modal?.Dispose();
            if (earlySampler != null) { earlySampler.Tick = null; earlySampler.LostFocus = null; UnityEngine.Object.Destroy(earlySampler); }
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
        }
    }
}
