"""Structural integration checks only: do not load or execute Unity/game DLLs."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/TargetingPodDisplay'
def read(name):return (SRC/name).read_text()
class TargetingContract(unittest.TestCase):
    def test_separate_project(self):
        s=read('TargetingPodDisplay.csproj')
        self.assertIn('<AssemblyName>TargetingPodDisplay</AssemblyName>',s)
        self.assertNotIn('Observer.dll',s)
        self.assertNotIn('ProjectReference',s)
        self.assertEqual(s.count('<Reference Include='),s.count('<Private>false</Private>'))
    def test_default_opt_in_unbound(self):
        s=read('Plugin.cs')
        for snippet in ('"Enabled",false','"ReplaceCockpit",false','"Fullscreen",KeyCode.None','"ManualSensor",KeyCode.None','"ConfirmNoOtherModUsesSensorKeys",false'):
            self.assertIn(snippet,s)
    def test_native_state_never_mutated(self):
        s='\n'.join(p.read_text() for p in SRC.glob('*.cs'))
        for snippet in ('SetTargetCam(','.Fire(','.Seek(','.LaunchMount(','.SelectUnit(','.DeselectAll(','.ClearTargetList(','.SetActiveStation(','.SwitchIRState(','.SetValue(','.sharedMaterial =','flightControlsEnabled =','controls.pitch =','Harmony.Patch(','.GetInputs().'):
            self.assertNotIn(snippet,s)
    def test_owned_sensor_not_clone(self):
        s=read('OwnedSensor.cs')
        for token in ('new GameObject(', 'camera.enabled=false','SingleCameraRequest','SupportsRenderRequest','SubmitRenderRequest','renderPostProcessing=false','PhysicsLayers.CockpitMask'):
            self.assertIn(token,s)
        self.assertNotIn('Instantiate(',s)
        self.assertNotIn('mount.rotation',s)
        self.assertIn('context.Aircraft.transform.rotation',s)
    def test_source_read_only_and_composition_cleanup(self):
        s=read('Presentation.cs')
        self.assertIn('Graphics.Blit(source,Output)',s)
        self.assertIn('RenderTargetScope.Run(()=>RenderTexture.active,previous=>RenderTexture.active=previous',s)
        self.assertLess(s.index('RenderTargetScope.Run'),s.index('Graphics.Blit(source,Output)'))
        self.assertIn('finally { GL.PopMatrix(); }',s)
        self.assertNotIn('Destroy(source)',s)
    def test_cockpit_component_is_not_aircraft_damage_marker(self):
        s=read('NativeContract.cs')
        self.assertIn('GetComponentsInChildren<Cockpit>(true)',s)
        self.assertIn('Read<Aircraft>(candidate,"aircraft")',s)
        self.assertNotIn('Read<TacScreen>(context.Aircraft.cockpit',s)
        self.assertNotIn('var cockpit=context.Aircraft.cockpit;',s)
        self.assertIn('ResolveCockpit(context,out reason)',s)
    def test_unique_structural_consumer(self):
        s=read('NativeContract.cs')
        for token in ('"tacScreen"','"targetCamDisplay"','"landingCamDisplay"','IsChildOf','DisplayPolicy.Unique(matches)','ReferenceEquals(images[i].texture,source)','GetComponentsInChildren<Renderer>'):
            self.assertIn(token,s)
        self.assertNotIn('FindObjectsOfType',s)
        self.assertNotIn('sharedMaterial',s)
    def test_compare_restore_and_activation_intent(self):
        s=read('NativeContract.cs')
        self.assertIn('DisplayPolicy.MayRestore(image.texture,owned)',s)
        self.assertIn('nativeActive=change.enabled',s)
        self.assertIn('SetActive(nativeActive)',s)
        self.assertIn('targetCam.onCamToggle-=NativeToggle',s)
    def test_local_offline_guard(self):
        s=read('PodContext.cs')
        for token in ('GameState.SinglePlayer','server.Listening','client.IsHost','AuthenticatedPlayers.Count!=1','GameManager.IsLocalPlayer(pilot.player)','Identity.HasAuthority','!ReferenceEquals(pilot.currentState,pilot.playerState)','view.cockpitState','IsDetached()'):
            self.assertIn(token,s)
    def test_chat_close_waits_for_native_map_restore(self):
        s=read('PodContext.cs')
        ui=s[s.index('internal static bool UiClear()'):]
        self.assertIn('GameplayUI.AllowPauseKeybind',ui)
        self.assertIn('CursorManager.GetFlags()==CursorFlags.None',ui)
    def test_key_failure_diagnostics_do_not_alternate_with_cockpit_status(self):
        s=read('Plugin.cs')
        self.assertIn('private string keyStatus=',s)
        self.assertIn('if(!keys)SetKeyStatus(keyReason);else keyStatus="";',s)
        self.assertNotIn('SetStatus(keyReason)',s)
    def test_invalid_toggle_reason_names_setting_and_value(self):
        s=read('DedicatedKeys.cs')
        self.assertIn('"Keys.Fullscreen="+fullscreen',s)
        self.assertIn('"Keys.ManualSensor="+manual',s)
        self.assertIn('Choose None, F1-F6 or F12.',s)
    def test_fullscreen_does_not_require_cockpit_binding(self):
        s=read('Plugin.cs');context=read('PodContext.cs')
        self.assertIn('if(cockpitConfig.Value&&binding==null)',s)
        self.assertIn('if(image!=null)binding=new CockpitBinding',s)
        self.assertNotIn('ResolveCockpit',context)
        self.assertNotIn('binding==null)return',s)
    def test_key_collision_proof_not_remapping(self):
        s=read('DedicatedKeys.cs')
        for token in ('ReInput.players.AllPlayers','GetAllMaps(ControllerType.Keyboard)','map.AllMaps','element.keyCode','maps>128','elements>4096'):
            self.assertIn(token,s)
        self.assertNotIn('map.enabled=',s)
    def test_observer_read_only_exact_identity(self):
        s=read('ObserverArbitration.cs')
        self.assertIn('cb1a36f7a505375636468e58e4c806c29c42e3020394dc36e2327e517a06514e',s)
        for token in ('"fullscreen","Active"','"fullscreenInput","Draining"','"modal","IsOpen"','catch{return false;}'):
            self.assertIn(token,s)
        self.assertNotIn('SetValue',s)
    def test_native_hint_is_visible_fresh_same_station(self):
        s=read('NativeHint.cs')
        for token in ('ReferenceEquals(station,previousStation)','previousTargets','stamp<=changedAt','stamp>.25f','!hint.isActiveAndEnabled','"weaponStation"','"WPN HINT: "'):
            self.assertIn(token,s)
    def test_no_fake_thermal_or_seeker(self):
        s=read('Presentation.cs')
        self.assertIn('THERM N/A',s);self.assertIn('WPN LOCK N/A',s);self.assertIn('NIGHT N/A',s)
        self.assertIn('COLOR (OWN CAMERA)',s)
        self.assertNotIn('WPN LOCKED',s)
    def test_same_presentation_texture_fullscreen(self):
        s=read('Plugin.cs')
        self.assertIn('new CockpitBinding(context,image,presentation.Output)',s)
        self.assertIn('var output=presentation.Output',s)
        self.assertIn('presentation.Compose(sensor.Texture)',s)
        self.assertIn('AIRCRAFT CONTROLS LIVE / NOT AUTOPILOT',s)
    def test_configuration_generation_and_render_checks(self):
        s=read('Plugin.cs')
        self.assertIn('configuration.Poll(CurrentConfiguration()',s)
        self.assertIn('if(changed){Release();return;}',s)
        self.assertIn('configuration.Matches(CurrentConfiguration())',s)
        self.assertIn('if(manual||fullscreen)configuration.Block()',s)
        self.assertIn('Input.GetKey((KeyCode)key)',s)
    def test_continuous_bounded_consumer_reproof(self):
        s=read('NativeContract.cs')
        self.assertIn('FindConsumer(context,(RenderTexture)original,out _,image,owned)',s)
        self.assertIn('DisplayPolicy.CanBindHierarchy(',s)
        self.assertIn('ReferenceEquals(images[i].texture,ownedTexture)',s)
        self.assertIn('"landingCamDisplay"),landingRoot)',s)
    def test_no_private_evidence_or_images(self):
        for path in SRC.rglob('*'):
            if path.is_file() and 'bin' not in path.parts and 'obj' not in path.parts:
                self.assertNotIn(path.suffix.lower(),('.jpeg','.jpg','.png','.dll'))
if __name__=='__main__':unittest.main()
