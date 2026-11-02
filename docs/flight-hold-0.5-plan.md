# 0.5.0 EXPERIMENTAL flight hold + optional terrain avoidance

Default OFF. One new Flight Hold section in the existing mod menu: Enabled, independent TerrainAvoidance(defaultON), rearm after manual override, read-only status/captured targets. No speed/altitude entry, gain UI, combat AI or native pilot/AI handoff. Capture current horizontal ground course, HUD speed(m/s), global sea altitude and trim/throttle ONCE at a qualifying map/supported-external/fullscreen edge; overlap/one-frame transition coalescing cannot recapture targets. Manual override releases immediately in the same axis tick and latches until trigger cycle/rearm; if fullscreen, exit it rather than apply neutral fallback.

One existing native axis boundary/arbiter: manual/native > active hold command > fullscreen neutral fallback. No second axis patch/writer or FilterInputs call. A separate exact throttle-root skip prevents render Update overwrite while hold owns propulsion; seed matched private simulatedThrottle once from current output on valid same-state release, with collective/invert/negative conversion. No private-state write after identity/offline loss.

Conservative outer controller: course error→small bounded bank, sea-altitude→bounded vertical speed/pitch with entry-alpha feedforward and small antiwindup integral, HUD-speed PI with entry-throttle feedforward, angular-rate damping, finite/dt/envelope/slew caps. Fixed physics step only; pause freezes integration. Initial support normal airborne fixed wing and actual forward-nozzle SwivelDuctSystem VTOL only, never force mode. Reject unknown tilt/helicopter, ground/stall/damaged/engine-out/large attitude/invalid telemetry with visible reason. Actual prefab/gain stability remains unverified.

Own bounded Statics|Ships/IgnoreTriggers raycasts plus Datum water, below/ahead/corridor/direct descent samples, cached global heights and strict freshness. Sensor budget/cadence independent of render; no-hit below sea can be water, invalid/unavailable/truncated data never means clear. Terrain demand prioritizes bounded climb/level-bank/speed margin, retains original altitude and returns slowly with hysteresis. OFF removes only avoidance state/commands/sampling, not hold targets/integrals. Insufficient recovery envelope disengages with limitation, no teleport/force writes/avoidance guarantee.

Read private flight-hold contract before implementation. Test-first production pure lifecycle/controller/override/eligibility/terrain/arbiter/handover seams; exact static API/mapping inspection and wiring checks, full suites, both loader builds0warnings/errors, private-free packages and parent independent review. Resolve mouse/Pan-vs-flight mapping ambiguity explicitly. No runtime game/Unity execution or PC action; no tuned/stable/ground-safe claim before user retest.

Review ruling: composite raw flight intent has priority even if Mouse mappings become
ambiguous during fullscreen, and remains live through a valid same-pilot release drain.
Known overlap still refuses entry. With no raw intent, use the safety release boundary.
A runtime-changed overlapping Mouse signal cannot be separated from joystick amplitude;
it may be treated as manual takeover. No source subtraction/controller exclusion.
