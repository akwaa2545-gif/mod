# Observer 0.2.0 — manual missile view

User confirmed 0.1.1 works in-game and requested manual activation, position/shortcut settings and missiles belonging to any faction/owner. User opted for autonomous implementation rather than more design questions. Their latest scope explicitly narrows QoL to saved position/size/shortcuts and manual next/previous controls, inside one mod menu with Missile Camera as its first section; search/filter/sorting/reset are excluded.

## Design / choices
- Retain the exact offline/compatibility gate and standalone passive 20 Hz render path. No guidance, primary camera, flight controls, input maps, time scale or cursor writes.
- Replace aircraft launch subscriptions with a bounded (0.5 s) snapshot of the actual `UnitRegistry.allUnits`, accepting only active registered `Missile` components. A lifetime token combines context, persistent ID, managed reference and OnEnable epoch. Refresh/launch never selects or opens the view. Selection loss clears rather than switching.
- Use the existing IMGUI rendering for one small main mod menu with a Missile Camera settings/selector section. It opens only under the native paused menu, with a separate top-sorted fullscreen uGUI raycast blocker and cleared uGUI selection; close automatically if pause/menu/readiness is lost. If that surface cannot be established, refuse opening. No native pause/resume or cursor calls.
- Panel configures persisted PiP bounds/UI scale and distinct function-key bindings (defaults F8 panel, F9 view, F10 next). Restrict bindings to function keys to avoid Mouse/Space/Enter/flight-axis shortcuts. Panel and cycle are paused-only; view toggle is allowed only in the unobstructed gameplay context or own panel. Disable shortcuts while typing/capturing. Native user bindings cannot be statically certified conflict-free; document checking/rebinding.
- View begins hidden even with existing Enabled=true. Config schema migration preserves opt-in and discards old auto-launch behavior. Bounds clamp at each draw after resolution/scale changes. Position/size are edited only in the protected paused panel; view is display-only during flight.

Alternatives: full uGUI panel would add more scene/UI complexity; IMGUI without a uGUI blocker would permit native menu click-through and is rejected. The small hybrid keeps existing renderer and isolates clicks without altering native controls.

## Execution plan (test first)
1. Pure manual catalog tests: all-owner candidates, stable tokens, explicit selection/visibility, refresh never selects, epoch/death/scene clearing, explicit next/previous wrapping, bounded refresh. Implement production-used catalog seam.
2. Pure settings/input/modal-lifecycle tests: bounds/scale clamp, finite-value fallback, schema migration, round-trip persistence values, F-key collision rejection, paused/menu/text gate, failed-open rollback and idempotent close. Implement production-used helpers.
3. Verify exact uploaded Unity/game UI APIs statically, add explicit non-copy-local UI/input references, wire manual catalog into GameBridge, then paused panel and modal owner into Plugin. Add structural integration checks (not represented as runtime proof).
4. Run all pure/Python tests; compile against both supported loaders with zero diagnostics; package only own DLL/readme/config/docs; verify exact hashes/member allowlist, sanitized tracked source archive and baseline diff. Commit explicit files locally. Parent independently reviews. Game runtime retest remains required.

## Review focus
Modal creation/rollback and close on resume; old config opt-in preserved but view hidden; pooled missile epoch and stale-row clicks rejected; no launch subscription/auto-open left; resolution/scale/NaN bounds; rebinding and text-entry suppression; no authority/cursor mutation. Offline/static evidence is not runtime UI/performance proof.
