# Threat transition frame review — 2026-10-27

## Reproduced defect and change

Label placement updated every frame, while arrow/direction text waited for a 0.1-second refresh. A rear target reversing to the right could therefore show a left-pointing arrow at its new right-side position. Before/800x600/rear-reversal/04 visually demonstrates this; the matching After image shows the correct right arrow.

Only the direction string is cached and compared. A changed direction now refreshes text in the same LateUpdate as placement, while unchanged directions keep the normal distance refresh cadence. No placement, font, capacity, threat priority, sounds, or lower-summary rules changed.

## Focused continuous-frame evidence

Four sequences at 800x600 and 1280x720: rear reversals, +/-1 centre jitter behind the camera, rear/above/side direction changes, and five competing rear targets with a four-label budget and low-health warning pressure. Each sequence observes 32 consecutive render callbacks after threat layout, with actual local target positions, label positions/text, frame IDs, and timestamps. Total: 256 frames / 448 labels per run. Rendering is observed every frame; PNG saving is limited to transition samples and violations to avoid stretching every frame beyond the old refresh interval.

- Before diagnostic run: 220 direction text/arrow mismatches, zero opposite-half placement samples, and no other reported violations. Diagnostic test completion is not a passing product assertion.
- Before frame IDs advance by exactly one in every sequence. Recorded intervals ranged from about 5.8ms to 74ms, all below 100ms. See Before/timing.json.
- After validation run: 448 labels, zero violations. Checks cover actual arrow/direction against coordinates, expected label counts, active/unpaused combat, text size/truncation, viewport containment, aim/HUD/notice clearance, pairwise label overlap, and centre-jitter position stability. The focused Editor test passed.
- The earlier full 600-frame safety suite was not rerun; these boundary cases include the relevant clearance checks.

The first diagnostic attempt timed out at the resolution switch because the fixture subscribed to a renderer pending destruction. It is excluded. The fixture now uses the exact renderer owned by its current probe. No game behavior was changed for that fixture correction.

These are explicit pooled-target movements, not natural play, OS input, or HMD acceptance. Near the exact rear centre, the mathematically shorter turn can change rapidly; arrows now follow those real direction changes immediately while the label position stays stable. Human interpretation and HMD comfort remain unverified. Finite crowd fixtures do not certify all possible space-exhaustion configurations.

Work continued only in the managed threat-direction worktree. The original checkout index lock was not touched. User records/settings preservation and Suno approval wait remain in force.

## Extracted release verification

Product commit: 527a889ae9fc13421fb74254270f01b94d9040b1. Clean non-development Windows build; 222 package entries individually hash-verified. ZIP SHA256: 8a9feb91a51e95958e7a9ea14bfe82789abe200740e0deb2ba65dcfc541e8ce8 (89,619,662 bytes).

The extracted archive ran the opt-in fixture under Direct3D12 on RTX 3070: 256 consecutive frames / 448 labels, zero violations. Native timing records confirm consecutive frame IDs; screenshot capture can slow wall-clock intervals. The Editor baseline supplies sub-100ms reproduction evidence. Inspected native rear reversal, crowded, and above-direction frames. This release was not given a new ordinary OS-input or HMD playthrough; the previous release's ordinary gameplay check is not claimed as a new check. Records and non-session preferences were restored and verified.
