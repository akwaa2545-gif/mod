# Targeting Pod Display 0.1.2 — experimental runtime correction

**Compile/test verified; the new camera routes have not been run in Nuclear Option here.** Separate DLL; no Observer dependency or changes. Requires the matched game 0.34.6 references and official supported BepInEx 5.4.23.5/5.4.23.6. Offline local cockpit only.

## Changes
- Fixes cockpit discovery when intact physics parts are normally unparented. Only current-aircraft registered, attached parts and a unique initialized cockpit presentation qualify. Ambiguous, damaged or unsupported surfaces remain native. Landing displays and shared materials are not modified.
- Clean owned camera follows native targeting-camera pose/FOV without copying its UI stack. Compact baked-green original symbology replaces oversized white labels. Aircraft attitude uses the aircraft cockpit frame, the clock is explicitly UTC, and RNG is the native selected-target distance, not a reticle ground scale or manual-camera measurement.
- Fullscreen now enables an owned camera rendering directly to the screen at the screen resolution. It temporarily disables only the captured native scene/cockpit Camera render components. Camera controllers and simulation continue. Exit/fault/context loss restore native rendering. Native screen-space overlay HUDs can still appear independently; this is not a claim that every HUD canvas is hidden.
- Optional passive right-center PiP shares the one offscreen camera/presentation output with the cockpit. During direct fullscreen, the offscreen pass and cockpit/PiP presentation are suspended. The camera pose is retained across transitions; output aspect changes to the screen.
- Native kill notification for the uniquely watched target can trigger a live view of its last known native tracking position for 3 seconds. Duration is configurable from 0 to 10 seconds. No frozen screenshot, new designation or weapon command is used. Target/weapon hints clear and AFTERMATH is shown. Ordinary deselection, despawn and cleanup do not prove a kill. New target/manual control/context loss cancels it.
- Manual COLOR camera provides dedicated-key slew, zoom, recenter and inertial direction hold. It does not designate targets, stabilize a ground point or control the aircraft.

## Honest limits
- **Not autopilot. Aircraft controls and firing remain live.** No flight-axis writes or input-map changes.
- **No true thermal or independent night mode.** Native-follow IR copies read-only native volume settings and scoped fog behavior; visual equivalence needs runtime checking. It is labeled IR (GAME), never heat-signature thermal. Manual camera remains COLOR.
- Selected target is not seeker lock. No invented acquisition, laser-active state, launch envelope or impact cue. Laser status is LSR N/A. Fresh native weapon hints retain their native meaning.
- The requested green long split crosshair and three lower ticks appear only for a live native selected target with a conservative surface-missile role. The label is TARGET SELECTED, not seeker lock. Clearing selection or entering aftermath removes it. The surface score must be positive and exceed air/missile scores; this is a presentation-role inference, not an AGM enum. Ambiguous roles use ordinary TGP presentation. Weapon names remain native; the ticks are decorative viewing references, not a launch/ground-range scale.
- Native targeting camera/output must already be initialized, possibly through ordinary native target selection first. No unsupported aircraft setup or expanded target search is performed.
- Native-follow adds a scene render up to 1024 pixels on its long edge, preserving native texture aspect outside fullscreen. Fullscreen replaces main/cockpit geometry rendering but the native target-camera pass may continue. Performance and exact postprocessing/renderer feature equivalence are unmeasured.
- The original compact alphabet supports limited characters. The raster preview tests verify geometry/colors, not actual Unity blending.

## Install or update, preserving your config
1. Close the game. Replace only `BepInEx/plugins/TargetingPodDisplay/TargetingPodDisplay.dll`. Do not install reference DLLs or another Observer copy.
2. Keep your existing `BepInEx/config/local.nuclearoption.targetingpoddisplay.cfg`. Missing new settings are added when the plugin loads. The supplied config is an example, not a required replacement.
3. Set `[General] Enabled = true`; use `ReplaceCockpit = true` if desired. Defaults remain opt-in false.
4. `[Keys] Fullscreen` and `ManualSensor` accept **None, F1–F6 or F12**. H is unsupported. Choose distinct unused keys; for example F6/F5 only if free in your setup. F7–F11 remain reserved for Observer.
5. Manual entry also requires `ConfirmNoOtherModUsesSensorKeys = true` after checking your other mods. Rewired keyboard-map collisions are checked automatically; other mods' raw key handlers cannot be proved absent.
6. Default manual keys: arrows slew, PageUp/PageDown zoom, Home recenter, End inertial direction hold. All eight can be rebound to permitted native-unmapped distinct keys. A/C/F/H/V/Y and keypad keys are excluded because of native raw handlers.
7. `[Presentation] PiPEnabled = true` enables the passive right-side view. `AftermathSeconds = 3` is the default; 0 disables aftermath.

Configuration changes to keys/confirmation/general activation invalidate the session; release both old and new keys before reentry. Pause/map/chat/focus loss, external view, owner/scene change and landing mode release the view. Reentry is explicit. Observer 0.5.1 idle/hold can coexist; its fullscreen/drain/modal takes priority. Unknown Observer identity blocks interactive pod views, not unrelated plugins generally.

## Build and checks
Private references are compiler inputs only, never executed, committed or redistributed.

```sh
dotnet build src/TargetingPodDisplay -c Release -p:GameRefs=/absolute/private/refs -p:LoaderRefs=/absolute/BepInEx/core
dotnet run --project tests/TargetingPodDisplay.Tests
dotnet run --project tests/Aftermath.Tests
dotnet run --project tests/overlay-raster
dotnet run --project tests/Observer.Tests
python3 -m unittest discover -s tests -p 'test_*.py'
```

Read `TARGETING_POD_SMOKE_TEST.md` before describing a runtime feature as validated.
