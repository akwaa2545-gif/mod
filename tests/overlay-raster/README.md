# Pure overlay raster checks

Run from this directory after sourcing the available .NET environment:

`dotnet run --project OverlayRaster.Tests.csproj`

No Unity/game assemblies are loaded. Tests link only OverlayRaster.cs. The executable emits PPM previews from actual returned RGBA pixels composited over neutral gray. PNG derivatives were made with Pillow for inspection. Preview inputs are illustrative fixtures, not game telemetry or runtime captures. Do not ship previews as game assets.

Tests cover baked green/white pixels, transparent center gap, selected/unselected/non-AGM reticle gates, all three tick widths and the transparent center gap, clipping of 500-character names, central-label exclusion for baseline layout, invalid size/missing-data handling, explicit unavailable laser status, independent pitch/roll changes, fixed aircraft marker and invalid attitude omission. Small PiP tests require full laser status and clock seconds to remain visible.

OverlayData fields are caller-supplied display truth. Caller must label range RNG with units, clock SIM/UTC according to its source, and supply pitch positive nose-up / roll positive right-bank. RangeNearReticle introduces the specifically requested range-label exception to the otherwise clear center. Tracking brackets must be enabled only for proven camera tracking. Missile is the caller-confirmed AGM role; only Missile && TargetSelected selects the green long split reticle with three lower ticks increasing in width downward. Missing/cleared selection and non-AGM weapons use the ordinary white TGP reticle. TargetSelected is native selection, not a seeker-lock claim. There is no fabricated envelope, yardstick, or lock scale.

RGBA rows are TOP-DOWN. Unity upload must flip rows or adjust UV exactly once. Transparent output requires alpha blending over a clean sensor source; baking RGBA colors alone does not fix incorrect renderer blending. Cache by all fields and dimensions (including attitude/time); avoid allocating raster every frame when unchanged. Clock/pitch/roll should be sensibly quantized by caller if cache churn is costly. Rendering accepts 128..4096 pixels per axis.

Geometry and status placement remain adjustable pending user preview feedback. No camera, PiP, fullscreen, input or Unity ownership code is included here.

## 0.1.3 reticle-only visibility change
The central crosshair, native-tracking corner brackets, and selected-AGM ticks use an independent stroke of max(2, round(shortAxis*0.004)) pixels. TGP arm outer radius is 0.080*shortAxis; selected AGM is 0.320*shortAxis. Center gaps stay 0.018/0.020*shortAxis. Three AGM tick half-widths are 0.024/0.040/0.056*shortAxis, at unchanged offsets 0.14/0.19/0.24*shortAxis below center. Font, text positions, border stroke, attitude cue, selection gating and colors are unchanged. Pixel tests measure both arm and tick thickness and extended endpoints across six resolutions. Generated PNG previews are from this production raster, not separate mock geometry.
