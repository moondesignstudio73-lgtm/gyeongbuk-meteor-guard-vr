# Top HUD / world pickup priority checkpoint

Baseline product5bf66577, docsHEADc62df9b. Existing upper800 original shows green world pickup core/ring crossing top stage/count text; instruction packing is a separate accepted fix. Scope this one readability issue. Reproduce same supported distinct CalculateSpawnPosition drops/real max3 and camera angles at800x600/2560x1080, capture entry/overlap/exit before altering product. Determine actual contrast/readability from originals.

If confirmed, minimal top mandatory HUD background/display correction, no font shrink/text omission/item movement/collider/gaze changes or global item hiding. Targeted warning, paused settings, resume acquisition, instruction layout and reset regression; do not rerun all previous18 phases. Then clean Windows package/extracted run/public GitHub verification if product changes. Preserve user ProjectSettings and Suno; no Drive/state/inter-thread writes. Controlled fixture is not natural user/OS/HMD.

Code inspection: top panels ChamferedHudPanel fill alpha0.82, UI camera plane0.45/order220. Pickup instructions plane0.25/order230. First hypothesis is translucent top panel lets green effects reduce glyph contrast; do not change ordering globally without visual evidence.

Baseline original800 overlap confirms the green core/ring crosses stage/count glyphs and reduces contrast. Minimal candidate changes only fill opacity of MainTelemetry panels to1; border, fonts, contents, canvas order/depth and pause handling unchanged. Item positions/colliders/gaze and accepted instruction placement unchanged. Runtime Configure/OnEnable both apply the fill, avoiding scene rebuild.

Editor after.xml Passed1/1. Sixteen targeted frames generated. Original800 andwide overlap inspected: stage/count glyphs now clear with world ring remaining visible outside the panel; no item or instruction movement introduced. Other frame originals retained for comparison; final extracted player review follows.

## Accepted Windows package
Clean product963f2b9c86cef0a1b13f9c608adf9e1fd5a44b84; nondevelopment Unity6000.5.10f1. Original user ProjectSettings preserved exactly. ZIP89,566,599bytes/222 files; SHA25632429e8756d995396fbb3ae826a15b5a3cff0788533ede8ccfcd9ef02c2812db. Extracted to Builds/Verify/PHP20261024, each file hash matched built original. Default Direct3D12 /RTX3070; exit0, soak passed/issues[], zero round errors/warnings. Controlled round result90.924s/reset102.165s, average89.93FPS in this environment. Run2026-10-24T03:54:16Z–03:56:44Z.

All16 ExtractedFinal originals visually reviewed: entry, overlap, exit, warning, paused-settings, partial-gaze, acquired, reset at800x600 and2560x1080. Top glyphs clear; ring remains outside opaque panel. Warning text and pickup instructions remain separately readable; pause/settings hides HUD/instructions, resume restores them; actual collider ray plus elapsed synthetic dwell acquires exactly once and reset removes active items. Partial gaze after resume naturally returns camera toward centre through existing resume behavior; no item position/collider modification. Existing fixed32 instruction font/full text unchanged. Before entry/overlap/exit six originals also inspected. The fixture has three active items and scenario HUD4/4 bookkeeping, not a natural campaign progression. No claim of full continuous sweep or every item arrangement.

Existing delivery failure/return fixture passed; current extracted hangar landing original also inspected (hangar-landing.png). Underlying hangar improvement remains f46dbf9, see hangar-graphics-20261022 for geometry/texture and transition proof. Prior18 multiple-edge phases were not rerun. This audit is controlled rendering/callbacks, not natural human/OS/HMD play; Suno remains pending. No Drive/state/inter-thread updates.

Published release422504141/windows-pickup-hud-priority-20261024 targeting exact productcommit. Public redownload comparison recorded separately.
