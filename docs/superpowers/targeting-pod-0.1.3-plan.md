# 0.1.3 focused runtime corrections

User runtime evidence confirms cockpit replacement and direct fullscreen now render. Remaining observed defects: some helipad geometry is magenta in the owned IR view, unrelated native aircraft HUD remains over fullscreen, and reticle lines are too thin. User additionally requests fullscreen-only mouse-wheel zoom while preserving truthful native firing/envelope advice.

## Boundaries and implementation sequence
1. Compare exact native and owned camera renderer selection, culling masks, rendering hooks and volume setup. Correct a proven mismatch, with bounded runtime diagnostics when serialized scene configuration cannot be inferred statically. Do not invent a shader replacement or mutate shared materials.
2. Identify exact aircraft HUD rendering components. Lease only presentation flags on proven dedicated components, leaving CombatHUD, weapon hints, targeting and camera logic active. Restore on every fullscreen exit, owner/context loss and failure. Unknown/shared UI roots remain untouched and diagnosed.
3. Increase only TGP/AGM reticle line width/length and tick clarity; keep text and other layout dimensions unchanged. Verify production raster pixels at small and large resolutions.
4. Add fullscreen-only owned-camera wheel zoom with finite bounded FOV. Prove native scroll isolation at mapped-input boundaries without disabling steering/fire globally. Refuse unsupported isolation rather than claiming consumption. Keep menus/focus loss and competing ownership as immediate release boundaries.
5. Preserve native weapon advice by reading fresh same-station native hint data, rendering it in the pod overlay. No invented FIRE/lock permission or weapon behavior change.

## Checks
Pure tests for wheel zoom bounds, lease restoration and conflict ownership; structural checks for native render contract and no controller/weapon mutations; existing pod, aftermath, raster, Observer and Python suites; exact-reference compilation without executing game assemblies. Independent review before packaging. Runtime retest must verify helipad materials in COLOR/IR, no remaining aircraft HUD, visible truthful envelope advice, wheel-only isolation and restoration through menu/focus/camera/scene transitions.
