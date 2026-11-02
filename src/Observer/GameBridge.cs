#nullable disable
using System;
using System.Threading;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Player = NuclearOption.Networking.Player;

namespace NuclearOptionObserver
{
    // Integration is deliberately specific to the statically inspected uploaded build.
    internal sealed class GameBridge : IPictureBridge, IDisposable
    {
        internal sealed class Anchor
        {
            internal Player Player;
            internal Aircraft Aircraft;
            internal CameraStateManager CameraManager;
            internal Camera Camera;
            internal int SceneHandle;
        }
        private static long nextContext;
        private readonly Anchor anchor;
        private readonly PersistentID aircraftId;
        private readonly LifetimeEpochs<Missile> epochs;
        private readonly Func<bool> lifecycleReady;
        private readonly Func<long> sceneVersion;
        private readonly long initialSceneVersion;
        private readonly int mainThread;
        private readonly MissilePicture picture;
        private readonly ManualCatalog<Missile> catalog;
        private readonly LaunchObservation<Missile> launches;
        private readonly ulong context;
        private readonly RefreshCadence refresh = new RefreshCadence();
        private bool disposed, selectedDisabled;
        private Missile selected;
        private PersistentID selectedId;
        private long selectedEpoch;
        private EntityToken selectedToken;
        private bool fullscreen;
        private float orbitYaw, orbitPitch;
        private GameObject cameraObject;
        private Camera camera;
        private RenderTexture texture;
        private readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        internal bool Faulted => picture.Faulted;
        internal Exception Failure => picture.LastFailure;
        internal string Status => catalog.Selected != null ? catalog.Selected.Label : picture.TrackingEnded ? "Tracking ended — choose another missile manually" : "No missile selected — pause, then open panel";
        internal RenderTexture Texture => catalog.Visible && picture.HasSelection && IsCurrent(selectedToken) ? texture : null;
        internal bool Visible => catalog.Visible;
        internal ManualCatalog<Missile> Catalog => catalog;
        internal GameBridge(Anchor anchor, LifetimeEpochs<Missile> epochs, Func<long> sceneVersion, Func<bool> lifecycleReady)
        {
            this.anchor = anchor;
            aircraftId = anchor.Aircraft.persistentID;
            this.epochs = epochs;
            this.lifecycleReady = lifecycleReady;
            this.sceneVersion = sceneVersion;
            initialSceneVersion = sceneVersion();
            mainThread = Thread.CurrentThread.ManagedThreadId;
            context = Next(ref nextContext);
            catalog = new ManualCatalog<Missile>(context);
            launches = new LaunchObservation<Missile>(epochs, lifecycleReady, context);
            picture = new MissilePicture(this); // Deliberately hidden. Existing Enabled=true is not auto-open.
        }
        internal Aircraft NativeAircraft => anchor.Aircraft;
        internal CameraStateManager NativeCamera => anchor.CameraManager;
        internal EntityToken TargetToken => selectedToken;
        internal bool TargetReady => picture.HasSelection && IsCurrent(selectedToken);
        internal void SetVisible(bool value) { AssertMainThread(); if (catalog.Visible != value) catalog.ToggleVisible(); picture.SetVisible(value); }
        internal void SetPresentation(bool full, float yaw, float pitch)
        { AssertMainThread(); if (fullscreen != full) Release(); fullscreen = full; orbitYaw = yaw; orbitPitch = pitch; }
        internal ulong Context => context;
        internal void BeginObservation()
        {
            AssertMainThread(); if (disposed) throw new ObjectDisposedException(nameof(GameBridge));
            anchor.Aircraft.onRegisterMissile += OnOwnRegistration;
            anchor.Aircraft.onDeregisterMissile += OnOwnDeregistration;
        }
        private void OnOwnRegistration(Missile missile) => launches.Observe(missile, true, ReferenceEquals(missile, null) ? 0 : missile.persistentID.Id);
        private void OnOwnDeregistration(Missile missile) => launches.Observe(missile, false);
        internal void SetAutoShowOwnLaunch(bool value) { AssertMainThread(); launches.SetEnabled(value); }
        private static ulong Next(ref long counter)
        {
            long value = Interlocked.Increment(ref counter);
            if (value <= 0) throw new InvalidOperationException("Observer lifetime counter exhausted; restart required.");
            return (ulong)value;
        }
        internal static bool TryGetAnchor(out Anchor result) => TryGetAnchor(out result, out _);
        internal static bool TryGetAnchor(out Anchor result, out string reason)
        {
            result = null;
            reason = "readiness not established";
            uint facts = 0;
            if (GameManager.gameState != GameState.SinglePlayer) return Reject("game state=" + GameManager.gameState + "; native SinglePlayer required", out reason);
            facts |= OfflineSnapshot.SinglePlayer;
            if (!NetworkManagerNuclearOption.IsLoaded) return Reject("network manager not loaded", out reason);
            facts |= OfflineSnapshot.ManagerLoaded;
            if (NetworkManagerNuclearOption.IsLoadingScene) return Reject("mission scene loading", out reason);
            facts |= OfflineSnapshot.NotLoading;
            var manager = NetworkManagerNuclearOption.i;
            if (manager == null) return Reject("loaded network manager unavailable", out reason);
            facts |= OfflineSnapshot.ManagerPresent;
            var server = manager.Server;
            var client = manager.Client;
            if (server == null || client == null) return Reject("network server/client unavailable", out reason);
            facts |= OfflineSnapshot.ServerPresent | OfflineSnapshot.ClientPresent;
            if (server.Active) facts |= OfflineSnapshot.ServerActive;
            if (!server.Listening) facts |= OfflineSnapshot.NotListening;
            if (client.IsConnected) facts |= OfflineSnapshot.ClientConnected;
            if (client.IsHost) facts |= OfflineSnapshot.ClientHost;
            if (ReferenceEquals(server.LocalClient, client)) facts |= OfflineSnapshot.SameLocalClient;
            var networkPlayer = server.LocalPlayer;
            if (networkPlayer != null) facts |= OfflineSnapshot.LocalNetworkPlayerPresent;
            var participants = server.AuthenticatedPlayers;
            if (participants != null && participants.Count == 1)
            {
                facts |= OfflineSnapshot.ExactlyOneParticipant;
                if (ReferenceEquals(participants[0], networkPlayer)) facts |= OfflineSnapshot.SameAuthenticatedPlayer;
            }
            if (!GameManager.GetLocalPlayer<Player>(out var player) || player == null || !player.isActiveAndEnabled) return Reject("local player unavailable/inactive", out reason);
            facts |= OfflineSnapshot.PlayerReady;
            if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft == null || !aircraft.isActiveAndEnabled || aircraft.disabled || !aircraft.persistentID.IsValid) return Reject("local aircraft unavailable/inactive/disabled or ID invalid", out reason);
            facts |= OfflineSnapshot.AircraftReady;
            var nativeCamera = SceneSingleton<CameraStateManager>.i;
            if (nativeCamera == null || !nativeCamera.isActiveAndEnabled || nativeCamera.mainCamera == null || !nativeCamera.mainCamera.isActiveAndEnabled) return Reject("gameplay camera unavailable/inactive", out reason);
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !aircraft.gameObject.scene.isLoaded || !nativeCamera.mainCamera.gameObject.scene.isLoaded) return Reject("gameplay/aircraft/camera scene unavailable or unloaded", out reason);
            facts |= OfflineSnapshot.CameraReady;
            if (!Phase1Policy.CanActivate(true, new OfflineSnapshot(facts)))
                return Reject("offline predicate rejected: Server.Active=" + server.Active + "; Server.Listening=" + server.Listening +
                    "; Client.IsConnected=" + client.IsConnected + "; Client.IsHost=" + client.IsHost +
                    "; same local client=" + ReferenceEquals(server.LocalClient, client) + "; authenticated count=" + (participants == null ? -1 : participants.Count) +
                    "; local participant present=" + (networkPlayer != null) + "; same sole participant=" + (participants != null && participants.Count == 1 && ReferenceEquals(participants[0], networkPlayer)), out reason);
            result = new Anchor { Player = player, Aircraft = aircraft, CameraManager = nativeCamera, Camera = nativeCamera.mainCamera, SceneHandle = scene.handle };
            reason = "offline mission ready";
            return true;
        }
        private static bool Reject(string message, out string reason) { reason = message; return false; }
        internal bool Matches(Anchor live) => !disposed && sceneVersion() == initialSceneVersion &&
            live.SceneHandle == anchor.SceneHandle && ReferenceEquals(live.Player, anchor.Player) &&
            ReferenceEquals(live.Aircraft, anchor.Aircraft) && ReferenceEquals(live.CameraManager, anchor.CameraManager) &&
            ReferenceEquals(live.Camera, anchor.Camera) && anchor.Aircraft.persistentID == aircraftId;
        public bool IsReady
        {
            get { AssertMainThread(); return !disposed && lifecycleReady() && TryGetAnchor(out var live) && Matches(live); }
        }
        private void OnSelectedDisabled(Unit unit) { selectedDisabled = true; }
        internal void Tick(double now)
        {
            AssertMainThread();
            if (!IsReady) { Dispose(); return; }
            if (launches.Failure != null) throw new InvalidOperationException("Own-launch observer callback failed.", launches.Failure);
            if (!ReferenceEquals(selected, null) && !IsCurrent(selectedToken)) EndCurrent();
            if (refresh.Due(now))
            {
                var candidates = new System.Collections.Generic.List<MissileCandidate<Missile>>();
                // The native main-thread registry is the only discovery source; never a global Unity search.
                foreach (var unit in UnitRegistry.allUnits)
                {
                    if (!(unit is Missile missile) || !Eligible(missile)) continue;
                    string type = missile.definition != null ? missile.definition.unitName : missile.name;
                    string owner = missile.owner != null ? missile.owner.definition != null ? missile.owner.definition.unitName : missile.owner.name : "unknown";
                    float distance = Vector3.Distance(missile.transform.position, anchor.Aircraft.transform.position);
                    candidates.Add(new MissileCandidate<Missile>(missile, missile.persistentID.Id, epochs.Read(missile),
                        type + " | owner " + owner + " #" + missile.ownerID.Id + " | " + distance.ToString("0") + " m"));
                }
                catalog.Refresh(candidates);
                if (!ReferenceEquals(selected, null) && catalog.Selected == null) EndCurrent();
            }
            foreach (var registration in launches.TakeReady())
            {
                var missile = registration.Target;
                if (missile == null || !OwnLaunchPolicy.Accept(launches.IsCurrent(registration), Eligible(missile),
                    ReferenceEquals(missile.owner, anchor.Aircraft), missile.ownerID.Id, aircraftId.Id, registration.EntityId, missile.persistentID.Id)) continue;
                if (TryCaptureMissile(missile, out var snapshot)) SelectObserved(snapshot, true);
            }
            picture.Tick(now);
            if (!picture.HasSelection && !ReferenceEquals(selected, null)) EndCurrent();
        }
        internal bool Select(EntityToken token, bool show = false)
        {
            AssertMainThread();
            if (!IsReady) return false;
            var row = System.Linq.Enumerable.FirstOrDefault(catalog.Rows, r => Phase1Policy.IsCurrent(token, r.Token));
            if (row == null || !Eligible(row.Candidate.Target) || !row.Candidate.MatchesIdentity(row.Candidate.Target, row.Candidate.Target.persistentID.Id, epochs.Read(row.Candidate.Target))) return false;
            EndCurrent();
            if (!catalog.Select(token, show)) return false;
            selected = row.Candidate.Target;
            selectedId = selected.persistentID;
            selectedEpoch = row.Candidate.Epoch;
            selectedToken = row.Token;
            selectedDisabled = false;
            selected.onDisableUnit += OnSelectedDisabled;
            if (picture.Track(selectedToken)) { picture.SetVisible(catalog.Visible); return true; }
            EndCurrent(); return false;
        }
        internal bool TryCaptureMissile(Missile missile, out MissileCandidate<Missile> snapshot)
        {
            AssertMainThread(); snapshot = null;
            if (!IsReady || !Eligible(missile) || !UnitRegistry.allUnits.Contains(missile)) return false;
            string type = missile.definition != null ? missile.definition.unitName : missile.name;
            string owner = missile.owner != null ? missile.owner.definition != null ? missile.owner.definition.unitName : missile.owner.name : "unknown";
            float distance = Vector3.Distance(missile.transform.position, anchor.Aircraft.transform.position);
            snapshot = new MissileCandidate<Missile>(missile, missile.persistentID.Id, epochs.Read(missile),
                type + " | owner " + owner + " #" + missile.ownerID.Id + " | " + distance.ToString("0") + " m");
            return true;
        }
        internal bool SelectObserved(MissileCandidate<Missile> snapshot, bool show)
        {
            AssertMainThread();
            if (snapshot == null || !IsReady || !Eligible(snapshot.Target) || !UnitRegistry.allUnits.Contains(snapshot.Target) ||
                !snapshot.MatchesIdentity(snapshot.Target, snapshot.Target.persistentID.Id, epochs.Read(snapshot.Target))) return false;
            var row = catalog.Observe(snapshot);
            return row != null && Select(row.Token, show);
        }
        internal void ClearSelection() { AssertMainThread(); EndCurrent(); }
        internal void ToggleVisible() { AssertMainThread(); catalog.ToggleVisible(); picture.SetVisible(catalog.Visible); }
        internal void Cycle(int direction)
        {
            AssertMainThread();
            var row = catalog.Adjacent(direction);
            if (row != null) Select(row.Token);
        }
        private bool Eligible(Missile missile) => missile != null && missile.isActiveAndEnabled &&
            missile.gameObject.activeInHierarchy && !missile.disabled && missile.persistentID.IsValid &&
            missile.gameObject.scene.isLoaded && missile.gameObject.scene.handle == anchor.Aircraft.gameObject.scene.handle &&
            UnitRegistry.TryGetUnit(missile.persistentID, out var live) && ReferenceEquals(live, missile);
        public bool IsCurrent(EntityToken token)
        {
            AssertMainThread();
            return !disposed && !selectedDisabled && Phase1Policy.IsCurrent(token, selectedToken) &&
                !ReferenceEquals(selected, null) && Eligible(selected) && selected.persistentID == selectedId && epochs.Read(selected) == selectedEpoch;
        }
        public void Render(EntityToken token)
        {
            AssertMainThread();
            if (!IsReady || !IsCurrent(token)) throw new InvalidOperationException("Observer guard/target invalidated before render.");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) throw new NotSupportedException("Active pipeline is not the matched URP; observer disabled.");
            EnsureResources();
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new NotSupportedException("URP SingleCameraRequest unsupported; observer disabled.");
            // Native CameraStateManager LateUpdate (order 2) has already shifted the world; never shift it ourselves.
            var pose = selected.transform;
            Vector3 position = pose.position;
            Vector3 forward = pose.forward;
            ChasePose view; string reason;
            bool valid = fullscreen ? ChaseView.TryOrbit(new ChaseVector(position.x, position.y, position.z), new ChaseVector(forward.x, forward.y, forward.z), orbitYaw, orbitPitch, out view, out reason) :
                ChaseView.TryCreate(new ChaseVector(position.x, position.y, position.z), new ChaseVector(forward.x, forward.y, forward.z), out view, out reason);
            if (!valid)
                throw new InvalidOperationException("Observer chase pose invalid: " + reason);
            var viewPosition = new Vector3(view.Position.X, view.Position.Y, view.Position.Z);
            var viewForward = new Vector3(view.Forward.X, view.Forward.Y, view.Forward.Z);
            var viewUp = new Vector3(view.Up.X, view.Up.Y, view.Up.Z);
            camera.transform.SetPositionAndRotation(viewPosition, Quaternion.LookRotation(viewForward, viewUp));
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try { RenderPipeline.SubmitRenderRequest(camera, request); }
            finally { if (camera != null) camera.targetTexture = previousTarget; RenderTexture.active = previousActive; }
        }
        private void EnsureResources()
        {
            var size = RenderSize.ForScreen(fullscreen, Screen.width, Screen.height);
            if (camera != null && texture != null && texture.width == size.Width && texture.height == size.Height) return;
            if (camera != null || texture != null) Release();
            cameraObject = new GameObject("NuclearOptionObserver.Camera");
            // New scene-local root, not a clone and not persistent; no AudioListener or native camera scripts.
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = anchor.Camera.farClipPlane;
            camera.aspect = (float)size.Width / size.Height;
            camera.cullingMask = ~(PhysicsLayers.UIMask | PhysicsLayers.HUDMask | PhysicsLayers.CockpitMask | PhysicsLayers.PreviewRenderMask | PhysicsLayers.EditorSelectOnlyMask);
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.allowXRRendering = false;
            data.antialiasing = AntialiasingMode.None;
            texture = new RenderTexture(size.Width, size.Height, 16, RenderTextureFormat.ARGB32) { name = "NuclearOptionObserver.PiP", useMipMap = false, autoGenerateMips = false };
            if (!texture.Create()) throw new InvalidOperationException("Observer RenderTexture allocation failed.");
            request.destination = texture;
            request.mipLevel = 0;
            request.face = CubemapFace.Unknown;
            request.slice = 0;
        }
        private void EndCurrent()
        {
            picture.EndTracking();
            if (!ReferenceEquals(selected, null)) selected.onDisableUnit -= OnSelectedDisabled;
            selected = null;
            catalog.Clear();
            selectedDisabled = false;
            selectedToken = default;
        }
        public void Release()
        {
            AssertMainThread();
            request.destination = null;
            var oldObject = cameraObject;
            var oldTexture = texture;
            cameraObject = null; camera = null; texture = null;
            try { if (oldObject != null) Object.Destroy(oldObject); }
            finally { if (oldTexture != null) { try { oldTexture.Release(); } finally { Object.Destroy(oldTexture); } } }
        }
        public void Dispose()
        {
            AssertMainThread();
            if (disposed) return;
            Volatile.Write(ref disposed, true);
            launches.Dispose();
            anchor.Aircraft.onRegisterMissile -= OnOwnRegistration;
            anchor.Aircraft.onDeregisterMissile -= OnOwnDeregistration;
            EndCurrent();
            picture.Dispose();
        }
        private void AssertMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new InvalidOperationException("Observer Unity access must run on its main thread.");
        }
    }
}
