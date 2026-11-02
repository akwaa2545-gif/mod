"""Native wiring guards; controller/state/terrain execute only through pure linked production code."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/Observer'
class FlightHoldContract(unittest.TestCase):
 def read(self,n):return (ROOT/n).read_text() if (ROOT/n).exists() else ''
 def test_single_axis_arbiter_and_exact_throttle_handover(self):
  f=self.read('FullscreenInput.cs');h=self.read('FlightHoldDriver.cs')
  self.assertIn('AxisArbiter.Choose',f);self.assertIn('WriteAxisCommand',f)
  self.assertIn('0x06000350',h);self.assertIn('0x040005e1',h);self.assertIn('0x040005dd',h)
  self.assertIn('ThrottleHandover.Convert',h);self.assertNotIn('.FilterInputs(',h)
 def test_bounded_terrain_and_actual_airframe_sensors(self):
  s=self.read('HoldAirframe.cs')
  for x in ('RaycastNonAlloc','QueryTriggerInteraction.Ignore','PhysicsLayers.StaticsMask','PhysicsLayers.ShipsMask','Datum.originPosition','GetNozzleAngle','GetRPMRatio','onApplyDamage','onPartDetached','IsChildOf','TerrainScanSchedule'):
   self.assertIn(x,s)
  self.assertNotIn('PhysicsLayers.Everything',s);self.assertNotIn('SetDestination',s)
 def test_actual_mouse_maps_not_arbitrary_controller_exclusion(self):
  s=self.read('HoldBindings.cs')
  for x in ('GetAllMaps(ControllerType.Mouse)','map.enabled','GetElementMapsWithAction','CheckForAssignmentConflict','controllerId','GetActionId'):
   self.assertIn(x,s)
  self.assertNotIn('GetLastActiveController',s)
 def test_default_off_separate_terrain_toggle_and_private_free_hash_gate(self):
  p=self.read('Plugin.cs');c=self.read('Compatibility.cs')
  self.assertIn('"FlightHold", "Enabled", false',p);self.assertIn('"FlightHold", "TerrainAvoidance", true',p)
  self.assertIn('e7c7467a81c3fd9952c694400dbbc17708fc365f7a444bdc00253f02a39b577b',c)

 def test_shared_writer_stays_at_axis_boundary_and_manual_exit_returns_native(self):
  f=self.read('FullscreenInput.cs');h=self.read('FlightHoldDriver.cs');p=self.read('Plugin.cs')
  self.assertIn('bool haveHold = !manual',f);self.assertIn('AxisArbiter.Choose(manual, haveHold, observer.Capturing())',f)
  self.assertIn('CancelForManualOverride()',f);self.assertIn('Exit(false)',f)
  self.assertIn('WriteAxisCommand(observer.hold.CurrentInputs, state, held, false)',f)
  self.assertNotIn('inputs.pitch =',h);self.assertNotIn('inputs.roll =',h)
  self.assertNotIn('FilterInputs(',f);self.assertNotIn('FilterInputs(',h)
  self.assertIn('Handover();',h);self.assertIn('SameHandoverOwner()',h)
  self.assertIn('HoldHudBounds()',p);self.assertIn('extraOcclusion?.Invoke(pointer)',self.read('MapWatch.cs'))

 def test_virtual_joystick_pan_requires_actual_native_cockpit_mode(self):
  self.assertIn('CameraStateManager.cameraMode == CameraMode.cockpit',self.read('FlightHoldDriver.cs'))
  for action in ('Pitch','Roll','Yaw'):self.assertIn('GetAxisRaw("'+action+'")',self.read('FlightHoldDriver.cs'))

 def test_raw_manual_detection_precedes_mapping_safety_and_remains_live_in_drain(self):
  h=self.read('FlightHoldDriver.cs');p=self.read('Plugin.cs')
  self.assertIn('ShouldObserve(lifecycle.Active, fullscreen(), releaseDrain())',h)
  self.assertIn('bool conflict = !intentional',h)
  self.assertLess(h.index('var raw = ReadRaw();',h.index('internal bool ObserveManual')),h.index('HoldBindings.MouseViewDisjoint(player',h.index('internal bool ObserveManual')))
  self.assertIn('ManualOverridePolicy.Response(intentional, conflict)',h)
  self.assertIn('() => fullscreenInput?.Draining == true',p)
