# Stage/banner coexistence audit — 2026-10-23
Scope: audit existing readability/intro/campaign/telemetry evidence, then capture only missing reachable combinations at 800x600 and 2560x1080. Inspect longest actual stage/boss copy alongside allowed warnings, rewards and active abilities; verify safe area, fixed font readability, cleanup and input blocking.
Next: inventory existing evidence and production state/timing rules before constructing fixtures. No product change or build unless a meaningful defect is reproduced. Preserve user settings/history, HUD, pending Suno approval; no OS occlusion retry. Editor, executable, OS and HMD evidence remain distinct. Drive and continuous-improvement-state.json are out of scope.

## Inventory and reachable combinations
- readability-20261021/After: last-stage-longest-copy at all five supported sizes already proves standalone longest title/subtitle fits. FlightTransitionTests covers 1→2, pause/end/reset and nonblocking graphics. Do not repeat all ten identical subtitle variants.
- intro-transition-review-20261021 covers earlier launch/introduction adjacency, not carried buffs on final-stage arrival. campaign-audio-20261022 has stage10 boss audio/render evidence without this aspect/ability matrix. buff-readability-20261021 and telemetry-stable-20261022 cover max 3 abilities and fixed HUD at five sizes, separately.
- Current stage title varies only by number (1..10), subtitle after stage1 is always 새로운 운석 무리에 대비하세요. Boss WARNING maps to 위험! / 거대한 운석이 다가와요!, followed by 마지막 운석입니다! / 지구를 지켜주세요!.
- StageArrival resets on every state change and only appears for Playing; BossMeteor cancels it. Boss entry follows resolved regular meteors. Do not force a simultaneous stage card + boss warning + regular threats.
- PowerUpManager pauses carried buffs during StageClear and boss warning/narration; actual maximumActiveBuffs=3. Acquisition/reward/warning share MissionHudController's single feedback slot and replace each other, not independent stacked alerts. Next-stage spawning clears old regular meteors and uses the actual schedule.
- Next action: Editor controlled fixtures skip earlier progression setup, use stage9 completion callback and production timed advance to stage10, sample carried max buffs and refresh acquisition while Playing, then boss warning/narration. Preserve all original texts/fonts. Inspect resulting originals before any product change. This is not natural user completion of nine stages.

## Reproduced defect and targeted change
Before/800x600/stage10-settled shows the stage card background covering the actual WaveStarted FlightNotice while a real scheduled meteor, max three carried buffs, and an acquisition notification coexist. This is a visibility defect, not an invented boss/stage combination. The single product fix assigns FlightNotice a stable viewport-height lane (85%) between top telemetry and the shared feedback panel. Text, font size, duration, priorities, stage animation, fixed HUD, and gameplay timing remain unchanged. Validate actual glyph bounds rather than the oversized transparent TMP rectangle.

## Editor follow-up
- Final.xml: 2/2 passed (new coexistence fixture plus existing FlightTransitionTests). Successful 20 render samples: ten each at 800x600 and 2560x1080.
- Before: actual wave caption is covered by the moving stage plate at stage10-settled. After: wave caption and long low-health warning sit above the shared acquisition message, away from the stage title/subtitle. 800/wide low-health originals inspected; required copy preserved, no font shrink.
- Low-health state is explicitly prepared with RestoreCheckpoint(25), a reachable health+shield combination, because ApplyDamage while shielded correctly consumes a shield instead. It is not presented as a naturally occurring hit. Scene spawning remains production scheduled; no extra threat objects are forced into the boss warning.
- One initial harness missing inactive HUD reference and one oversized transparent text-rectangle assertion failed; retained separately. Actual visible glyph bounds are used for notice lane safety. Neither is counted as another product defect.
- Temporary scene reset/input suppression is scoped to the QA flag; physical keyboard/mouse device enabled states are restored in finally. No settings/history writes. Next: clean Windows build, package identity, separate extraction run and original visual review before release.

## Build checkpoint
Clean non-development Windows build bb7de7419d13d7d422578dfe864c44fc5e9247a0 succeeded (2026-10-23 16:59 KST). User ProjectSettings backup restored and compared successfully. ZIP 89,545,781 bytes / 222 files, SHA256 9f81da538adc5115d9d9ae3a81dcd90cdafd6b24c0ce4a8440ba64f007ace502. Separate extraction run and final originals pending; not yet published.

## Fresh extracted executable checkpoint
- Source bb7de7419d13d7d422578dfe864c44fc5e9247a0. Fresh ZIP extraction: all 222 files verified. Executable exit 0; one synthetic-input normal round passed with no issues, errors or duplicate callbacks.
- Reviewed all 20 original Extracted captures individually (10 states at 800x600 and 2560x1080). Stage 10 copy and digits remain complete; wave/low-health notices sit above the feedback panel and stage plate. Acquisition/max-three-buff displays remain readable. Boss warning/narration replaces feedback and clears the stage plate. End/reset captures clear the stage plate. Wide settled wave text is in its existing timed fade; arrival/acquisition show the full-opacity notice clear of both panels.
- Controlled stage setup plus production transition timers, camera renders and raycast assertions; not a natural full ten-stage playthrough, native OS interaction, or HMD verification. Menu pointer/regression suite was not rerun in this scoped change.
- ZIP SHA256: 9f81da538adc5115d9d9ae3a81dcd90cdafd6b24c0ce4a8440ba64f007ace502 (89,545,781 bytes). Next: publish this verified ZIP and re-download the public asset to compare hashes.

## Published and independently downloaded
Release windows-stage-notice-20261023 (421591220) is public as a prerelease, targeting source bb7de7419d13d7d422578dfe864c44fc5e9247a0. ZIP and SHA256SUMS asset digests were checked at upload. Public ZIP was downloaded again at 2026-10-23T08:10:46Z; its 89,545,781 bytes and SHA256 9f81da538adc5115d9d9ae3a81dcd90cdafd6b24c0ce4a8440ba64f007ace502 match the fresh-extraction-tested package. README points to this release. Older releases preserved. No changes to user settings/history, fixed HUD policy, pending Suno audio, Drive or automation state.
