# Runtime presentation correction and requested camera modes

The reported 0.1.1 image exposed two defects: overlay text was sized from a small native texture and tint was not reliably applied; copying the final native texture also retained native labels. Native cockpit components additionally live under registered physics parts that can be normally unparented.

## Approved outcomes
1. Resolve only the current aircraft's registered, attached presentation components, including intact unparented parts. Revalidate membership continuously; preserve native display when proof fails.
2. Render a clean owned scene camera following the native sensor pose, or the user's manual pose. Never clone native UI/camera stacks or mutate target/weapon selection. Copy only read-only volume settings; IR appearance requires runtime comparison and is not true thermal.
3. Use a reversible direct-to-screen camera lease for fullscreen, disabling only captured native scene/cockpit camera render components. Keep simulation and camera controller running. Restore on exit, context change, UI/focus loss, fault and shutdown. Screen-space native overlays may require separate runtime investigation.
4. Use one offscreen owned feed for cockpit and configurable passive right-side PiP outside fullscreen. Do not run the offscreen scene pass during fullscreen. New overlay raster uses baked colors and resolution-independent geometry; layout is awaiting user feedback.
5. On a verified native kill notification for the uniquely watched target, retain a live view of its last known global position for a configurable 0–10 seconds (default 3). Clear stale labels immediately. Never infer destruction from deselection/despawn. Cancel on new designation, explicit manual control or lost context.

## Validation phases
- Pure mutable-registry, render-lease restoration/ownership, output geometry and aftermath ordering tests.
- Static integration checks for exact context exception, camera-only suppression, disposal/fault paths and no native target writes.
- Compile against matched private references, without executing game assemblies. Run complete existing tests including Observer regressions.
- Render the production overlay raster to inspected previews, then independent code review before packaging.
- Runtime checklist explicitly covers direct-screen resolution, native camera restore, cockpit/PiP clean feed, IR appearance, kill timing, scene/origin shifts and no stale target labels. Automated tests do not claim runtime validation.
