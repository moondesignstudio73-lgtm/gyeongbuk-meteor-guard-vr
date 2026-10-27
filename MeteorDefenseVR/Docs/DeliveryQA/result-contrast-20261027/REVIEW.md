# Result contrast — 2026-10-27

Result-only secondary text and idle/focused button outlines are brighter. Font size, geometry, wording, result retention, and activation behavior are unchanged. Other menu buttons retain their previous default outline colors.

## Evidence

- Accepted ResultChoice ordinary frame 0116: JPEG luminance ROI proxy difficulty 4.86, button label 9.19, border 1.69. Public-build 800px fixture: difficulty 4.43, label 8.89, highlighted label 6.16. These are diagnostic sampled-pixel ratios, not accessibility certification.
- Matched D3D11 Editor renders cover 800x600 and 1280x720, scene and white-background stress conditions, normal/gaze/disabled states. Before reconstructs old colors in the same current renderer; it is not a separate old executable. 24 PNGs retained.
- Matched scene-normal 800px: difficulty proxy 4.513 → 6.676; outline 1.336 → 5.918; label unchanged 8.663. At 1280px: difficulty 4.984 → 7.308; outline 1.336 → 5.918; label unchanged 9.272. Exact ROIs and all states are in contrast-proxy.json.
- Visual inspection: 800px bright-background normal, gaze, and disabled images and 1280px scene-normal image show intact text and more visible outlines. Gaze has a cyan fill; disabled retains a yellow side marker. Bright background does not bleed through the opaque result panel.
- Editor final run: 12/12 passed, including result contrast and reset-controller tests. The new fixture asserts nontruncated text and visible glyphs; render-notes minimum glyph heights include punctuation and must not be interpreted as Korean letter heights.

## Excluded / limited evidence

Three initial Editor fixture attempts failed during setup. Moving verification into a coroutine entered after EnterPlayMode, matching existing tests, resolved the test lifecycle problem. They do not represent passing product checks.

An initial 800px public-build fixture was interrupted by foreground focus changes and stalled before combat assertions. A clean second attempt passed; preservation reports confirm original records and non-session preferences restored. Neither scripted fixture constitutes OS gameplay or HMD validation. Raw logs and private data backups remain local and untracked.

New executable regression and publication evidence will be added after packaging. Suno/listening and HMD verification remain pending.
