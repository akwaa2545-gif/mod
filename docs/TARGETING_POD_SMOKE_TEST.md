# 0.1.3 runtime acceptance checklist — not yet run

Use a backed-up offline mission. Test this DLL alone, then Observer 0.5.1, then the existing mod set. Capture logs and screenshots. Compilation and pure tests do not replace these checks.

- [ ] Revisit the reported helipad in COLOR and IR. Confirm no localized magenta surfaces; compare to vanilla. Capture the new Sensor render parity log if any remain. Compilation does not prove this fixed.
- [ ] Fullscreen hides native flight/radar/top-right panels while fresh own weapon advice remains. Fresh OUT OF ARC/RANGE transitions remain truthful; no stale or intentionally hidden SHOOT is invented. External Canvas ownership change exits without reacquiring. Menus/chat/expanded map stay usable.
- [ ] With FullscreenWheelZoom=true, entry waits for a neutral verified boundary; the first next notch zooms exactly once without native camera/weapon/flight response. Test split wheel-as-button mappings, XY/buttons and alternate keyboard/joystick bindings, exit while scrolling, fast reentry, remaps and focus/map loss.
- [ ] Unsupported digital-smoothed wheel bindings refuse entry precisely. Explicit FullscreenWheelZoom=false permits prior fullscreen without pod scroll consumption; native wheel bindings stay live. Config changes invalidate the session.
- [ ] Check third-party raw-wheel readers separately. Native Rewired isolation does not prove unknown-mod raw-input isolation.
- [ ] Disabled defaults leave camera/input/weapons unchanged. Existing config survives DLL-only update and receives new PiP/aftermath keys.
- [ ] Native camera initialized by ordinary selection; unique cockpit target RawImage found through registered intact parts even after normal physics unparenting. Missing/ambiguous/damaged displays stay native.
- [ ] Separate landing image sharing native texture stays unchanged. Target/landing overlap, registry removal, detachment and another mod's replacement release only owned values.
- [ ] Actual compact green legends, white TGP / selected-AGM green split reticles and transparent background are upright, readable and not doubled with vanilla targeting-camera text. Test square cockpit and wide fullscreen. Range matches native target distance; ARS follows aircraft, not sensor motion; UTC clock and LSR N/A remain honest.
- [ ] F6 (or configured unused key) enters real edge-to-edge direct camera view, without cockpit geometry. World resolution follows screen size. Exit repeatedly restores native camera and cockpit. Check any remaining screen-space HUD and report exact canvas/screenshot, rather than disabling shared UI blindly.
- [ ] Pause/map/chat/menu/focus loss, external view, death/ejection, damaged cockpit, origin shift, respawn and scene unload cannot leave a black screen or resurrect the cockpit in another camera state.
- [ ] Passive right-side PiP enables/disables by config and shares the cockpit feed. It intercepts no mouse/input. PiP/cockpit offscreen pass stops during fullscreen and resumes on exit.
- [ ] Manual entry requires distinct unused toggle/sensor keys and explicit other-mod key confirmation. A native binding collision refuses manual controls without disabling native input. Changing any key or confirmation while active releases and requires key-up before reentry.
- [ ] Slew/zoom/recenter/inertial direction hold work through origin shifts. Manual motion never changes native target selection or weapon acquisition. Aircraft steering/throttle/firing remain live; no autopilot assumed.
- [ ] A uniquely watched target's actual native kill report starts live AFTERMATH for configured seconds; moving smoke/debris/world proves it is live. Target name/lock/weapon hint disappears immediately. No destroyed Unity target object is retained by aftermath.
- [ ] Ordinary deselection, target cleanup/despawn, another unit's kill, multiple selections and replacement IDs do not start aftermath. Same-frame selection removal before kill report is handled without a frozen image. New designation, manual entry and context loss cancel; duration 0 disables, 10 caps.
- [ ] Native COLOR/IR settings compare with vanilla; manual stays COLOR. No claim of true thermal/night. Switch weapon roles: surface missile with live selected target gets green long arms and three lower ticks; deselection/destruction removes them immediately, ambiguous/air roles stay generic, no fabricated lock/laser/launch values.
- [ ] Observer absent/idle/hold can coexist; fullscreen/drain/modal or unknown identity blocks/releases interactive pod view. Native flight controls remain untouched.
- [ ] Measure frame time, allocation and memory over prolonged use, repeated mode changes and resolution changes. No doubled offscreen world pass during direct fullscreen, orphan cameras or persistent fog changes.

On unsafe behavior disable this DLL and retain vanilla presentation. Report aircraft, plugin/game versions, log and screenshot; do not claim broad aircraft compatibility before testing.
