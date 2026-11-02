# Nuclear Option Observer — one mod menu

**0.5.1 EXPERIMENTAL cloud candidate — NOT YET TESTED IN GAME.** The user confirmed 0.1.1
works in-game and later reported that 0.3.0's missile view tilted on banked launches.
This 0.3.1 pose correction still needs in-game retest; other menu/map/input behavior
is not thereby broadly validated. Cloud work compiled
against the supplied game 0.34.6 / Unity 2022.3 managed references without running
any game/Unity/loader assembly. No PC files or game binaries were changed.

## 0.5.1 pilot-input and shortcut correction

The supplied0.5.0 runtime log confirmed F11 reached a pilot/cockpit guard while the
user was piloting. Static inspection found our guard incorrectly used a legacy
`Pilot.playerControlled` field with no direct reads/writes in the matched native game IL. It now
uses the game's actual **local-player identity** predicate instead, keeping exact
pilot/player-state, aircraft, offline, alive/cockpit and uniqueness checks. This
correction applies consistently to fullscreen, hold and input handover. Remaining
rejections report their specific predicate and bounded candidate-state details in
`BepInEx/LogOutput.log`; no guard is blindly disabled. Runtime confirmation is pending.

Ownership capture/return explicitly centers the native virtual joystick and clears
its retained raw pilot-axis cache, which native map/radial input can otherwise replay.
It does not overwrite filtered aircraft commands, change control gains or retain a
previous steering offset on handback. Map/UI/free-look Pan/Tilt is not flight takeover;
intentional raw flight inputs remain live. Check the hold HUD says **ACTIVE**—blocked
hold cannot stabilize a plane. Direct Mouse flight bindings overlapping view/UI
bindings remain ambiguous; the mod does not subtract or ignore joystick sources.

**Toggle PiP (F9)** and **Next missile (F10)** are clearly labeled, configurable in
Missile Camera and work during unobstructed gameplay or the paused Observer menu.
Next is explicit/manual, wraps the existing live list, and never opens a hidden view.
An explicit Next can retain fullscreen only with the same valid pilot and a live
new target; automatic launches do not gain this manual handoff. Key capture, text
entry, native menus and modifier chords suppress shortcuts. Existing saved key values
are reused, not reset or duplicated.

## 0.3.1 camera correction

The chase camera no longer copies missile roll or rotates its offset with that roll.
It stays behind the missile's forward direction with a fixed world-up offset and
looks back at the selected missile's position, keeping that target centered with a
world-up horizon. Cockpit/main-camera orientation is not used. At straight-up/down
views the horizon is geometrically undefined; a deterministic world-axis fallback
keeps a finite stable pose. Invalid/nonfinite/unrepresentable pose fails closed.
Only the mod camera changes; no missile transform, physics, map or settings changes,
no smoothing or additional camera mode.

## Fullscreen + orbit (Flight Hold OFF by default)

Select a live missile, use normal **cockpit or orbit view following your own aircraft**,
then press **F11** for fullscreen. Hold **RMB** and move the configured native
Pan View/Tilt View input to orbit the missile; **F7** resets orbit. **F11** returns
to PiP; **F9** hides the view. **Esc** exits fullscreen into the normal pause menu,
then **F8** opens settings. All five keys are rebindable in Missile Camera. New key
slots choose unused function keys if your existing custom bindings collide; existing
settings/view/next bindings are preserved. F12 is not a default because Steam commonly
uses it for screenshots. Check native/other-mod bindings; arbitrary conflicts are not
made exclusive by this mod.

Fullscreen is an owned render-texture image, **not a native camera/state switch**.
It defaults to PiP on start. The reusable image is capped at1280x720 and20Hz,
keeps screen aspect with ScaleToFit, and reallocates only when size/mode changes.
Orbit remains centered/world-up, independent of cockpit pose and missile roll.

**Your aircraft keeps simulating. Flight Hold is OFF by default.** Without active
Flight Hold, fullscreen only neutralizes manual axes/brake and holds entry
throttle/custom axis; that is **not autopilot** and can lose altitude or crash.
With eligible experimental Flight Hold active, the same single axis boundary supplies
bounded hold commands instead. Native filtering, assists, GLOC and dynamics continue
once; render/button phases never overwrite filtered outputs.

The input guard supports only ordinary local cockpit/orbit with a live player pilot
and attached cockpit. It refuses entry during any active weapon salvo and releases
an already-held manual countermeasure trigger. It does NOT cancel automatic turrets,
existing shots or every global game action. Exact named camera queries are masked
only for the local Rewired player inside supported camera-update scopes; those camera
methods, native origin/environment updates and physics remain running. Native UI,
Pause/Map/Chat and unrelated players/actions are not globally remapped or disabled.
If complete guards cannot install, fullscreen refuses while PiP remains usable.

Menu/map/camera UI/radial/focus/scene/offline/pilot/target identity loss returns the
image immediately and resets orbit. For manual return or target-only loss while the
same live local pilot/context remains valid, a visible **release held mouse/flight/view buttons**
drain prevents held drag, keyboard countermeasures, release-driven view/weapon buttons
and their release frame leaking into native controls. Absolute flight axes do not
prolong the drain.
There is no arbitrary timeout that releases still-held fire. Scene/offline/pilot/UI
or focus loss cancels the drain immediately; returning focus requires released keys
before a fresh entry. Native input production resumes, not stale steering snapshots.
No cursor flags, input maps, pilot/AI state or native camera transforms are restored
or overwritten. Fullscreen input timing/scope/teardown still needs actual game retest.

## Experimental Flight Hold / optional terrain avoidance

In the same paused **F8 mod menu**, open **Flight Hold** and opt in with **Enable**.
No speed or height entry: opening the maximized map, supported own-aircraft external
view (orbit/chase/TV), or missile fullscreen captures the current **ground course,
HUD speed in m/s and sea altitude** once. Overlapping views do not recapture them.
Closing all triggering views returns manual control. The on-screen status distinguishes
waiting/blocked/ACTIVE and terrain ON/OFF/CLIMB; captured targets are read-only.

**Terrain avoidance** is a separate saved ON/OFF toggle, default ON. OFF leaves
ordinary course/speed/altitude hold active, but removes terrain queries and avoidance
climb. ON uses bounded below/ahead/corridor/descent rays plus water, and prioritizes
bounded climb/level-bank when predicted clearance is low. It retains the original
altitude target. Invalid/stale sensing or demand beyond its recovery envelope releases
hold with a reason. Sparse sensors and untuned commands cannot guarantee avoiding ground.

Manual pitch/roll/yaw, throttle movement, vector/custom-axis movement, brake, gear,
engine/eject/assist controls cancel hold immediately, latching it off until all trigger
views close/reopen or **Rearm** is clicked. In fullscreen a flight override also returns
the image and native flight input in the same axis tick, rather than neutral fallback.
Normal fullscreen mouse/button release-drain remains for nonmanual exits. Hold alone
never owns weapons/countermeasures or switches native pilot/AI states. No combat AI,
pathfinding, forced transforms or flight-mode changes.

Initial conservative eligibility: healthy engine-running forward-flight fixed wing,
gear locked retracted, above roughly **300 m ground/sea clearance** on entry, sufficient
forward airspeed (at least 1.35× the larger prefab takeoff/landing speed, minimum45 m/s),
modest attitude/rates. Actual SwivelDuctSystem VTOL can qualify only with its measured
nozzle within5° of forward and forward demand; no VT-7 prefab support is promised.
Helicopter/unknown tilt/hover/stall/damaged/low-clearance states are rejected. Pause
freezes integration; focus/identity/offline/scene loss releases ownership.

Combined hold/fullscreen requires provably disjoint enabled Mouse Pan/Tilt and
Pitch/Roll/Yaw bindings. Conflicts or unverifiable mappings show a reason; remap them
or disable Flight Hold to use ordinary fullscreen. RMB/native Free Look Pan/Tilt is
not virtual-joystick flight intent; actual joystick/keyboard override is never ignored.
A conflict discovered during RMB use with no raw flight intent releases hold/fullscreen
with the normal safe release boundary. Intentional/composite raw flight input always
wins, including during that drain: it returns native flight control immediately. If
bindings change mid-fullscreen, overlapping Mouse and joystick sources cannot be
separated; the combined raw signal is treated as manual override, not subtracted. No input maps are changed. Raw-axis override deadband is optionally
available in the config, not a flight-target/gain UI.

**EXPERIMENTAL / NOT GAME TESTED.** Start high, level, fast enough and over open water;
be ready to take over. No tuning/stability/crash protection is established by cloud
unit tests. Keep a backup of the previous DLL. Flight Hold can be disabled separately
without changing the missile camera features.

## Quick use

1. Close the game. Back up and replace **only** the previous
   `Observer.dll` **wherever it is currently installed** (your current path is
   `BepInEx/plugins/Observer.dll`). No relocation is needed. Keep the existing loader,
   other mods and config. Do not install a second Observer copy.
2. Use a native **offline single-player** mission. New installs remain disabled;
   intentionally set `[PiP] Enabled = true` in
   `BepInEx/config/io.github.nuclearoption.observer.cfg`. Existing true is preserved.
   **The view always starts hidden with no selection. Auto-show is OFF by default.**
3. Press **Esc** to open the native pause menu, release mouse buttons, then **F8**
   for Observer. If an on-screen safety explanation appears, follow it first.
4. In the **Missile Camera** section, choose a live missile, then **Show view** (or
   **F9**). List selection and visibility are separate; list buttons never auto-open.
5. Close the Observer panel, then press **Esc** to resume. Tracking continues.
   **F9** toggles the display during unobstructed gameplay. Opening/closing the
   native pause menu never forces the view on. **F10** during safe gameplay or while the Observer panel is
   open manually selects the next missile. **Previous missile / Next missile**
   buttons wrap through the same stable all-live list; neither opens the view.

**F8 settings / F9 Toggle PiP / F10 Next missile** are distinct, rebindable **F1–F12** keys.
Click a key button while paused, then press an unused function key with no
modifiers. Duplicates, mouse/flight/confirm keys and modifier chords are rejected.
Key capture and native text entry suppress shortcuts. Esc remains the game's pause key;
it may resume the native game and therefore closes Observer automatically.
Native/other-mod custom key conflicts cannot be certified statically: check those
bindings first and rebind Observer to unused keys. Observer does not intercept or
rewrite the game's keyboard/Rewired mappings.

## Map watch and optional own-launch auto-show

- Open the **native maximized map**, then hold **Ctrl and left-click a visible native
  missile icon**. This explicit Watch gesture selects **and shows** PiP, independently
  of the auto-show setting. Do not add Shift/Alt/Command or hold other mouse buttons.
- Only the actual top raycast native icon qualifies. No nearest-unit guessing or
  clicking through an overlaid window; no hidden/enemy icons are manufactured.
  The visible Observer PiP image/status area is also excluded from map Watch,
  using the same current clamped/scaled geometry as its rendering. The Flight Hold status rectangle is also excluded from map Watch.
  Native already-selected/highlighted icons can be non-raycastable, and some missiles
  have no native icon. Use the all-live list / Previous / Next in those cases.
- Ordinary left-click, Shift selection and right-click orders remain native. Only
  the captured Ctrl+left gesture's paired native Mouse/Controller selection routes
  are consumed, including release after Ctrl is let go. A stale capture is a consumed
  no-op, never a native targeting fallback. Native map panning is not rewritten.
- Map watch requires offline/readiness, native map-only cursor state, focus, no text
  entry/menu/Observer modal, and native menu weapon safety (or already-disabled
  native flight controls). Observer never changes those settings. A map hook failure
  disables map watch until restart; the list remains available.
- In **Missile Camera**, **Auto-show NEW own-aircraft launches** defaults **OFF**.
  When deliberately enabled, a fresh actual local-aircraft missile registration
  selects and shows that new own missile after deferred owner/ID/epoch validation.
  It can therefore replace a manually watched missile when you fire a new own one.
  Enabling does not pick existing missiles; other owners' launches never auto-switch.
  Turning it OFF clears pending automatic events but preserves current tracking.
  The setting persists as `[MissileCamera] AutoShowOwnLaunch = false/true`.

## Panel and input safety

The interactive panel is **paused-only**, requiring the real native pause-menu
Canvas, visible unlocked native cursor, focused application and active EventSystem.
It uses its own fullscreen top-sorted uGUI raycast shield, checks the actual top
raycast at setup/current pointer, and moves native submit/navigation focus only
to its own handler-free shield. This blocks clicks on underlying native uGUI
menus; IMGUI alone is not treated as protection. Opening with held mouse buttons
is refused to avoid carrying a native pointer press into the modal session.

Static inspection found that pausing alone does **not** necessarily prevent Fire:
the native `PilotPlayerState` uses `PlayerSettings.menuWeaponSafety` and cursor
visibility (or already-disabled native flight controls). Observer refuses the panel
unless that native weapon-safety condition is true and shows an explanation. It
**does not change that user setting**, flight controls, pilot state, cursor flags,
input maps or game time. `menuWeaponSafety` is the internal setting identifier;
the precise localized settings-menu label has not been runtime verified.

The panel closes if the native pause/menu/cursor/focus/readiness context disappears
or the raycast shield loses priority. It never resumes the game. Close immediately
disables and destroys only its own shield, clears only its own UI selection and
does not restore an old native selection or overwrite cursor flags. During flight
PiP is display-only, **not a mouse-input shield**. Do not click it expecting a button.

Move/resize the PiP with **Left / Top / Width / Scale** controls in the protected
panel. Bounds are clamped on every draw for current resolution/scale. Settings save
when the panel closes (including native resume), and reload from disk about once a
second. Width is a base width before scale; the render texture remains 480×270.
A low-resolution/small view may constrain displayed labels. The panel is designed
for normal desktop game resolutions, not tiny windows.

The menu is the home for this mod; Missile Camera and experimental Flight Hold are implemented.
No pretend/active freecam or unit-control features are added.

## What can be watched

The **Missile Camera** selector in the single main mod menu refreshes the real **`UnitRegistry.allUnits` every 0.5 seconds** on the
main thread, selecting only active valid registered `Missile` components in the
mission scene. It includes player, friendly, opposing and unknown-owner missiles;
with Auto-show OFF, no firing event can select, switch or show the view. There are no extra search,
filter or sorting features in this deliberately small first section.
The stable `#unit/lifetime` label also shows type, owner/type + owner ID and distance
from the local aircraft, sampled at refresh. It is an observer list, not a targeting
or authority API. Bullets or custom projectiles without real `Missile` components
are not supported merely because their visual appearance resembles a missile.

Destroyed/despawned/disabled/re-enabled missiles, changed IDs, scene/player/aircraft
changes or guard loss end tracking. There is **no automatic fallback or rebind**.
A pooled object needs explicit selection of its new lifetime. Stale list clicks
are revalidated and rejected. Discovery does not scan Unity objects per frame.

The existing observation-only OnEnable epoch hook and compatibility/offline guard
remain. One standalone disabled URP Base camera (no AudioListener), reusable
480×270 texture and requests capped at **20 Hz**, no catch-up bursts. Primary
camera, transforms of native objects, weapon guidance and networking are untouched.
No freecam, RTS, possession, retargeting or multiplayer support.

## Compatibility / disable / remove

Supported exact official existing **BepInEx 5.4.23.5 or 5.4.23.6**, bundled HarmonyX
2.9.0.0, and the uploaded original Managed files. No loader DLL is included or
needs updating. Original disk hashes remain strict; only the normal proven
BepInEx CoreModule live rewrite has the existing scoped exception. The added
UI/input DLL originals and live MVIDs are also pinned. Flight Hold additionally pins the supplied PhysicsModule; its failure blocks hold, not existing PiP. A mismatch fails closed;
do not bypass it. `PiP inactive:` readiness messages are capped at one per 10 s.
Render/allocation/lifecycle/modal exceptions disable until restart.

Set `[FlightHold] Enabled = false` to disable hold only. Set `[PiP] Enabled = false` to stop all Observer features and release resources. Exit the game
before removing/replacing files. To uninstall, remove only the installed
`Observer.dll` and optionally its own config; remove a plugin subfolder only if it
was created solely for Observer and contains nothing else. Leave shared loader, game files, other mods,
saves and logs intact. Check an ordinary offline mission afterwards. Deleting a DLL
while running is not unloading.

## Reproduce cloud checks

SDK **10.0.403**, private uploaded references and official loader references are
required; these are not distributed with the source. No assembly under test loads
or executes game/Unity/loader/Harmony code.

```bash
source /workspace/shared/nuclear-option-tools/activate-dotnet.sh
python3 -m unittest discover -s tests -v
dotnet run --project tests/Observer.Tests/Observer.Tests.csproj -c Release
dotnet build src/Observer/Observer.csproj -c Release \
  -p:GameRefs=/workspace/shared/nuclear-option-mod/private/refs \
  -p:LoaderRefs=/workspace/shared/nuclear-option-tools/bepinex-5.4.23.5/core
```

Use `dotnet run`, not `dotnet test`: the pure C# project is an executable assertion
harness, with production-used selection/settings/gate/resource-lifecycle helpers.
Python wiring assertions are static safety checks, **not runtime UI tests**.
See [smoke checklist](docs/testing/phase1-smoke.md) and
[results](docs/testing/phase1-results.md). Only our DLL, README, example config and
two testing docs may be packaged. Never include private/decompiled/game/loader/UI
reference DLLs, PDB, deps JSON, logs or manifests.
