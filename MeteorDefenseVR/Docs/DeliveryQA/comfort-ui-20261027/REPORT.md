# Comfort UI review — 2026-10-27

## Classification

| Screen | Status in this bundle | Evidence / limits |
|---|---|---|
| Pause | Redesigned | Dedicated opaque panel and title; original resume control retained; hidden during settings/combat |
| Look settings | Redesigned | Shared framed surface, stronger heading, separate row surfaces; unchanged body/control sizes |
| Audio settings | Redesigned | Same heading/surface hierarchy; unchanged channel controls, no new audio |
| Start / tutorial / result | Regression only | Existing layout and behavior retained |
| Loading | Incomplete | Existing original 1280 loading capture reviewed; tip/progress are small and blue against cockpit. No loading change or current-build loading acceptance claimed |
| Boss warning | Incomplete | Existing gallery boss state captures do not contain active boss-warning text. Full warning/phase/simultaneous-notice review still required; state-only capture is not boss acceptance |
| HMD / human OS gameplay / listening / Suno | Incomplete | Separate verification/approval still pending |

The three changed screens appear only outside active combat. There is no additional gameplay occlusion. Font sizes of body copy and buttons are preserved; settings titles increase from 52 to 56 and pause gets a separate 64-point heading. Baseline source is 8d610a8; only the two PcTestOverlay files are replaced temporarily for Before, while the same expanded QA fixture is used on both versions.

## Coverage

Five sizes: 800×600, 1024×768, 1280×720, 1920×1080, 2560×1080. Per size: ordinary settings, sensitivity minimum/maximum, selected toggles, all audio channels at zero/muted and 100%, complete Korean helper text, synthetic pointer changes, audio save/reload under isolated QA preferences, close back to paused state, and resume. Existing result restart/menu gallery remains covered. No new launch-continuity run is required by these changes.

PausePriorityTests supplies simultaneous stage/tutorial/practice/combat/boss presentation suppression, frozen timer and resume regression. ViewSettingsReadabilityTests supplies modal/Escape/reset/gaze regressions and text/target geometry. These are automated fixtures, not a human playthrough. Original user ProjectSettings and player preferences are preserved.

## Originals

Before and Accepted are Editor renders. ExtractedFinal is the new ZIP extracted and run as a normal Windows player. Their exact results and archive identity are recorded alongside this report after verification. Full local galleries are listed in evidence-manifest.json; selected original PNGs and labels are committed for comparison.
Initial comparison attempt was rejected because the synthetic pointer targeted top-left button pivots. The fixture now clicks each RectTransform center through its actual canvas camera; rejected outputs remain local and are excluded from accepted evidence.

| Size | Before pause | After pause | Before look | After look | Before audio | After audio |
|---|---|---|---|---|---|---|
| 800x600 | [Original](Before/gallery/800x600/pause/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/800x600/pause/stability-game-render.png) | [Original](Before/gallery/800x600/look-settings-max/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/800x600/look-settings-max/stability-game-render.png) | [Original](Before/gallery/800x600/audio-settings-muted/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/800x600/audio-settings-muted/stability-game-render.png) |
| 1024x768 | [Original](Before/gallery/1024x768/pause/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1024x768/pause/stability-game-render.png) | [Original](Before/gallery/1024x768/look-settings-max/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1024x768/look-settings-max/stability-game-render.png) | [Original](Before/gallery/1024x768/audio-settings-muted/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1024x768/audio-settings-muted/stability-game-render.png) |
| 1280x720 | [Original](Before/gallery/1280x720/pause/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1280x720/pause/stability-game-render.png) | [Original](Before/gallery/1280x720/look-settings-max/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1280x720/look-settings-max/stability-game-render.png) | [Original](Before/gallery/1280x720/audio-settings-muted/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1280x720/audio-settings-muted/stability-game-render.png) |
| 1920x1080 | [Original](Before/gallery/1920x1080/pause/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1920x1080/pause/stability-game-render.png) | [Original](Before/gallery/1920x1080/look-settings-max/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1920x1080/look-settings-max/stability-game-render.png) | [Original](Before/gallery/1920x1080/audio-settings-muted/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/1920x1080/audio-settings-muted/stability-game-render.png) |
| 2560x1080 | [Original](Before/gallery/2560x1080/pause/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/2560x1080/pause/stability-game-render.png) | [Original](Before/gallery/2560x1080/look-settings-max/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/2560x1080/look-settings-max/stability-game-render.png) | [Original](Before/gallery/2560x1080/audio-settings-muted/stability-game-render.png) | [Original](ExtractedFinal/screens/gallery/2560x1080/audio-settings-muted/stability-game-render.png) |
Visual review of the existing boot settings proof exposed near world-menu text drawing over the settings canvas. PausePresentationGate now also suppresses world presentation while either settings modal is open and restores it on close. New boot-look-settings and boot-audio-settings captures cover that case. An intermediate run was intentionally interrupted for this fix and is excluded.

Product commit: e832848. Before comparison: 1/1 passed (110 images). Accepted: 3/3 passed (120 gallery images plus pause/settings regression). All prior failed or interrupted attempts are excluded from acceptance. New boot settings captures verify the world-menu occlusion fix; the baseline 110-image gallery predates those two additional states.
Build identity: normal (non-development), clean product source e832848b7f1ebc454cbb281010b94e289f0e3105, Unity 6000.5.10f1. User settings exact-byte SHA256 restored: A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89.
Extracted native result: 120 images; exit 0; RTX 3070 / Direct3D12; UTC 2026-10-26T18:12:29Z–18:14:29Z; player preferences and ranking preservation verified. Visual inspection: 800 pause/look/audio-muted, 1024 maximum look, 1280 boot audio, 1920 audio-muted, 2560 pause. Native fixtures use synthetic input and explicit game states; no full natural combat or new departure continuity claim.
