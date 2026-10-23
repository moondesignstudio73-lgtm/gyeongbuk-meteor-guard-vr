# Populated records boundary review — 2026-10-23

No product defect was reproduced in this scoped review. No production code, font size, required value, build or release changed. Product source remains f624718cec44e895b14db1252606891ca5d5115b; working documentation HEAD at capture was 36db701. Existing user ProjectSettings modification is preserved.

## Existing evidence and gap
- Docs/LocalRankingQA contains populated recent, TOP 10 first/last, best and empty screenshots at ordinary scores around 12,600. Those are older UI and do not establish current maximum-width behavior.
- Docs/DeliveryQA/readability-20261021/After has maximum-score/24-hour recent-record renders at five sizes, but not populated maximum-width TOP 10 or all-difficulty best tables after the latest layout changes.
- Docs/DeliveryQA/readable-menu-text-20261023/Extracted has current executable empty TOP 10 renders and executable menu-transition/gaze/pointer proofs. The empty table was not treated as evidence for populated columns.
- Added only the missing current-layout combinations, at 800x600 and 2560x1080. No new full gameplay or unrelated UI sweep.

## Boundary selection and isolation
The temporary harness uses LocalRankingStore(null), accepted through its actual Add validation. The controller is initialized before adding data because Initialize calls Load and clears the store. Early harness attempts cleared fixtures accidentally; their empty-state run was rejected and is not acceptance evidence. One missing namespace compile issue was also corrected before the successful run.

Score range is 2,147,483,638–2,147,483,647 (nonnegative int maximum), with N0 formatting; ten records exercise ranks 1–10. Destroyed and combo are int.MaxValue, with combo equal to destroyed as required. HP 10000 and 9999.9 exercise the validator maximum and longest one-decimal format. Accuracy 1 and .999 both format as 100%; all four Korean difficulties are included. Play time is 86400 seconds, formatted 24:00:00. Stage uses actual campaign maximum 10; the store does not independently cap MaxStage, and arbitrary corrupted int stage values are not claimed as gameplay boundaries. Recent record contains the last timestamp and same maximum-length score format; exact int.MaxValue appears in TOP 10 and best records.

No operator history is overwritten. The harness ran in batch Editor with memory-only ranking; physical keyboard/mouse devices were disabled only within the fixture and restored. No webcam, settings mutation, OS window attempt, audio generation or HMD was involved. Harness sources are retained here for reproducibility, outside Assets so they do not enter the player build.

## Observations
Successful test: before.xml, 1/1 passed. Before/ is the unchanged product baseline, not a failed visual baseline. All 16 PNGs were individually viewed; 2560x1080 originals were displayed by the viewer at 2048x864 and retained at original resolution.

Each size covers recent-max, top-first, top-last, top-return-first, best-max, empty-Recent, empty-TopTen and empty-Best. Required digits, Korean difficulty and S10 stage suffix, percent and decimal HP remain legible. No inter-column overlap, clipping, missing values or footer/button overlap was seen. Recent score/rank, long destroyed/combo and full time fit. First/last page labels and next-button captions agree. Best rows retain all four difficulties and full comma-separated scores. Empty transitions clear prior values and hide next-page; restoring populated data restores next-page. Back and reentry return to the correct menu/recent tab.

The checks use explicit Present/controller Queue calls and camera renders in the Editor; they are not new extracted-executable or natural user-input evidence. Since no production change was needed, no new binary or release was manufactured. Existing extracted f624718 gaze back/modal/reentry proof at ../readable-menu-text-20261023/extracted-menu-verification.txt is reused, not represented as rerun. The public windows-readable-menu-text-20261023 ZIP remains the current delivery; prior public SHA verification remains applicable.
