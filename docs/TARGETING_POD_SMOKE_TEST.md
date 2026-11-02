# Runtime acceptance checklist — not yet run

Use a backed-up offline mission. First test with only this DLL, then Observer 0.5.1, then the user's existing mod set. Do not treat compilation/static checks as these results.

- [ ] Default disabled leaves cockpit, controls, weapons and native IR unchanged. Enable/disable repeatedly; no orphan textures/cameras or recurring exceptions.
- [ ] Inspect diagnostic for unique target RawImage. Zero/two target consumers or renderer surfaces remain native. Target and separate landing image sharing source texture: only target texture changes.
- [ ] Target/landing ancestor overlap refuses replacement. Native cockpit/TacScreen replacement rebinds only after new ancestry/texture proof. Another mod replacing the owned texture is not overwritten during cleanup.
- [ ] Same output, aspect and sensor pose in cockpit/enlarged view through repeated toggles. Original label glyphs upright, unclipped on square/wide screens. Native source text remains readable, no duplicated misleading lock label interpreted as seeker acquisition.
- [ ] Manual entry needs explicit key settings/confirmation and native-unmapped keys. Binding any one sensor/toggle key to a native action refuses it; native action still works. Change bindings while active: mod releases. Test system/UI keyboard maps too.
- [ ] Other-mod raw-key shortcut conflicts are checked manually before confirmation. No claim of universal input conflict detection. Test custom keyboard layouts.
- [ ] Manual slew/FOV/recenter/inertial direction hold work. Flight controls and native firing continue normally; manual keys do not steer/fire/select targets. Camera motion does not change weapon/target list or acquire unseen units.
- [ ] Origin shifts preserve direction hold and aircraft-relative camera attachment without jumps. Home resets owned view only. Inertial hold is not ground track; label stays honest.
- [ ] No-target manual screen activation changes only target display; radar, time and landing layers are not altered. Release restores latest native target activation intent, including native toggles during ownership.
- [ ] Landing/gear transition, detached cockpit/sensor, ejection/death, respawn, aircraft and mission change promptly restore native behavior. No resurrection of a damaged/BSOD screen.
- [ ] Pause, map, menu/chat/console/text input, camera controls, focus loss and external view stop pod keys/rendering and require explicit reentry. Held keys do not reenter automatically.
- [ ] Observer absent, idle and hold-active: expected coexistence. Observer fullscreen/drain/modal active or unknown build: manual/enlarged view declines or releases without touching Observer input/hold state.
- [ ] Native COLOR/IR feed reflects game mode, manual mode says COLOR (OWN CAMERA), THERM/NIGHT unavailable. No cosmetic effect claimed as thermal.
- [ ] Missile/bomb/gun/no-weapon changes refresh labels; missing/stale/hidden hints show N/A. Selected target is not seeker lock; no fabricated launch-range scale or bomb impact cue.
- [ ] One/multiple/no targets suppress appropriate per-target telemetry; target changes do not reuse previous hint until a fresh native update.
- [ ] Measure vanilla vs native-overlay vs manual-camera frame time and GPU allocation. Enlarging must not add another camera render. Check prolonged play for resource growth.

Capture log, aircraft type, exact versions, screenshots and observed failures. On any unsafe behavior disable this plugin and retain native display; do not claim broad aircraft compatibility until tested.
