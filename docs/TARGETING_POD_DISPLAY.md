# Targeting Pod Display 0.1.0 — guarded experimental candidate

**Compile-verified, not tested in Nuclear Option.** Separate `TargetingPodDisplay.dll`; no dependency on Observer and no Observer changes. Original green legends, border, gapped crosshair and missile/bomb page styling; no copied screenshot or real-aircraft functionality claim.

## What this candidate does
- Reuses native target-camera imagery (including native COLOR / **IR (GAME)** and vanilla telemetry), with original weapon-sensitive presentation.
- Provides an independent **COLOR** viewing camera: slew, FOV zoom, recenter and **inertial line-of-sight direction hold**. This is not ground tracking, a seeker or a weapon designation. Moving the camera does not change selected targets.
- Uses one presentation texture for cockpit and enlarged fullscreen view. Fullscreen toggling does not reset camera state or create another sensor camera. A perimeter remains visible; the overlay can obscure parts of the native HUD, so this is not a flight-information replacement.
- Replaces only a runtime-proven unique `RawImage` beneath the actual cockpit target display. Separate landing consumers may share the unchanged native source. Unsupported/ambiguous surfaces retain their native display. Renderer-material surfaces are not supported.
- Reads native weapon flags/name. Missile page has long split crosshair; bomb/other page has short crosshair. These crosshairs are viewing references, **not impact or lock cues**. A fresh visible native missile hint can appear as `WPN HINT`; `SHOOT` remains native envelope advice, never a lock claim. Missing information says N/A.
- Shows native-selected-target range/grid/name and, only under native single-air-target accuracy conditions, heading/altitude/speed/relative data. Manual camera FOV is labeled separately. Unsupported characters use `?` in the original tiny bitmap alphabet.

## Important limits / requirements not fulfilled
- **True thermal unavailable.** Native IR is not proven heat-signature rendering. Manual sensor does not reproduce native IR. Independent night enhancement is unavailable.
- No captured mouse/wheel control, ground stabilization, independent laser, custom target designation, prelaunch seeker lock state, bomb impact/release calculations or automatic aircraft hold.
- Native target controls remain the way to select/change game targets. Sensor motion never selects one. Native weapon acquisition, firing and safety logic are untouched.
- A native target camera and initialized native output texture must exist. An aircraft that has not initialized that texture may require ordinary native target selection first. The mod never calls native target setup with an empty selection.
- Cockpit texture composition, font orientation, URP single-camera support and each aircraft prefab need runtime validation. Native-feed composition is made after the native frame and may appear one frame later on the cockpit screen. Manual rendering adds one 1024-square world render per frame; performance has not been measured.

## Installation and activation
1. Use the supported official BepInEx 5.4.23.5 or 5.4.23.6 setup already used for Observer. Keep all game/reference DLLs out of the plugin directory.
2. Place only `TargetingPodDisplay.dll` in `BepInEx/plugins/TargetingPodDisplay/`. Launch once to generate `BepInEx/config/local.nuclearoption.targetingpoddisplay.cfg`.
3. Set `General.Enabled = true`. Optionally set `ReplaceCockpit = true` for the guarded replacement. Both default false.
4. Fullscreen and ManualSensor keys default **None**. Choose distinct unused F1–F6/F12 keys; F7–F11 remain reserved for Observer. No key is universally conflict-free.
5. For manual viewing, review native and other-mod bindings, then set `Keys.ConfirmNoOtherModUsesSensorKeys = true`. Native keyboard maps for all Rewired players are checked automatically every frame. A known collision refuses manual input instead of changing bindings. Other mods' raw Unity-key handlers cannot be proved absent; the explicit confirmation is not an automatic safety proof.
6. Default dedicated sensor controls: arrows slew; PageUp narrow FOV; PageDown widen FOV; Home recenter; End toggle inertial direction hold. The eight assignments can be rebound in `SensorKeys` to letters other than A/C/F/H/V/Y, or navigation keys (arrows/PageUp/PageDown/Home/End); each must be distinct and native-unmapped. Keypad keys and those six letters are conservatively excluded because native raw-key handlers exist. Release them before entering manual mode. Recenter affects only the mod camera.

**AIRCRAFT CONTROLS REMAIN LIVE. This is not input capture or autopilot.** No flight axes, controls, safety flags or input maps are changed. Do not expect the aircraft to hold itself while looking away. Pause/map/chat/focus loss, external view, local-owner/scene change, landing mode or contract failure exits the mod view. Reentry is explicit. Conflicts preserve native inputs and are logged.

## Observer coexistence
Both DLLs may be installed. Passive native presentation has no dependency. Manual/enlarged viewing recognizes only the exact delivered Observer 0.5.1 binary; unknown/failed inspection refuses those views rather than assuming idle. Active Observer fullscreen, release-drain or settings modal takes priority. Its Flight Hold may coexist because this mod neither writes native flight controls nor uses native mapped control inputs. This does not validate or guarantee Observer's hold behavior. Other unrelated plugins are not blanket-blocked.

## Building / tests
Private reference DLLs are supplied separately and must never be committed or redistributed.

```sh
dotnet build src/TargetingPodDisplay -c Release -p:GameRefs=/absolute/private/refs -p:LoaderRefs=/absolute/BepInEx/core
dotnet run --project tests/TargetingPodDisplay.Tests
dotnet run --project tests/Observer.Tests
python3 -m unittest discover -s tests -p 'test_*.py'
```

Only pure test code executes. The game/Unity assemblies are compiler references, never test execution inputs. See `TARGETING_POD_SMOKE_TEST.md` before describing any runtime capability as validated.

Configuration changes invalidate the active viewing session, including switching a toggle to None, confirmation or enable/replacement flags. Old and new keys must be released across a frame before explicit reentry.
