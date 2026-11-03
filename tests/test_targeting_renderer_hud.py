from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/TargetingPodDisplay'
class RendererHudContract(unittest.TestCase):
 def test_native_renderer_is_explicit_and_verified(self):
  s=(ROOT/'SensorRenderContract.cs').read_text()
  for token in ('"m_RendererIndex"','owned.SetRenderer(index)','ReferenceEquals(owned.scriptableRenderer,native.scriptableRenderer)','source.cullingMask&~excluded','native.requiresDepthTexture','native.requiresColorTexture'):
   self.assertIn(token,s)
  self.assertNotIn('CopyFrom(',s)
  self.assertNotIn('sharedMaterial',s)
 def test_dedicated_canvas_only_not_hud_logic(self):
  s=(ROOT/'FullscreenHudLease.cs').read_text()
  for token in ('"canvas"','FlightHud','"topRightPanel"','hudMapAnchor','maximizedMapCanvas','GetComponentsInChildren<Canvas>','canvas.enabled=false'):
   self.assertIn(token,s)
  self.assertNotIn('SetActive(',s)
  self.assertNotIn('hud.enabled=',s)
