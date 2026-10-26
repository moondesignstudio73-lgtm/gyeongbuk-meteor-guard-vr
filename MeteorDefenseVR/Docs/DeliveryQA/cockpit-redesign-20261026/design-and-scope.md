# Hangar, departure and cockpit presentation — 2026-10-26

Product: f5234e877b0b0e2d1df8e60ff705b8dfab5b9017. Previous public product: 1723e35.

The design prioritizes pilot information: quiet navy surfaces, pale neutral text, restrained teal for normal status and amber for warning. Values have stronger hierarchy than captions. Lower panels remain fixed at the dashboard edges; neither moving targets nor decoration relocates them.

Implemented: bay-only satin metal material grain, differentiated carbon/alloy/edge response, service panel vents, lower rails and lamp housings. Added fittings are combined by material. Departure distance is retained and accumulated through Space/countdown. The lobby no longer overwrites departure positions. Pilot camera translation is untouched; cockpit vibration is removed. Film blur and peripheral streaks are reduced. Top/bottom HUD layout and selected menu/settings colors are updated.

This is a focused hangar and cockpit release. Start/loading/tutorial/boss/result layouts were captured for regression but were not all redesigned. HMD and human OS input are unverified. Suno work is unchanged.

## Evidence interpretation

- `Before`, `Baseline`, `BaselineCorrected`: old presentation observations. Their first-start attempts did not finish tutorial and are NOT first-start passes. The original replay action, after an explicitly forced result, did reach Playing and provides the before replay sequence. `validate=false` means the harness completing is not a continuity pass.
- `After`, `Review`: rejected input-harness attempts. `Candidate` and `Candidate2`: rejected test-driver initialization attempts. `ReviewDebug`: diagnostic visuals, not acceptance.
- `Candidate4`: accepted Editor run. Both first start and forced-result replay reach Playing; 62/63 motion frames, no observed hangar Z reversal, camera translation 0. One test passed in 95.83 seconds. The QA mouse is explicitly enabled/current and relative events are queued once per PlayerLoop frame. No direct tutorial/practice completion is called.
- The 65 gallery captures use explicit states/menu actions and an injected maximum score, at 800×600, 1024×768, 1280×720, 1920×1080, 2560×1080. These are display fixtures, not full campaign sessions. Result count-up can still be in progress in its capture.
- Before/after replay videos use recorded frame intervals. The before comparison is replay only, not the failed original first-start attempt.

Only sampled originals have been manually reviewed; retaining 205 Editor frames does not mean all 205 were inspected. Reviewed samples include 800×600 maximum-score HUD/result, 1024×768 look settings, 1280×720 hangar/HUD and departure frames, 1920×1080 start, and 2560×1080 hangar. The health segment overflow and stray horizontal decorations seen in an earlier candidate were corrected before Candidate4.

Native ZIP/process/hash verification is recorded separately in `extracted-run.json`, `process-f5234e8.json`, `pc-soak.json`, and the publication verification report. No complete engine logs, private preferences, ranking backups, or user ProjectSettings backup are intended for commit/publication.
