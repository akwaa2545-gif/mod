# 0.5.1 bounded input diagnosis + shortcut delta

Recovered baseline is exact sanitized0.5.0-c8bfbd5 archive. User reports F11 denies
live pilot/cockpit while piloting, and hold appears to retain an earlier mouse roll.
Runtime log confirms F11 reached the guard, not which predicate failed. Do not
weaken ownership or blame other mods without evidence.

1. Test the existing pilot predicate matrix, then use that production pure seam in
   native pilot selection. Preserve eligibility, but give specific ejection/cockpit/
   pilot-count/control/state/input/HUD/countermeasure rejection reasons, with bounded
   candidate-state details on failed F11 attempts. Same diagnostics improve hold status.
2. Inspect successful hold capture/virtual-stick clearing and raw vs virtual mouse
   inputs. Prove UI/map/free-look motion cannot be takeover, deliberate flight intent
   remains live and handback cannot restore an old virtual offset. Only change a
   demonstrated faulty path; otherwise report precise unconfirmed state.
3. Reuse persisted Keys.View/F9 and Keys.Next/F10, label Toggle PiP and Next missile.
   Extend Next to safe gameplay without opening the menu, preserving input capture,
   modifiers, native UI guards and manual-only selection. No new duplicate config.
4. Pure executable tests + structural checks; exact supplied refs compile only,
   both official loader reference builds0warnings/errors; sanitized packages/source,
   independent parent review before delivery. No game/Unity execution/PC changes.


Investigation outcome: matched native Pilot.playerControlled0x04001D94 has no direct
IL reads/writes across14,181 method bodies. Native player entry uses
GameManager.IsLocalPlayer(pilot.player). Replace the legacy flag consistently with
that actual native identity, preserving null/alive/exact-state/aircraft/unique/offline
gates; report predicate-specific failures. This is a static defect correction, not
a passed runtime retest or proven diagnosis of every failed attempt.

Native map/radial early return can replay cached pitchInput/rollInput/yawInput before
updating the HUD-derived virtual axes. Center stored mouse plus those cached raw
fields only on validated ownership transitions, never filtered ControlInputs; do not
clear later manual input during idempotent teardown after capture ended. UI/map/free-look
Pan/Tilt exclusion and deliberate raw flight override remain unchanged.

Explicit gameplay Next retains fullscreen only for a same-owner live manual selection;
other discovery/launch selection changes do not adopt a captured fullscreen lifetime.
