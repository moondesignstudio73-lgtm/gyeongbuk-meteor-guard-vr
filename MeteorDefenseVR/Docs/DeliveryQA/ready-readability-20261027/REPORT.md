# Start guidance readability — Windows 2026-10-27

Product commit: `8d610a882cfb986ee46901846580052ac73ff4b9`.

The complete start hint now sits in an opaque, padded panel to the right of the difficulty cards, clear of cockpit and radar lines. Active difficulty descriptions use the brighter body-text color. Hint font size 260, title 300, description 240 and all copy remain unchanged. The card/hint composition is centered; the experience notice retains its screen center.

## Validation scope

- Before: previous product UI from bb59aa1, with the new comparison fixture. Editor test passed 1/1; 90 images.
- Accepted: updated UI, Editor tests passed 2/2, including combat assistance rejecting off-axis menu selection while allowing direct gaze. 90 images.
- The fixture checks opaque fill, hint padding, unchanged font sizes, text bounds and description contrast. Each of five resolutions includes start, direct-gaze Hard selection, Experience selection and locked state with the experience notice visible.
- Synthetic gaze and Input System mouse events exercise selection, Start → Intro, and result Menu/Replay. Experience-notice rearming is confined to QA.
- ExtractedFinal contains the newly extracted normal Windows build's rendered gallery and runtime evidence. Its process, exit status and preservation result are recorded in extracted-run.json.
- BeforeNative is a byte-identical copy of the previous published build's native start originals, not a newly executed old build. See BeforeNative/provenance.json. Editor Before captures provide the simultaneous notice/locked comparison.

These are automated rendered-player/input checks, not a manual OS-input playthrough, headset test or audio listening test. Other screen redesigns and Suno audio are outside this small follow-up. The earlier hangar graphics upgrade is retained.

## Native start comparison

| Resolution | Previous native release | New extracted native release | New notice + locked state |
|---|---|---|---|
| 800x600 | [Before](BeforeNative/gallery/800x600/start/stability-game-render.png) | [After](ExtractedFinal/screens/gallery/800x600/start/stability-game-render.png) | [Locked](ExtractedFinal/screens/gallery/800x600/locked/stability-game-render.png) |
| 1024x768 | [Before](BeforeNative/gallery/1024x768/start/stability-game-render.png) | [After](ExtractedFinal/screens/gallery/1024x768/start/stability-game-render.png) | [Locked](ExtractedFinal/screens/gallery/1024x768/locked/stability-game-render.png) |
| 1280x720 | [Before](BeforeNative/gallery/1280x720/start/stability-game-render.png) | [After](ExtractedFinal/screens/gallery/1280x720/start/stability-game-render.png) | [Locked](ExtractedFinal/screens/gallery/1280x720/locked/stability-game-render.png) |
| 1920x1080 | [Before](BeforeNative/gallery/1920x1080/start/stability-game-render.png) | [After](ExtractedFinal/screens/gallery/1920x1080/start/stability-game-render.png) | [Locked](ExtractedFinal/screens/gallery/1920x1080/locked/stability-game-render.png) |
| 2560x1080 | [Before](BeforeNative/gallery/2560x1080/start/stability-game-render.png) | [After](ExtractedFinal/screens/gallery/2560x1080/start/stability-game-render.png) | [Locked](ExtractedFinal/screens/gallery/2560x1080/locked/stability-game-render.png) |

The full local gallery is indexed by evidence-manifest.json. Selected original images and machine-readable results accompany this report. Public release identity, archive size/hash, file count and redownload verification are recorded in release-evidence.json.

User ProjectSettings bytes were restored exactly (SHA256 A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89). Private settings, ranking backups and engine logs are excluded from public evidence.
Native result: 90 images, exit 0, RTX 3070 / Direct3D12, 2026-10-26T17:14:54Z to 17:16:31Z. All five locked/notice originals and the 1280 normal start were visually inspected.
