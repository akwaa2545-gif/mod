"""Diagnostic/shortcut native wiring, supplemented by executable production predicate tests."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/'src/Observer'
class InputDiagnostics(unittest.TestCase):
 def text(self,n):return (ROOT/n).read_text() if (ROOT/n).exists() else ''
 def test_specific_pilot_diagnostics_preserve_native_predicates(self):
  f=self.text('FullscreenInput.cs');h=self.text('FlightHoldDriver.cs');p=self.text('LocalPilotContext.cs')
  self.assertIn('LocalPilotContext.TryGet',f);self.assertIn('LocalPilotContext.TryGet',h)
  for marker in ('HasEjected()', 'cockpit == null', 'cockpit.IsDetached()', 'pilots == null', 'PilotEligibility.Evaluate', 'ReferenceEquals(candidate.currentState, candidate.playerState)', 'GameManager.playerInput == null', 'GetInputs() == null', 'PlayerSettings.virtualJoystickEnabled', 'SceneSingleton<FlightHud>.i == null'):
   self.assertIn(marker,p)
  self.assertIn('DescribeCandidates',p);self.assertIn('GetType().Name',p)
  self.assertIn('Countermeasure manager unavailable',f)
  self.assertNotIn('Fullscreen needs the live local player pilot and attached cockpit.',f)
 def test_shared_safe_gameplay_shortcut_scope_and_existing_config_identity(self):
  p=self.text('Plugin.cs')
  self.assertIn('InputPolicy.MissileShortcutAllowed',p)
  self.assertIn('if (missileShortcuts && Input.GetKeyDown((KeyCode)cycleKey.Value))',p)
  self.assertIn('"Toggle PiP"',p);self.assertIn('"Next missile"',p)
  self.assertIn('Config.Bind("Keys", "View"',p);self.assertIn('Config.Bind("Keys", "Next"',p)

 def test_native_local_player_identity_replaces_unused_serialized_flag_at_all_control_boundaries(self):
  p=self.text('LocalPilotContext.cs');h=self.text('FlightHoldDriver.cs');f=self.text('FullscreenInput.cs')
  self.assertIn('GameManager.IsLocalPlayer(candidate.player)',p)
  self.assertIn('GameManager.IsLocalPlayer(pilot.player)',h)
  self.assertIn('GameManager.IsLocalPlayer(pilot.player)',f)
  self.assertNotIn('&& pilot.playerControlled',h+f)
  self.assertNotIn('candidate.ejected, candidate.playerControlled,',p)

 def test_stored_steering_clear_is_identity_gated_and_never_writes_filtered_control_object(self):
  h=self.text('FlightHoldDriver.cs');f=self.text('FullscreenInput.cs')
  reset=h.split('private void ClearVirtualJoystick()')[1].split('private void Handover()')[0]
  self.assertIn('if (!SameHandoverOwner()) return;',reset)
  self.assertIn('StoredSteering.Clear(ref state.pitchInput, ref state.rollInput, ref state.yawInput)',reset)
  self.assertIn('StoredSteering.Clear(ref pilotState.pitchInput, ref pilotState.rollInput, ref pilotState.yawInput)',f)
  self.assertNotIn('inputs.pitch =',reset);self.assertNotIn('inputs.roll =',reset)

 def test_next_fullscreen_adoption_is_explicit_manual_only_and_lifetime_guarded(self):
  f=self.text('FullscreenInput.cs');p=self.text('Plugin.cs')
  nextbody=f.split('internal void NextMissile()')[1].split('internal void CancelContext()')[0]
  self.assertIn('bridge.Cycle(1)',nextbody);self.assertIn('CanRetainManualSelection',nextbody)
  self.assertIn('Context(true, out _, out _, false)',nextbody);self.assertIn('bridge.TargetReady',nextbody)
  self.assertIn('fullscreenInput.NextMissile()',p)
 def test_fullscreen_teardown_does_not_clear_new_manual_input_after_capture_already_ended(self):
  f=self.text('FullscreenInput.cs')
  exitbody=f.split('private void Exit(bool userReturn)')[1].split('private void Fail')[0]
  self.assertIn('bool hadCapture = mode.Active || drain.Active;',exitbody)
  self.assertIn('if (hadCapture) NeutralizeJoystick();',exitbody)

 def test_fullscreen_cached_raw_reset_checks_captured_control_and_rewired_identity(self):
  f=self.text('FullscreenInput.cs')
  reset=f.split('private void NeutralizeJoystick()')[1].split('private bool HeldExitInputs')[0]
  self.assertIn('StoredSteering.SameInputObjects(controls, bridge.NativeAircraft.GetInputs(), player, GameManager.playerInput)',reset)
