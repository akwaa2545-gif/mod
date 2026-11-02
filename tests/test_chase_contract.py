"""Pure pose math runs in C#; these only assert Unity integration/no native pose mutation."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/Observer'
class ChaseContract(unittest.TestCase):
 def test_render_uses_roll_independent_target_centered_math(self):
  s=(ROOT/'GameBridge.cs').read_text().split('public void Render(')[1].split('private void EnsureResources')[0]
  self.assertIn('ChaseView.TryCreate',s);self.assertIn('pose.forward',s);self.assertIn('Quaternion.LookRotation',s)
  self.assertNotIn('pose.rotation',s);self.assertNotIn('pose.up',s);self.assertNotIn('anchor.Camera.transform',s)
  self.assertIn('throw new InvalidOperationException("Observer chase pose invalid: " + reason)',s)
 def test_no_new_runtime_vector_dependency(self):
  self.assertNotIn('System.Numerics',(ROOT/'ChaseView.cs').read_text())
