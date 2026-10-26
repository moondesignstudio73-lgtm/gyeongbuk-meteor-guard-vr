# Integrated UI verification and critical survival notice

## Baseline: actual latest public ZIP

Public release windows-loading-boss-20261027 / product0bf9fc944b8ba9df2be28c9c10b565fccb0e70d0. Independently downloaded archive SHA256 3e4e0f13725aa6de0812ee0781648d602ce52ec9a9556318701e1eda14faf0af, 89,606,406 bytes, 222 extracted files compared with their build hashes. This batch uses a new extraction and new processes; no old f523 run is relabelled.

PublicExtracted/pc-soak.json: two Experience rounds passed, each 4980 score / 21 destroyed / 24 shots and hits / zero errors and warnings. Actual timed Intro, briefing, calibration, tutorial, practice, launch, countdown, combat, boss, mission complete, result and reset events were required once per round. Real InputSystem synthetic aim drives gaze/weapon hits; boss is defeated through weapon damage, not an explicit completion call. Starts and standby use internal menu commands, and preflight, failure return, boundary, menu and health suffixes are separate controlled fixtures. Times, process exit0 and exact preservation audit are in public-run.json (19:52:22–19:57:28 UTC, RTX3070/Direct3D12). This is not human OS gameplay, a full ten-stage campaign or an uninterrupted user-clicked replay loop.

PublicExtracted/MenuTransitionGaze verifies actual collider gaze: holding an old target does not retry, leaving and reentering performs the normal dwell and reaches Playing. Initial failure result/menu/settings setup is explicitly forced. PublicExtracted/ThreatNotice uses checkpoint health25 and normal director update, not a direct ShowNotice call; 800/wide space/Earth, moving threat and fade samples. Screenshot health25 and low-shield message agree. Damage0 in those checkpoint images is expected because RestoreCheckpoint does not simulate damage.

PublicPointer was separately run on the same unchanged public ZIP. Native exit2 / passed=False is preserved. Every real UI pointer down/up/click, result exit/destination, modal interception and gaze guard observation passed, but two old configuration assertions failed: they assumed only result buttons were immediate-click controls. Current DifficultySelectionView deliberately adds StartMission as an immediate-click control. This is a stale harness contract, not evidence that clicking failed. Do not relabel this process as passed. The final harness explicitly allows that one authored start control and verifies replay preparation through actual launch/countdown/Playing and a weapon hit after a synthetic pointer click.

## Reproduced issue and scope

At supported minimum800×600 the real survival notice is a small, dark, long orange sentence. It is readable when scrutinized but weak for a brief critical alert. The previous loading-boss concurrent sample uses direct ShowNotice with FlightUiTheme.Warning at health100; it is a layout stress fixture, not a real low-health event, and its tint differs from production. Production generates this notice only in Playing; entering BossMeteor hides the old notice. Its forced lower-lane boss sample is therefore not evidence of ordinary gameplay chronology.

The public2560×1080 original also exposes a stronger defect: the upper survival notice is behind the central telemetry card. An initial enlarged upper-lane candidate passed four older tests but was rejected visually because actual impact feedback obscured its lower glyphs. A second upper-lane candidate passed800 but the added telemetry assertion rejected wide. Both are retained locally as rejected evidence, never published.

Two related changes are scoped to critical survival notices: 35% larger/brighter text with fitted opaque backing, and a stable lower lane below aim/above dashboard readouts. This reuses the already verified boss-notice lane. A further visually rejected lower-lane candidate exposed near-camera impact particles covering the middle of the sentence despite passing geometry tests. Critical text now uses an owned overlay-ordered material and the backing reuses the authored HUD overlay material; ordinary notices restore their original material/sorting/size. Owned material is released on destruction. No change to health, damage, progression, scores, warning cadence or lifetime. Ordinary wave notices retain their original upper placement. QA-only improvements are separate: updated pointer contract/continuation and an actual meteor impact fixture. Unrelated UI is not redesigned.

EditorImpact is a separate five-size collision test: explicit StartMainGame/scenario setup, one pooled meteor with75 damage, ordinary movement/swept collision, actual health event100→25, HUD25 and generated Korean notice. No direct health assignment, damage call or ShowNotice invocation. It asserts minimum17px letter/digit height, no overlap with actual impact feedback/top telemetry/lower status panels/aim, and overlay depth policy. It is Editor evidence, not unchanged public-player collision evidence. Rejected early test attempts failed on test-domain reload/inactive HUD lookup and are excluded from acceptance.

## Scope still open

See [current coverage and gaps](coverage.md). Natural human OS interaction, HMD, listening and Suno remain incomplete. Sampled renders are not proof of no flicker between every frame. Synthetic combat, forced state layout, real input-module clicks, and actual collision fixtures are intentionally reported separately. A forced initial result followed by real replay input is not a completed natural victory→user replay→second victory chain.

Final corrected build, targeted tests, screenshots and publication evidence are recorded below after verification.

## First-visible-frame regression

Candidate109 was not published. A render-completion observer on an actual meteor collision reproduced a one-frame flash at viewport y=0.85, alpha=1; following frames were y=0.27. The collision test failed as intended. ShowNotice now begins critical copy transparent and disables its stale backing until the next positioning/fade update; CriticalNoticeBounds exposes only an enabled backing. This prevents reserving the stale upper rectangle too. The change preserves normal warning duration/repetition. The first-frame test records every visible warning frame during the collision sample and requires the lower lane. Candidate109 binary and evidence remain in local ignored archives; it is not the final acceptance source.

The retained hangar upgrade is documented in [hangar graphics](../hangar-graphics-20261022/verification.md). Current extracted landing was visually inspected again: metal/normal maps, floor panels/grates, fittings, lights and chamfered geometry are present. No new external assets or generated textures were introduced in this final UI batch.

Final951ce7d visual review before publication: all five EditorImpact collision originals, all five native boss-warning-concurrent originals, the three native pointer replay continuation originals, and timed native tutorial/boss/result originals. Boss concurrency intentionally forces the danger sentence at full health; actual low-health evidence remains separate. Selected PNGs are committed alongside all fixture state/verification metadata; manifest entries marked committed=false are retained locally, not downloadable evidence.

Additional final951ce7d original review: failure-landing (retained hangar art), ThreatNotice800/space/hold and2560/Earth/hold, TransientFeedback800/heal-overlap and2560/resumed-critical. TransientFeedback invokes explicit fixture conditions, including a forced critical sentence at health100; it does not contradict the separate actual collision100→25 evidence. Top-edge direction-label occlusion remains a documented gap; no claim of complete UI acceptance.

## Final corrected release

Product commit: 951ce7d003e76a2c79d947b4af3f08430249b84f. Normal clean non-development Windows build. ZIP MeteorDefenseVR-Windows-SurvivalReadability-20261027.zip, 89611718 bytes, 222 verified files; SHA256 7eb00471b73637f1634472425e7be769a3cf5248cf6522583d7b07f84ef070b2. [Release](https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-survival-readability-20261027). Public re-download matched at 10/26/2026 20:51:13.

Final extracted native integration: exit 0, 10/26/2026 20:44:41–10/26/2026 20:50:08. NVIDIA GeForce RTX 3070 / Direct3D12. Two new synthetic combat rounds; see FinalExtracted/pc-soak.json, separate from the baseline0bf9 rounds. Native pointer continuation exit 0; final boss five-size fixture exit 0, 50 images. Separate processes, not one uninterrupted human playthrough.

- Round 1: passed=True, score=4980, destroyed=21, shots=24, hits=24, errors=0, warnings=0, result after 90.8695831298828s.
- Round 2: passed=True, score=4980, destroyed=21, shots=24, hits=24, errors=0, warnings=0, result after 91.0395889282227s.

Final Editor suite4/4 passed on final source: actual collision at five sizes including every observed first-visible frame, boss five-size layout, stage/notice and threat clearance. Earlier failed shader-property assertion and candidate109 first-frame regression are retained as rejected evidence, not final passes. All five final collision originals were visually inspected for complete text, no effect occlusion, and clearance from aim/top/lower HUD. Native checkpoint-warning originals and replay continuation originals are separately identified in the manifest.

Original user ProjectSettings exact SHA256 remains A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89; all native wrappers verified ranking/non-session preferences preserved. Private backups and raw engine logs are excluded.

## Same-scenario public baseline versus final native warning

| Size | Baseline checkpoint25 | Final checkpoint25 |
|---|---|---|
| 800x600 | [Original](PublicExtracted/ThreatNotice/800x600/space/hold/stability-game-render.png) | [Original](FinalExtracted/ThreatNotice/800x600/space/hold/stability-game-render.png) |
| 2560x1080 | [Original](PublicExtracted/ThreatNotice/2560x1080/space/hold/stability-game-render.png) | [Original](FinalExtracted/ThreatNotice/2560x1080/space/hold/stability-game-render.png) |

## Actual impact originals (Editor)

- [800x600](EditorImpact/800x600/stability-game-render.png), [health/damage/glyph/bounds](EditorImpact/800x600/state.txt).
- [1024x768](EditorImpact/1024x768/stability-game-render.png), [health/damage/glyph/bounds](EditorImpact/1024x768/state.txt).
- [1280x720](EditorImpact/1280x720/stability-game-render.png), [health/damage/glyph/bounds](EditorImpact/1280x720/state.txt).
- [1920x1080](EditorImpact/1920x1080/stability-game-render.png), [health/damage/glyph/bounds](EditorImpact/1920x1080/state.txt).
- [2560x1080](EditorImpact/2560x1080/stability-game-render.png), [health/damage/glyph/bounds](EditorImpact/2560x1080/state.txt).

[Replay preparing](FinalPointer/ResultPointer/replay-preparing/stability-game-render.png), [Playing](FinalPointer/ResultPointer/replay-playing/stability-game-render.png), [actual weapon hit](FinalPointer/ResultPointer/replay-hit/stability-game-render.png), [input assertions](FinalPointer/ResultPointer/verification.txt). Initial result is forced; no further start/state API is invoked between click and first hit.
