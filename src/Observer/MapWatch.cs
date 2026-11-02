#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using NuclearOption.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NuclearOptionObserver
{
    // Exact native map hooks only. Never call native selection/command or manufacture map icons.
    internal sealed class MapWatch : IDisposable
    {
        private static MapWatch current;
        private readonly GameBridge bridge;
        private readonly Func<bool> modalOpen;
        private readonly Func<ViewLayout> viewLayout;
        private readonly Func<Vector2, bool> extraOcclusion;
        private readonly MapGesture<MissileCandidate<Missile>> gesture = new MapGesture<MissileCandidate<Missile>>();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private Harmony mapHarmony;
        private ObservationGate mapGate;
        private Exception failure;
        private DynamicMap observedMap;
        private bool disposed;
        internal Exception Failure => failure ?? mapGate?.Failure;
        internal MapWatch(GameBridge bridge, Func<bool> modalOpen, Func<ViewLayout> viewLayout, Func<Vector2, bool> extraOcclusion = null) { this.bridge = bridge; this.modalOpen = modalOpen; this.viewLayout = viewLayout; this.extraOcclusion = extraOcclusion; }
        internal bool TryInstall(out string reason)
        {
            try
            {
                var click = typeof(UnitMapIcon).GetMethod(nameof(UnitMapIcon.ClickIcon), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(MapIcon.ClickSource) }, null);
                var select = typeof(DynamicMap).GetMethod("SelectFromMap", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
                if (click == null || click.DeclaringType != typeof(UnitMapIcon) || click.IsStatic || !click.IsVirtual || click.ReturnType != typeof(void) || click.MetadataToken != 0x06000f0d ||
                    select == null || select.DeclaringType != typeof(DynamicMap) || !select.IsPrivate || select.IsStatic || select.IsVirtual || select.ReturnType != typeof(void) || select.MetadataToken != 0x06000e7a)
                { reason = "Exact native map selection hooks unavailable; list controls remain available."; return false; }
                mapGate = new ObservationGate(
                    () => {
                        mapHarmony = new Harmony(Plugin.Id + ".map");
                        mapHarmony.Patch(click, prefix: new HarmonyMethod(typeof(MapWatch), nameof(ObserveIconClick)));
                        mapHarmony.Patch(select, prefix: new HarmonyMethod(typeof(MapWatch), nameof(ObserveMapSelect)));
                        return true;
                    },
                    () => Volatile.Write(ref current, this),
                    () => { if (ReferenceEquals(Volatile.Read(ref current), this)) Volatile.Write(ref current, null); gesture.Reset(); hits.Clear(); },
                    () => { try { if (mapHarmony != null) mapHarmony.UnpatchSelf(); } finally { mapHarmony = null; } });
                if (!mapGate.TryStart()) { reason = "Map watch hook setup failed; list controls remain available: " + mapGate.Failure?.GetType().Name; return false; }
                reason = "Ctrl+left native missile icon watch ready.";
                return true;
            }
            catch (Exception ex)
            {
                failure = ex;
                Dispose();
                reason = "Map watch hook setup failed; list controls remain available: " + ex.GetType().Name;
                return false;
            }
        }
        private static bool ObserveMapSelect(DynamicMap __instance)
        {
            var observer = Volatile.Read(ref current);
            if (observer == null) return true;
            observer.ObservePointerPhase();
            return !observer.gesture.SuppressMapSelect(__instance);
        }
        private static bool ObserveIconClick(UnitMapIcon __instance, MapIcon.ClickSource clickSource)
        {
            var observer = Volatile.Read(ref current);
            if (observer == null) return true;
            observer.ObservePointerPhase();
            return !observer.gesture.SuppressIcon(observer.observedMap, __instance, (int)clickSource);
        }
        private bool ContextReady(DynamicMap map) => !disposed && Failure == null && mapGate != null && mapGate.CanObserve && bridge.IsReady && !modalOpen() &&
            map != null && map.isActiveAndEnabled && map.gameObject.activeInHierarchy && DynamicMap.mapMaximized && map.IsCursorInMapRectangle() &&
            !GameplayUI.GameIsPaused && !LeaderboardMenu.IsOpen() && !ModalSurface.HasNativeTextFocus() &&
            Application.isFocused && Cursor.visible && Cursor.lockState == CursorLockMode.None && CursorManager.GetFlags() == CursorFlags.Map &&
            (!GameManager.flightControlsEnabled || PlayerSettings.menuWeaponSafety);
        private void ObservePointerPhase()
        {
            try
            {
                var map = SceneSingleton<DynamicMap>.i;
                observedMap = map;
                gesture.CancelIfMapChanged(map);
                bool ready = ContextReady(map);
                bool modifiers = MapCapturePolicy.CanCapture(ready, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift), Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
                    Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand), Input.GetMouseButton(1) || Input.GetMouseButton(2));
                gesture.Observe(Time.frameCount, Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0), ready, modifiers, () => CaptureTopIcon(map));
            }
            catch (Exception ex) { failure = ex; gesture.Cancel(); } // Never throw through native UI callbacks.
        }
        private MapHit<MissileCandidate<Missile>> CaptureTopIcon(DynamicMap map)
        {
            var pointer = Input.mousePosition;
            if (viewLayout().BlocksPointer(bridge.Visible, pointer.x, pointer.y, Screen.height) || extraOcclusion?.Invoke(pointer) == true) return null; // IMGUI is not part of EventSystem raycasts.
            var system = EventSystem.current;
            if (system == null || !system.isActiveAndEnabled || map == null || map.iconLayer == null) return null;
            hits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = Input.mousePosition }, hits);
            if (hits.Count == 0 || hits[0].gameObject == null) return null;
            var top = hits[0].gameObject;
            var icon = top.GetComponentInParent<UnitMapIcon>();
            if (!top.activeInHierarchy || icon == null || !icon.isActiveAndEnabled || !icon.gameObject.activeInHierarchy ||
                icon.iconImage == null || !icon.iconImage.isActiveAndEnabled || !icon.iconImage.raycastTarget ||
                !(ReferenceEquals(top, icon.iconImage.gameObject) || top.transform.IsChildOf(icon.iconImage.transform)) ||
                !icon.transform.IsChildOf(map.iconLayer.transform) || !(icon.unit is Missile missile) ||
                !map.TryGetIcon(missile, out var registeredIcon) || !ReferenceEquals(registeredIcon, icon) ||
                !bridge.TryCaptureMissile(missile, out var snapshot)) return null;
            return new MapHit<MissileCandidate<Missile>>(map, icon, snapshot);
        }
        internal void Tick()
        {
            if (disposed) return;
            try
            {
                ObservePointerPhase();
                if (Failure == null && gesture.TryTake(out var hit)) bridge.SelectObserved(hit.Target, true);
            }
            catch (Exception ex) { failure = ex; gesture.Cancel(); }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            mapGate?.Dispose();

        }
    }
}
