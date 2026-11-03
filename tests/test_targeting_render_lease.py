from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/TargetingPodDisplay'
class DirectRenderContract(unittest.TestCase):
 def test_exact_context_exception(self):
  s=(ROOT/'PodContext.cs').read_text()
  self.assertIn('FullscreenCameraLease lease=null',s)
  self.assertIn('lease!=null?!lease.Allows(view):!view.mainCamera.isActiveAndEnabled',s)
  self.assertNotIn('!view.mainCamera.isActiveAndEnabled&&(lease==null',s)
 def test_camera_components_only_and_restore(self):
  s=(ROOT/'FullscreenCameraLease.cs').read_text()
  self.assertIn('new RenderLease<Camera>',s)
  self.assertIn('ReferenceEquals(view.mainCamera,main)',s)
  self.assertIn('ReferenceEquals(view.cockpitCamRender,cockpit)',s)
  self.assertIn('onSwitchCamera+=',s)
  self.assertIn('onSwitchCamera-=',s)
  self.assertIn('ReferenceEquals(view.currentState,state)',s)
  self.assertNotIn('SetActive(',s)
 def test_real_direct_screen_route(self):
  s=(ROOT/'OwnedSensor.cs').read_text()
  self.assertIn('camera.targetTexture=null',s)
  self.assertIn('camera.rect=new Rect(0,0,1,1)',s)
  self.assertIn('camera.enabled=true',s)
  self.assertIn('Screen.width',s)
  self.assertIn('RestoreFog()',s)
  self.assertIn('endFrameRendering',s)
