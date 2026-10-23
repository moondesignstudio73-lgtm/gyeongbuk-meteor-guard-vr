# Readability coverage reconciliation — 2026-10-24

Current scope: reconcile existing evidence for start/loading/tutorial/combat HUD/boss/pause/settings/hangar/results/restart/warnings/rewards. Record actual source, resolution/aspect, state and original evidence location, separating Editor, extracted executable, native OS and HMD. Do not repeat completed combinations.

Next action: inspect existing verification records and capture inventories; write the coverage matrix, select only the largest remaining supported-PC evidence gap, then perform a bounded render/input audit. Fix and release only if a meaningful defect is reproduced. Otherwise evidence only, no new build.

Preserve user ProjectSettings and existing records. Suno remains pending; no external-app workaround. Drive and continuous-improvement state are outside this task. Natural human play and HMD remain unverified unless explicit new evidence establishes otherwise. No product changes or new release for this task yet.

## Completed outcome

coverage.md reconciles 13 screen/state groups with actual source commits, size/aspect, evidence paths and Editor/extracted-executable/native-OS/HMD distinctions. path-checks.json confirms existence of 12 representative evidence roots/files. Historical tests are not represented as new runs or universal latest-build acceptance. Rejected/intermediate candidate 56b81ea and obsolete result digit-size limitations are not used as current acceptance.

Selected only the updated start difficulty layout at ordinary 16:9. Latest accepted f624718 menu originals covered 800x600 and 2560x1080; the older five-size introductory tests predate that card reflow. The current source was inspected at docs HEAD 34a6ff9 (delivered product 411d9bab; start implementation f624718). No other screen/finished combination was rerun.

start.xml passed 1/1 at 2026-10-24 03:39:07–03:39:18 KST (XML UTC 2026-10-23 18:39). Unity 6000.5.10f1 batch Editor, D3D11 camera capture. Initial harness compile missed the GameFlow namespace; corrected before successful execution, no product defect inferred.

All six original PNGs in Start16x9 were individually visually reviewed: 1280x720 and 1920x1080 × selected-partial-dwell, experience-selected, locked-selected. state.txt preserves real selected level, lock state, positive partial progress (0.5), four collider-ray hits, full parsed text, fixed fonts, projected visible glyph bounds and minimum glyph height. No glyph-pair overlap recorded, no truncation/viewport escape; actual description glyph minimum approximately 24.62px at 720p and 36.93px at 1080p. The card wording remains complete with semantic line breaks, separated from instructions and start prompt; selected check, partial progress and amber disabled marker remain distinguishable without dimming required labels. No meaningful clipping, overlap or low-contrast defect reproduced in this selected screen.

Input boundary: actual DifficultyManager selection and direct choice gaze methods for partial progress, plus collider-ray checks and rejection of selection when locked. This is not full gaze routing, native mouse interaction or human testing; existing f624718 pointer/gate executable evidence is referenced, not rerun. Experience hint is a once-shown timed notification: visible in the first size's experience/locked samples, absent on the second size; no claim of all hint phases at both sizes. No natural full play, native OS retry or HMD validation performed.

No production code change, new Windows build, extraction run or release. Current public release remains windows-threat-notice-overlap-20261023. Temporary fixture archived as StartAspectCoverageTests.cs.txt outside Assets; rerun by copying into Assets/_Project/Core/Tests and selecting MeteorDefenseVR.Tests.StartAspectCoverageTests in graphics-enabled EditMode. User ProjectSettings change remains uncommitted; no other user file, history, Suno, Drive or continuous-improvement state modified. Next action: none within this scoped reconciliation; residual gaps remain explicitly recorded in coverage.md.
