# Targeting Pod Display 0.1.1 runtime correction

Fixes the reported `MissingFieldException: AeroPart.tacScreen` shutdown. The aircraft's physical cockpit damage part is not the cockpit display component. The plugin now resolves one initialized Cockpit presentation component under the local aircraft and verifies its aircraft owner before reading its tactical screen. All existing target/landing ancestry, unique texture consumer and restore-ownership checks remain enforced.

If a supported cockpit presentation cannot be proven, the native cockpit remains untouched and standalone fullscreen can still use the valid native camera output. The fix does not claim every aircraft layout is supported or in-game validated.

The separately reported `Fullscreen = H` setting is unsupported. Version 0.1.1 names the exact offending setting/value in the log. Select an unused F1-F6 or F12 for Fullscreen, for example F6 only if native and other-mod bindings leave it free. `ManualSensor = None` disables manual entry; use a distinct permitted unused key only when manual controls are wanted. No existing config is silently changed.

Replace the old TargetingPodDisplay.dll, remove duplicate copies, and restart the game to clear the previous fatal latch. Keep your config and edit the invalid key. A valid native camera output must still be initialized, and flight controls remain live. Manual camera movement never designates a target. True thermal/night are still unavailable.
