#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;
using NuclearOption.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NuclearOptionObserver
{
    // Owned uGUI shield intercepts native menu raycasts. IMGUI alone is NOT an input shield.
    internal sealed class ModalSurface : IDisposable
    {
        private GameObject root, shield;
        private EventSystem system;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly ModalSession session;
        internal ModalSurface(Action onClosed) { session = new ModalSession(Create, DestroyOwned, onClosed); }
        internal bool IsOpen => session.IsOpen;
        internal Exception Failure => session.Failure;
        internal static bool HasNativeTextFocus()
        {
            if (NuclearOption.MissionEditorScripts.InputFieldChecker.InsideInputField) return true;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            foreach (var component in selected.GetComponents<Component>())
                if (component != null && (component is InputField || component.GetType().FullName == "TMPro.TMP_InputField")) return true;
            return false;
        }
        internal static bool NativeContext(out string reason)
        {
            reason = "Pause with Esc first, then open Observer.";
            var ui = SceneSingleton<GameplayUI>.i;
            var menu = SceneSingleton<LeaderboardMenu>.i;
            if (!GameplayUI.GameIsPaused || ui == null || !ui.isActiveAndEnabled || ui.menuCanvas == null || !ui.menuCanvas.isActiveAndEnabled || !LeaderboardMenu.IsOpen()) return false;
            if (menu == null || menu.SettingsMenuOpen) { reason = "Close native Settings before opening Observer."; return false; }
            if (!Application.isFocused || !Cursor.visible || Cursor.lockState != CursorLockMode.None) { reason = "Observer needs the native pause-menu cursor visible and unlocked."; return false; }
            // Native PilotPlayerState.PlayerControls otherwise permits Fire even while the mission is paused.
            if (GameManager.flightControlsEnabled && !PlayerSettings.menuWeaponSafety)
            { reason = "Enable the game's menu weapon safety setting before using Observer (menuWeaponSafety)."; return false; }
            if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled) { reason = "Native menu EventSystem unavailable; Observer panel cannot safely open."; return false; }
            if (!InputPolicy.PanelAllowed(true, GameplayUI.GameIsPaused, ui.menuCanvas.isActiveAndEnabled && LeaderboardMenu.IsOpen(),
                Application.isFocused && Cursor.visible && Cursor.lockState == CursorLockMode.None, false,
                !GameManager.flightControlsEnabled || PlayerSettings.menuWeaponSafety)) return false;
            reason = ""; return true;
        }
        internal bool TryOpen(out string reason)
        {
            if (!NativeContext(out reason)) return false;
            if (HasNativeTextFocus()) { reason = "Finish native text entry before opening Observer."; return false; }
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) { reason = "Release mouse buttons before opening Observer."; return false; }
            if (session.TryOpen()) return true;
            reason = "Observer could not establish a modal raycast shield; panel left closed.";
            return false;
        }
        private bool Create()
        {
            system = EventSystem.current;
            root = new GameObject("NuclearOptionObserver.Modal", typeof(RectTransform));
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            int highest = int.MinValue;
            foreach (var layer in SortingLayer.layers) if (layer.value > highest) { highest = layer.value; canvas.sortingLayerID = layer.id; }
            canvas.sortingOrder = short.MaxValue;
            root.AddComponent<GraphicRaycaster>();
            shield = new GameObject("Observer raycast shield", typeof(RectTransform));
            shield.transform.SetParent(root.transform, false);
            var rect = (RectTransform)shield.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = shield.AddComponent<Image>();
            image.color = new Color(0, 0, 0, .001f); // Nonzero alpha avoids transparent-mesh culling.
            image.raycastTarget = true;
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            // Remove native keyboard/submit focus; select only our handler-free shield. No old native focus restoration.
            system.SetSelectedGameObject(shield);
            return OwnsRaycast(new Vector2(Screen.width / 2f, Screen.height / 2f)) && OwnsRaycast(Input.mousePosition);
        }
        private bool OwnsRaycast(Vector2 position)
        {
            if (system == null || shield == null) return false;
            hits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = position }, hits);
            return hits.Count > 0 && ReferenceEquals(hits[0].gameObject, shield);
        }
        internal bool Maintain(out string reason)
        {
            if (!IsOpen) { reason = ""; return false; }
            if (!session.Maintain(NativeContext(out reason) && ReferenceEquals(system, EventSystem.current))) return false;
            system.SetSelectedGameObject(shield);
            if (OwnsRaycast(Input.mousePosition)) return true;
            reason = "Observer lost the modal raycast shield; panel closed for safety.";
            Close(); return false;
        }
        private void DestroyOwned()
        {
            var old = root;
            var oldShield = shield;
            var oldSystem = system;
            root = null; shield = null; system = null; hits.Clear();
            try
            {
                if (oldSystem != null && ReferenceEquals(oldSystem.currentSelectedGameObject, oldShield)) oldSystem.SetSelectedGameObject(null);
            }
            finally
            {
                // Disable immediately; Unity destruction is deferred. Always attempt destroy even if deselection fails.
                if (old != null) { try { old.SetActive(false); } finally { Object.Destroy(old); } }
            }
        }
        internal void Close() => session.Close();
        public void Dispose() => session.Dispose();
    }
}
