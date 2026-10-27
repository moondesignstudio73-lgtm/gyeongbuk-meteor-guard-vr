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

## Packaged executable regression

- Product commit ae2cd406ecb809db2a6952dd088dd24c4011bb18, clean nondevelopment Windows build. ZIP 89,614,129 bytes, 222 archived files individually matched to build output. SHA256: 1fb0ba4c4dd1290094d5e1a54bd2a4d54844704a7c9014db4424b0f6910bb009.
- Extracted ZIP, native 800x600 automated result fixture: completed=True, passed=True. Four pointer cycles, reading beyond the former timeout, settings interception/close, replay combat/hit, menu, and gaze selection passed. See new-800-fixture.txt. Screenshot standalone-800-result.jpg visually inspected: intact text and visible outlines. JPEG proxy difficulty 6.736, label 8.931, border 5.205; not a formal accessibility measurement.
- Same extracted ZIP, ordinary 1280x720 PC mouse run without proof flag: Hard selected, three practice targets destroyed, natural combat/loss and return; result held visibly for an additional 12.156 seconds, direct Replay click returned through transition to combat; a second natural loss followed by direct Menu click reached the hangar menu. 62 timestamped frames retained under ordinary/. This is actual OS input, not synthetic result injection. It is not a victory completion test or an HMD test.
- Original player records and non-session preferences restored after both runs. ProjectSettings hash preserved: A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89.

Suno/listening and HMD verification remain pending. Existing threat-label side parking tradeoff is not newly validated by this contrast change.

Published release 425100924, tag windows-result-contrast-20261027, targets product commit ae2cd406ecb809db2a6952dd088dd24c4011bb18. Public ZIP redownload matched all 89,614,129 bytes by SHA256 at 2026-10-27T06:30:49.2663486Z. Stable download: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/download/windows-result-contrast-20261027/MeteorDefenseVR-Windows-ResultContrast-20261027.zip
