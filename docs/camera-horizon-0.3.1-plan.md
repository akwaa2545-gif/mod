# 0.3.1 scoped roll/centering correction

Root: Render copied missile rotation into both camera offset and rotation; roll therefore rotated the chase offset/horizon, and the camera did not look exactly at the missile.

Plan: regression-test one pure position+forward pose calculator, with a world-up offset (7m behind/1.5m above), target-centered look direction and projected world-up basis. Near vertical, use a deterministic fixed-world-axis fallback; invalid/nonfinite/unrepresentable pose fails closed with a reason. No native/cockpit orientation input, smoothing, modes or UI/map changes. Wire only the standalone mod camera through this math after existing origin-shift timing and render/target guards. Full pure/static tests, both loader-reference compiles, exact sanitized packages and parent independent review before delivery. Game runtime retest remains required.
