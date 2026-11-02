"""Static wiring evidence only. Pure executable harness covers gesture/queue policy, not Unity runtime."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/Observer'
class MapLaunchContract(unittest.TestCase):
 def source(self,name):return (ROOT/name).read_text() if (ROOT/name).exists() else ''
 def test_launch_is_separate_off_by_default_actual_owner_event(self):
  p=self.source('Plugin.cs');b=self.source('GameBridge.cs')
  self.assertIn('"AutoShowOwnLaunch", false',p);self.assertIn('bridge.SetAutoShowOwnLaunch',p)
  self.assertIn('onRegisterMissile += OnOwnRegistration',b);self.assertIn('onRegisterMissile -= OnOwnRegistration',b)
  self.assertIn('OwnLaunchPolicy.Accept',b);self.assertIn('registration.EntityId',b);self.assertIn('ReferenceEquals(missile.owner, anchor.Aircraft)',b)
  self.assertIn('launches.TakeReady()',b);self.assertIn('SelectObserved(snapshot, true)',b)
 def test_exact_separate_map_hooks_and_lazy_phase(self):
  s=self.source('MapWatch.cs')
  for marker in ('0x06000f0d','0x06000e7a','BindingFlags.DeclaredOnly','MapIcon.ClickSource','nameof(ObserveIconClick)','nameof(ObserveMapSelect)','ObservePointerPhase','mapHarmony.UnpatchSelf()'):
   self.assertIn(marker,s)
  self.assertNotIn('MapControls',s);self.assertNotIn('GetCursorCoordinates',s);self.assertNotIn('GetOrAddIcon',s)
 def test_actual_top_hit_only_and_exact_native_icon(self):
  s=self.source('MapWatch.cs')
  for marker in ('RaycastAll','hits[0]','GetComponentInParent<UnitMapIcon>','icon.iconImage.raycastTarget','map.TryGetIcon','ReferenceEquals(registeredIcon, icon)','map.iconLayer','IsChildOf','CursorFlags.Map','menuWeaponSafety'):
   self.assertIn(marker,s)
  self.assertNotIn('foreach (var hit',s);self.assertNotIn('TryGetNearest',s)
 def test_map_action_is_explicit_show_not_toggle(self):
  s=self.source('MapWatch.cs');p=self.source('Plugin.cs')
  self.assertIn('gesture.TryTake',s);self.assertIn('bridge.SelectObserved(hit.Target, true)',s);self.assertNotIn('ToggleVisible',s)
  self.assertIn('mapWatch?.Tick()',p);self.assertIn('mapWatch?.Dispose()',p)

 def test_visible_imgui_pip_excluded_with_shared_render_geometry(self):
  m=self.source('MapWatch.cs');p=self.source('Plugin.cs')
  self.assertIn('viewLayout().BlocksPointer(bridge.Visible, pointer.x, pointer.y, Screen.height)',m)
  self.assertIn('new MapWatch(bridge, () => modal.IsOpen || fullscreen.Active, CurrentViewLayout, HoldHudBlocksPointer)',p)
  self.assertIn('var layout = CurrentViewLayout()',p)
  self.assertIn('layout.Height + ViewLayout.StatusHeight',p)
