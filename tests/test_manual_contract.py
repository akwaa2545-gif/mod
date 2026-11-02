"""Static wiring checks, not Unity/runtime evidence; behavior tested in the pure executable harness."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/Observer'
class ManualContractTests(unittest.TestCase):
 def test_registry_discovery_and_separate_optional_launch_subscriptions(self):
  s=(ROOT/'GameBridge.cs').read_text()
  self.assertIn('UnitRegistry.allUnits',s); self.assertIn('refresh.Due(now)',s)
  self.assertIn('row.Candidate.MatchesIdentity(row.Candidate.Target, row.Candidate.Target.persistentID.Id, epochs.Read(row.Candidate.Target))',s)
  self.assertIn('onRegisterMissile += OnOwnRegistration',s); self.assertNotIn('FindObjects',s)
  self.assertIn('new ManualCatalog<Missile>',s); self.assertIn('epochs.Read(selected)',s)
 def test_safe_modal_and_no_authority_mutation(self):
  s=(ROOT/'ModalSurface.cs').read_text()
  for marker in ('GraphicRaycaster','raycastTarget = true','RaycastAll','SetSelectedGameObject','GameplayUI.GameIsPaused','menuWeaponSafety'):
   self.assertIn(marker,s)
  source='\n'.join(p.read_text() for p in ROOT.glob('*.cs'))
  for marker in ('flightControlsEnabled =','CursorManager.SetFlag','Cursor.visible =','Time.timeScale =','SetTarget(','SetAimpoint(','SwitchState('):
   self.assertNotIn(marker,source)
  self.assertNotRegex(source,r'Cursor\.lockState\s*=(?!=)')
 def test_schema_and_manual_defaults(self):
  s=(ROOT/'Plugin.cs').read_text()
  self.assertIn('"0.5.0"',s); self.assertIn('ObserverPreferences.Normalize',s)
  self.assertNotIn('picture.SetVisible(true)',(ROOT/'GameBridge.cs').read_text())
  self.assertIn('InputPolicy.GameplayShortcutAllowed',s)
  self.assertIn('new ModalSurface(OnPanelClosed)',s)
  self.assertIn('Mod Menu',s); self.assertIn('Missile Camera',s)
  self.assertIn('Previous missile',s); self.assertIn('Next missile',s)
  self.assertNotIn('GUI.TextField',s); self.assertNotIn('MissileFilter',s)
