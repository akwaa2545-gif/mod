"""Cloud-only structural safety assertions. These do NOT simulate Unity runtime behavior."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
class IntegrationContractTests(unittest.TestCase):
    def read(self,name): return (ROOT/'src/Observer'/name).read_text() if (ROOT/'src/Observer'/name).exists() else ''
    def test_default_off_and_manual_shortcut_surface(self):
        s=self.read('Plugin.cs'); self.assertIn('"Enabled", false',s)
        self.assertIn('Input.GetKeyDown',s); self.assertIn('KeyCode.F8',s)
    def test_after_native_camera_lateupdate(self):
        self.assertIn('[DefaultExecutionOrder(100)]',self.read('Plugin.cs'))
    def test_hook_only_exact_public_declared_onenable(self):
        s=self.read('Plugin.cs'); self.assertIn('nameof(Missile.OnEnable)',s)
        self.assertIn('BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly',s)
        self.assertIn('method.ReturnType != typeof(void)',s)
        self.assertIn('method.MetadataToken != 0x06001a34',s)
        self.assertIn('harmony.UnpatchSelf()',s)
        self.assertEqual(s.count('harmony.Patch('),1)
    def test_standalone_urp_camera_and_finally(self):
        s=self.read('GameBridge.cs'); self.assertIn('UniversalRenderPipeline.SingleCameraRequest',s)
        self.assertIn('CameraRenderType.Base',s); self.assertIn('camera.enabled = false',s)
        self.assertIn('SupportsRenderRequest',s); self.assertIn('SubmitRenderRequest',s); self.assertIn('finally',s)
        self.assertNotIn('Camera.Render(',s); self.assertNotIn('AddComponent<AudioListener>',s)
        self.assertNotIn('Instantiate(',s)
    def test_no_native_control_mutations_and_protected_imgui(self):
        source='\n'.join(self.read(n) for n in ('Plugin.cs','GameBridge.cs'))
        self.assertIn('GUI.DrawTexture',source)
        for text in ('GUI.DragWindow','SetTarget(','SetAimpoint(','flightControlsEnabled =','allowInputs =','ConfigureNetwork(','SetFollowingUnit(','SwitchState('):
            self.assertNotIn(text,source)
    def test_reference_dlls_never_copy_local(self):
        s=self.read('Observer.csproj'); self.assertIn('<Private>false</Private>',s)
        self.assertIn('<TargetFramework>netstandard2.1</TargetFramework>',s)
        self.assertIn('<PlatformTarget>AnyCPU</PlatformTarget>',s)
    def test_guard_and_currentness_rechecked_at_render(self):
        s=self.read('GameBridge.cs'); self.assertIn('if (!IsReady || !IsCurrent(token))',s)
        self.assertIn('epochs.Read(selected) == selectedEpoch',s)
        self.assertIn('catalog.Refresh(candidates)',s)
    def test_deferred_candidate_not_discarded_same_phase(self):
        s=self.read('RegistrationQueue.cs'); self.assertIn('candidate.Stamp.Phase >= currentPhase',s)
        self.assertIn('inbox.Enqueue(candidate)',s)
    def test_runtime_uses_executable_startup_and_manual_catalog_seams(self):
        plugin=self.read('Plugin.cs'); bridge=self.read('GameBridge.cs')
        self.assertIn('new ObservationGate(',plugin); self.assertIn('gate.TryStart()',plugin)
        self.assertIn('gate.EnsureOperational()',plugin); self.assertIn('ObservePrefix(',plugin)
        self.assertIn('bridge.BeginObservation()',plugin)
        self.assertIn('new ManualCatalog<Missile>',bridge)
        self.assertIn('catalog.Select(token, show)',bridge)
        self.assertNotIn('epochs.Invalidate',bridge)
    def test_only_explicit_reference_graph_is_used(self):
        self.assertIn('<ExternallyResolved>true</ExternallyResolved>', self.read('Observer.csproj'))
    def test_fingerprint_has_original_disk_and_scoped_runtime_checks(self):
        s=self.read('Compatibility.cs'); policy=self.read('FingerprintPolicy.cs')
        self.assertIn('Paths.ManagedPath',s); self.assertIn('FingerprintPolicy.CheckGame',s)
        self.assertIn('FingerprintPolicy.IsSupportedLoader',s)
        self.assertIn('SHA256.Create()',policy); self.assertIn('ManifestModule.ModuleVersionId',s)
        self.assertIn('filename == "UnityEngine.CoreModule.dll"',policy)
        self.assertIn('runtime.Mvid != expectedMvid && (!loaderPatchedCore || !runtime.HasLoaderEntrypointReferences)',policy)
        self.assertIn('9e552383-bd56-40dc-8fe7-51f1744515e4',s)
    def test_readiness_rejection_is_explained_and_rate_limited(self):
        plugin=self.read('Plugin.cs'); bridge=self.read('GameBridge.cs')
        self.assertIn('new ReasonThrottle()',plugin)
        self.assertIn('readinessLog.ShouldLog(reason',plugin)
        self.assertIn('TryGetAnchor(out var anchor, out var reason)',plugin)
        self.assertIn('offline predicate rejected',bridge)
        self.assertIn('Server.Listening=',bridge)
if __name__ == '__main__': unittest.main()
