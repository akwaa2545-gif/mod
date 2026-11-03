from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/TargetingPodDisplay'
class WheelIntegration(unittest.TestCase):
 def test_only_fullscreen_consumes_wheel(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertIn('fullscreen&&wheelLease!=null&&wheelLease.TryConsume',s)
  self.assertIn('sensor.ApplyWheel(wheelDelta,manual)',s)
  self.assertIn('wheelLease?.Request(false)',s)
  self.assertIn('wheelLease?.Clear()',s)
  self.assertIn('wheelLease?.Dispose()',s)
 def test_wheel_never_changes_native_fov(self):
  s=(ROOT/'OwnedSensor.cs').read_text()
  self.assertIn('WheelZoom.Adjust',s)
  self.assertIn('wheelFov=null',s)
  self.assertNotIn('context.Camera.fieldOfView=',s)
  self.assertNotIn('native.fieldOfView=',s)

 def test_camera_entry_waits_for_verified_wheel_boundary(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertIn('fullscreenRequested',s)
  self.assertIn('wheelLease.Active&&!wheelLease.Draining',s)
  self.assertIn('"FullscreenWheelZoom",true',s)
  self.assertIn('OnWheelOwnershipLost',s)
 def test_hud_owner_loss_does_not_reacquire(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertIn('!hudLease.IsCurrent(context))){Release();',s)

 def test_first_entry_sample_has_direct_camera_state(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertLess(s.index('sensor.SetDirect(fullscreen)'),s.index('wheelLease.TryConsume'))
  self.assertLess(s.index('sensor.ApplyWheel'),s.index('sensor.Prepare'))
