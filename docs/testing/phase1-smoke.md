# Observer 0.5.0 EXPERIMENTAL fullscreen/orbit + inherited camera/map/menu checks — PC smoke gate

Cloud compile/pure tests do not pass these checks. All rows need actual game evidence.
Use the confirmed existing 64-bit Windows / Unity 2022.3.62.7762112 / game 0.34.6 /
BepInEx 5.4.23.5 environment, native offline single-player. Do not update the loader.
Close game, back up/replace only the existing Observer.dll at its current location
(user currently has BepInEx/plugins/Observer.dll). No relocation; preserve config/other mods
and avoid duplicates.

1. Disabled baseline: ordinary mission/camera/input/audio unchanged; no Observer camera/UI.
2. Existing Enabled=true migrates without losing opt-in; startup logs correct 0.5.0/identities.
   View starts hidden, no selection. With AutoShowOwnLaunch=false, several launches must neither show nor select anything.
3. Esc pause, mouse released, F8 opens panel. Native settings submenu/text input must block it.
   Unpaused F8 gives visible instruction, no panel. Alt-tab/locked cursor must close/refuse it.
4. With native menuWeaponSafety false and flight controls otherwise enabled: visible instruction,
   no interactive panel, no setting mutation. Restore the user's preferred safe native setting.
5. With safe native pause state: click list, sliders and blank panel/shield areas.
   Native Resume/Restart/Settings beneath must NOT activate; no weapon launches/ammo changes.
   Repeat with pointer over native buttons before opening; opening while mouse held is refused.
6. Native keyboard Submit/Navigation cannot activate underlying menu while Observer owns focus.
7. All shows valid friendly/enemy/unknown missiles from different aircraft and ground launchers.
   Bullets/non-Missile custom projectiles need not appear. Stable #unit/lifetime identity survives
   reorder and changing distance. The one mod menu has a Missile Camera section and no extra filter/search/sort UI.
   No missiles shows a clear count.
8. Choose an existing missile: selection changes, view remains hidden. Show view explicitly.
   Owner/type/distance match observed missile. With AutoShowOwnLaunch=false, new launches never change tracking or visibility.
9. Close panel, Esc resume: selected view continues. F9 toggles during unobstructed gameplay,
   not while typing/in native menus. Clicking flight PiP is NOT a control; do not use it as one.
10. Pause/open panel, F10 and Next/Previous buttons cycle the all-live list manually, wrapping in
    both directions. Never show a hidden view. Clear ends tracking without choosing
    another. An empty list leaves selection empty.
11. Distinct unused F-key rebinding persists after panel close and restart. Duplicate/non-F-key/
    modifiers rejected. Native text entry and key capture suppress shortcuts. Check native/other-mod conflicts.
12. Move/resize/scale panel controls; close normally and via native Esc resume. Restart confirms
    persistence. Change resolution/scale with view near edges: PiP stays within screen bounds.
13. Selected missile despawns/explodes/disables/re-enables: tracking ends, no fallback. Pooled/new
    lifetime needs explicit selection. A stale row click cannot revive an old lifetime.
14. F8 close/reopen, Close button, Esc/native resume, scene unload/player aircraft replacement,
    feature disable and repeated close leave no invisible blocker, stale selected UI object,
    extra camera/audio listener, retained textures or logs growing every frame. Native menu and
    gameplay control remain usable; plugin never resumes the game or changes cursor flags.
15. UI raycast priority loss/context uncertainty refuses/closes panel rather than leaving unsafe
    interactive controls. Capture log diagnostics if this occurs with normal native menu state.
16. Offline guard: loading/unknown/networked state must stop and release. Never join multiplayer
    with this candidate intentionally enabled to 'test' authority; use verified offline controls.
17. Fly far/origin shifts, pause/resume and track several missile types. Native camera unaffected;
    render requests max20Hz/no catch-up; performance/visual/audio behavior acceptable.
18. Disable, exit, remove only Observer, restart normal offline mission. No loader/game changes.

Stop immediately on input/menu click-through, unexpected weapons/camera behavior, network guard
failure, persistent shield/cursor loss, allocation growth or log spam. Preserve BepInEx log and
exact steps, disable/remove Observer with game closed. Do not claim runtime safety from static tests.

## 0.3.0 additional required runtime rows — all NOT RUN

19. Open native maximized map, Ctrl+left a real visible own/friendly/hostile missile
    icon: Observer selects and shows once, native target list/selectedIcons/main
    camera unchanged. Repeat with custom native Rewired Select bound to mouse:
    neither native Mouse nor Controller route duplicates/retargets the Watch action.
20. Release Ctrl before left-button release; no accidental native target selection.
    Subsequent unrelated normal/controller selection works normally. Ordinary left,
    Shift selection, right-click commands and native panning retain prior behavior.
21. Overlaid UI (including Observer PiP image/status at changed position/scale/
    resolution), non-missile icon, hidden/untracked/no-icon missile and already-selected
    non-raycastable native icon do not produce a guessed watch target or extra icons.
    Use list fallback. Stale/dead/changed-ID/epoch/map/context capture is a no-op,
    never a fallback into native targeting. Focus/menu/modal/guard loss cancels dispatch.
22. AutoShowOwnLaunch starts OFF on upgrade/new config; ON only arms future own
    registrations, never existing missiles. New own launch selects/shows when ON;
    other owners' launches do nothing. OFF leaves current tracking intact, discards
    pending events and prevents all future automatic selection/visibility changes.
    ON→OFF→ON cannot revive queued events. Restart verifies saved toggle value.
23. Scene/anchor changes and repeated disable/stop release own map hooks and event
    subscriptions. Map hook setup/failure leaves manual list controls available;
    no global MapControls skip, native icon/selected state or extra map asset mutation.

## 0.3.1 targeted camera retest — NOT RUN

24. Launch/select the same missile type with aircraft bank 0/45/90/180 degrees at
    comparable heading/pitch. PiP must not bank/orbit from missile roll; missile
    transform/pivot remains centered. Change cockpit/head-look view independently:
    it must not alter the mod camera pose. Check horizontal, climbing, descending,
    vertical/nearly-vertical flight and native floating-origin shifts. Horizon is
    world-up away from vertical; exactly vertical uses a stable world-axis fallback.
    Confirm native flight/weapon/camera behavior unchanged. No smoothing/mode added.

## 0.4.0 additional runtime gate — all NOT RUN

25. Start in PiP. Select a live missile; normal own-aircraft cockpit/orbit + F11
    enters fullscreen; F7 resets; RMB configured Pan/Tilt rotates only the owned
    missile view. Missile remains centered, world-up, roll/cockpit independent.
    F11 returns PiP, F9 hides; keys survive restart and duplicate keys are rejected.
    Existing custom F8/F9/F10 bindings survive new-slot collision normalization.
26. Fullscreen refuses other native camera states, following another unit, dead/
    ejected/detached/ambiguous local pilots and active salvos, with an explanation.
    Exact guard-install failure leaves PiP working; no unguarded fullscreen entry.
27. During fullscreen, drag RMB and press manual pitch/roll/yaw/brake/Fire/gear/
    eject inputs: exact local pilot input production is neutral/held-throttle,
    without affecting AI/other pilots or skipping GLOC/aircraft filtering/physics.
    Verify neutral command writes occur only before native axis filtering; button/
    render phases must not erase assist/filter corrections before physics consumers.
    Entry releases held manual countermeasures. Automatic turrets/existing shots
    are not globally stopped. This is NOT flight hold or terrain avoidance.
28. Native camera origin/environment/follow/pose maintenance still runs. Camera
    view queries only are masked inside exact cockpit/orbit scopes, with Free Look
    latched at entry. Outside scope/native UI/other players stay normal. Inspect
    exception/finalizer recovery without a retained query mask or repeated patches.
29. RMB release stops orbit, one sample per render frame even with multiple physics
    ticks. F11/F9 return or target death while RMB/fire/view key remains held returns
    image immediately but consumes that same live local pilot's held/release inputs;
    visible release cue disappears after release frame, then normal input resumes.
    Hold/release keyboard Countermeasures, Switch View, Next/Previous Weapon and
    other suppressed buttons too: no new manual firing/countermeasure restart or
    release-driven action leaks on return. Absolute axes do not prolong the drain. Do not release by arbitrary timeout.
30. Pause/map/chat/radial/camera UI, loss of focus, scene/offline/aircraft/pilot
    replacement/death/ejection exit fullscreen and cancel capture/drain immediately;
    UI keys remain usable. No stale input writes or automatic reentry after returning.
    Repeated entry/exit/disable/removal leaves no camera/audio/RT/hook/input-map/cursor
    ownership leaks. No autopilot/AI state was changed or supposedly restored.
31. Resize/fullscreen toggle at normal and ultrawide/portrait aspect: bounded image
    stays unstretched, allocations only on mode/resolution changes,20Hz no bursts.
    Native game and mod PiP behavior remain unchanged after returning/removing.

## 0.5 experimental Flight Hold / terrain toggle (NOT RUN)

Use a healthy aircraft initially high and level over open water, gear retracted,
forward wingborne mode and ample airspeed. Be ready for immediate manual takeover.
Never treat these checks or the status display as a ground-safety guarantee.

- Master OFF: all existing camera/map/fullscreen controls behave as before; no hold
  input/throttle ownership. Both toggles persist without losing existing layout/keys.
- Enable from paused menu Flight Hold section. Open map / supported own external view /
  missile fullscreen: capture current HUD speed(m/s), sea altitude and ground course ONCE.
  Overlap/transition map→external→fullscreen does not reset targets or snap throttle.
- Check actual pitch/roll/yaw signs first, short stable holds, trim/throttle handover,
  loadout/fuel/assist variants. Normal fixed-wing and actual forward-nozzle Swivel VTOL
  require separate validation. Unsupported/nozzle/hover/gear/stall/damage/engine-out/
  low-clearance/large-attitude states show a reason, never forced flight modes.
- Move pitch/roll/yaw, absolute throttle from its baseline, relative throttle, vector,
  brake/gear/engine/eject/assist: immediate native recovery same axis tick, no automatic
  reengagement while trigger remains. In fullscreen, intentional flight input returns
  the image and control. Rearm or full trigger cycle captures NEW current targets.
- RMB orbit and native Free Look alone do not cancel through virtual joystick. Enabled
  overlapping Mouse view/flight bindings block combined hold/fullscreen explicitly;
  joystick/keyboard overrides remain live. Change mappings/chat while active: no stale
  proof, disabled-map zeros or masked camera deltas become flight intent. If mapping
  becomes invalid under RMB simultaneously with raw joystick/keyboard/composite flight
  intent, manual input wins immediately, including a later still-valid release-drain
  frame; no neutral fallback traps steering until RMB release. Unsupported changed
  overlap can be treated as manual intent because combined sources cannot be separated.
- Verify exactly one input owner and one native filter pass; no render/button writes
  erase filtered outputs. Native AI/pilot ownership and weapon/countermeasure actions
  stay unchanged for plain hold. Existing fullscreen salvo/weapon/release rules remain.
- Pause freezes targets/integration; resume without recapture. Focus/scene/offline/pilot/
  aircraft loss releases without writing another state. Disable/re-enable/uninstall
  leaves no hooks, cached event handlers, throttle jumps or stale joystick deflection.
- Terrain ON: water, gradual land, ridge and descending-track warnings/demand show
  bounded climb and near-level bank, preserving the original altitude target; test
  insufficient recovery and invalid/stale/truncated sensors release with reason.
  Fly through floating-origin shifts: global target/sample heights do not jump.
- Toggle Terrain OFF while active: ordinary captured hold remains, no avoidance climb
  or terrain queries; targets and ordinary-controller history are preserved. Re-enable
  refreshes sensing. Clear hazard hysteresis avoids climb on/off chatter and bounded
  return to original altitude does not turn into a dive.
- Flight Hold status is visible while enabled, including blocked/latched reasons.
  Ctrl+left on that drawn HUD area cannot Watch a hidden map icon behind it; hidden
  HUD blocks nothing. Existing PiP geometry occlusion remains correct.

## 0.5.1 targeted runtime retest (NOT RUN)

- Back up/replace only Observer.dll, preserve config; start native offline with an
  ordinary local cockpit/orbit and live selected missile. F11 should enter fullscreen
  with the corrected native local-player predicate. If refused, capture the new precise
  message/candidate-state details from LogOutput.log. Null/remote/AI/dead/ejected/
  detached/ambiguous pilot contexts must still refuse, no safety weakening.
- With a native virtual-mouse steering offset before opening map, enable eligible
  hold: confirm ACTIVE and captured targets once, stored joystick/raw axes centered,
  wings stabilize rather than replay old roll. Map/UI/free-look mouse motion alone
  must not cancel; deliberate joystick/keyboard/flight input still cancels immediately.
  Return manual (also map/radial exit before the next physics tick): no old steering
  offset or cached axis replay. Repeated teardown after ownership ended must not clear
  new native manual commands. No extra filter pass or post-filter output writes.
- F9 Toggle PiP and F10 Next work in ordinary gameplay without opening settings;
  Next is manual only, leaves a hidden view hidden and wraps the same live list.
  In valid fullscreen, explicit Next keeps the presentation with the new live target;
  death/context loss still returns safely. Other launch/discovery never adopts an old
  fullscreen capture. Menu/text/key-edit/modifier suppression and saved distinct keys
  remain. Verify renamed buttons use existing View/Next config entries.
