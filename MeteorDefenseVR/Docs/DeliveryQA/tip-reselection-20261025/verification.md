# Experience tip follows user reselection — 2026-10-25

Baseline product31137b2, repository0c6c9fa plus QA-only additions. Fresh scene/view, supported unlocked Normal default configured before initialization. Original DifficultyChoiceButton collider and GazeRaycaster receive synthetic rays and elapsed0.8s dwell; no Choose/Select shortcut or private timer/shown writes. Runtime timer reflection is read-only. Post-LateUpdate render capture reuses the corrected prior harness. This is controlled input, not human OS/HMD gameplay.

Before800/1280: Experience→Easy changes selected value and the single check toEasy while the full Experience sentence remains active (~2.857/~2.848s remaining). Immediately returning toExperience still shows the original timed tip (~1.91/~1.886s). Original before/easy-selected screenshots confirm the misleading combination. before.xml2/2 is successful observation with desired-behavior validation disabled, not a no-defect result. All six before states per resolution are retained, including natural expiry and Normal→Experience after expiry.

Minimal product fix: in DifficultySelectionView.Refresh, non-Experience selection cancels remaining time and deactivates the tip immediately. The shown flag is never reset. The existing first Experience display,4s normal expiry, lock/selection/start rules and layout remain unchanged. QA verifies actual selected value, exactly one matching visible check, tip and restored choose instruction; reselecting Experience before or after the original expiry does not restart the one-time tip.

Cancellation happens synchronously in the existing SelectionChanged subscriber. Screenshots are captured after the0.8s dwell completes (confirmation threshold0.7s), not an instrumented exact event-time screenshot. The later recapture waits until4.3s after the first selected capture; private timers are neither reset nor fast-forwarded. An independent ordinary selection/expiry scenario verifies the normal4s timer separately.

Scoped regression: reselection800/1280, plus ordinary first Experience selection/natural4s expiry at1280. No operator-default switching or hangar-return expansion. Previous35-image suite not repeated; earlier rejected packages and timing provenance remain intact in the earlier tip-readability/experience-tip documents. User ProjectSettings edit, shared reviewer state/Drive and Suno remain untouched.

## Final ordinary Windows verification

Source7b7324edc12aa3de2a60010213de1e4547d7d49e, normal clean ReleaseCandidate, developmentBuild=false, no extra compilation defines. Explicit QA flag only; ordinary launch does not run fixtures or override initial operator defaults. after.xml3/3 passed. Final ZIP MeteorDefenseVR-Windows-TipReselection-20261025.zip is89,586,494 bytes/222 verified files, SHA256795c8aced30e40efb499740904d519da61124a913ed767343b93b66f8e23630b.

Three new extracted native processes2026-10-24T18:21:21Z–18:22:09Z (25Oct KST), RTX3070/defaultDirect3D12, all exit0, no timeout, assertions passed. All16 final original renders individually visually reviewed. Reselection800/1280 each has six states; ordinary1280 initial/200ms/reading-window/expired has four. Easy selected value and check match, tip is inactive and remaining0; returning toExperience before/after the original duration does not restart it. Normal selection/check also correct. Ordinary first tip remains visible through the reading window and expires naturally.

The choose instruction retains its existing fade when reactivated: easy-selected captures show it very dark despite instruction=True; subsequent experience-reselected/expired originals show it fully readable. Do not interpret hierarchy-active as instant opaque text. Cancellation of the obsolete Experience sentence and selected check are visible immediately in the sampled Easy captures. No new fade or duration behavior introduced.

Build wrapper exit0 and exact user's ProjectSettings backup SHA match. Native runs changed only four Unity launch-session preference fields; other preferences and ranking primary/bak/tmp unchanged, no restoration necessary. Shared reviewer state/Drive and Suno untouched. Scope remains these user-reselection paths and ordinary expiry, not natural OS/HMD play or the prior full35-state matrix.

Published prerelease422906327 / windows-tip-reselection-20261025, target7b7324e. Independent public redownload2026-10-24T18:25:54.8600074Z matches tested ZIP SHA256/length (public-redownload-verification.json).
https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/download/windows-tip-reselection-20261025/MeteorDefenseVR-Windows-TipReselection-20261025.zip
