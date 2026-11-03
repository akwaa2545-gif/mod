from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/TargetingPodDisplay'
class CleanPresentation(unittest.TestCase):
 def test_raster_is_baked_and_flipped(self):
  s=(ROOT/'Presentation.cs').read_text()
  self.assertIn('OverlayRaster.Render',s)
  self.assertIn('height-1-y',s)
  self.assertIn('LoadRawTextureData',s)
  self.assertNotIn('new BitmapText',s)
 def test_no_native_finished_texture_composite(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertNotIn('presentation.Compose(source)',s)
  self.assertIn('sensor.Prepare(',s)
  self.assertIn('if(!fullscreen)',s)
 def test_passive_pip_and_direct_screen_overlay_only(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertIn('"PiPEnabled",false',s)
  self.assertIn('presentation.DrawOverlay(',s)
  self.assertIn('new FullscreenCameraLease(',s)

 def test_semantic_changes_invalidate_raster(self):
  s=(ROOT/'Presentation.cs').read_text()
  for token in ('target.persistentID.ToString()','ReferenceEquals(station,previousStation)','data.Hint','data.State','data.Sensor','refresh.ShouldRefresh'):
   self.assertIn(token,s)
  self.assertIn('if(aftermath)nativeHint.Clear()',s)

 def test_idle_context_does_not_repatch_or_create_sensor_every_frame(self):
  s=(ROOT/'Plugin.cs').read_text()
  idle=s[s.index('if(!available)'):s.index('if(next==null')]
  self.assertIn('HidePresentation(true)',idle)
  self.assertIn('HidePresentation(false)',idle)
  self.assertNotIn('new OwnedSensor',s[s.index('bool arbitration='):s.index('if(rearmed&&arbitration')])
  hide=s[s.index('private void HidePresentation'):s.index('private void Release()')]
  self.assertNotIn('aftermath?.Dispose()',hide)

 def test_first_kill_hook_waits_for_unique_target(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertIn('aftermath==null&&!aftermathUnavailable&&!manual&&targets.Count==1&&cam.isActiveAndEnabled',s)
  self.assertLess(s.index('var targets='),s.index('new AftermathTracker'))

 def test_reticle_uses_live_selected_target_not_seeker_claim(self):
  s=(ROOT/'Presentation.cs').read_text()
  self.assertIn('TargetSelected=!aftermath&&selected',s)
  self.assertIn('!target.disabled&&target.isActiveAndEnabled&&target.persistentID.IsValid',s)
  self.assertIn('data.Page="TARGET SELECTED"',s)
  self.assertIn('FLIGHT LIVE NATIVE',s)
  self.assertNotIn('WPN LOCKED',s)
  self.assertIn('data.TargetSelected',s[s.index('semantic='):])
