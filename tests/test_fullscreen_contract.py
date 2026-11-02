"""Static Unity/Harmony wiring evidence; pure mode, sizing, scope, drain and orbit execute in C#."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/Observer'
class FullscreenContract(unittest.TestCase):
 def test_exact_scoped_guards_and_no_whole_camera_or_physics_skip(self):
  s=(ROOT/'FullscreenInput.cs').read_text() if (ROOT/'FullscreenInput.cs').exists() else ''
  for token in ('0x0600009f','0x060000e3','0x0600034f','0x0600034e','0x06001015','0x06000fa1','0x06000fa3','0x06000fc3'):
   self.assertIn(token,s)
  self.assertIn('finalizer:',s);self.assertIn('ScopedCameraMask',s);self.assertIn('ReleaseBoundary',s);self.assertIn('SalvoInProgress',s)
  self.assertNotIn('nameof(CameraStateManager.LateUpdate)',s);self.assertNotIn('nameof(PilotPlayerState.FixedUpdateState)',s)
  self.assertIn('Countermeasures(false',s);self.assertIn('SetVirtualJoystick(Vector3.zero)',s)
 def test_fullscreen_uses_owned_render_path_and_early_sampler(self):
  b=(ROOT/'GameBridge.cs').read_text();p=(ROOT/'Plugin.cs').read_text()
  self.assertIn('RenderSize.ForScreen',b);self.assertIn('ChaseView.TryOrbit',b)
  self.assertIn('FullscreenEarlySampler',p);self.assertIn('ScaleMode.ScaleToFit',p)
 def test_rewired_pinned_and_no_input_maps_cursor_or_pilot_state_writes(self):
  c=(ROOT/'Compatibility.cs').read_text();s='\n'.join(p.read_text() for p in ROOT.glob('*.cs'))
  self.assertIn('3ae8897b0a184775ddcf6af6cf55413c3fe015a7c752f8df1bc0e05a088640d4',c)
  for marker in ('SwitchState(','flightControlsEnabled =','Cursor.visible =','Cursor.lockState = ','ForceHidden(','SetFollowingUnit('):self.assertNotIn(marker,s)

 def test_raw_input_writer_only_at_axis_boundary_not_sampler_or_button_root(self):
  s=(ROOT/'FullscreenInput.cs').read_text()
  sample=s.split('internal void Sample()')[1].split('private static void CameraPrefix')[0]
  self.assertNotIn('Neutralize();',sample)
  self.assertIn('harmony.Patch(pilotButtons, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(PilotButtonsPrefix)))',s)
  self.assertIn('harmony.Patch(pilotAxes, prefix: new HarmonyMethod(typeof(FullscreenInput), nameof(PilotAxesPrefix)))',s)
  self.assertIn('PilotInputBoundary.AllowNative',s)
  self.assertIn('ExitActions.AnyHeld(player.GetButton)',s)
