# Runtime validation status

| Version / scope | Status / evidence |
|---|---|
| 0.1.0 startup | User log showed compatibility-gate rejection before PiP activation. |
| 0.1.1 runtime | User explicitly confirmed it works in-game before requesting manual behavior. This is user evidence, not a new cloud runtime test. |
| **0.2.0 manual selector/panel/input shielding/rendering** | **NOT RUN IN GAME. Candidate requires the smoke checklist.** |
| 0.3.0 runtime | User reported banked-launch missile-camera tilt. This does not establish all map/menu/input smoke rows passed. |
| **0.3.1 roll-stabilized centered chase correction** | **NOT RUN IN GAME; targeted camera retest plus outstanding inherited smoke rows required.** |
| **0.4.0 fullscreen/orbit/input capture** | **NOT RUN IN GAME. No autopilot/terrain avoidance.** |
| 0.4.0 pure C# policy/resource tests | **125/125 pass**, production pure helpers only; no game assemblies loaded. |
| 0.4.0 Python static/package tests | **57/57 pass**; structural/allowlist checks, not runtime integration. |
| 0.4.0 compile | Both official 5.4.23.5 and 5.4.23.6 reference builds: **0 warnings / 0 errors**. No game/plugin execution. |

Do not extrapolate 0.1.1's user-confirmed PiP success to 0.2.0/0.3.0's new UI/input behavior.

## 0.5.0 experimental hold candidate

- **NOT RUN IN GAME.** Controller gains, aircraft eligibility, override/handover, terrain
  coverage and avoidance effectiveness are not validated by cloud tests. No crash guarantee.
- **164/164** pure C# assertions pass (production lifecycle, arbiter, manual override,
  throttle conversion, controller/envelope and terrain policy plus inherited checks).
- **64/64** Python structural/package checks pass, not native execution evidence.
- Both loader-reference builds must report **0 warnings / 0 errors** before packaging.
- Flight Hold master defaults OFF; TerrainAvoidance is an independent saved default-ON
  option, only effective while hold is active. No target speed/altitude entry.
