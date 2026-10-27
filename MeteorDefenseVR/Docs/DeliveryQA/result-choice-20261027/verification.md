# Player-controlled result screen (2026-10-27)

This changes the prior eight-second result policy recorded in STEP22_FINAL_REPORT.md. A player can need longer to read the score and feedback; a late gaze selection must not compete with automatic return. The active interactive hangar/result presentation now owns an explicit-choice policy: only Replay or Menu ends the result. Settings suspension/resume and repeated results cannot restart an automatic timer. Disabling the interactive presentation retains the pre-existing bare-controller fallback; no kiosk mode or new settings UI has been invented.

Authorization scope was verified by reading the user-authored automation instructions in review thread 01a15103-0291-73e2-9cab-052a615bd708, 2026-10-27T04:55:50.342Z: evidence-based usability improvements, implementation, verification and GitHub executable delivery were delegated. Suno and HMD restrictions are unchanged. This is a reading/selection improvement, not a test-only timeout extension.

## Executed validation

Product commit: 9b483b36b2f9b633256f5fee7540dc53cc628181. Clean non-development Windows build, Unity 6000.5.10f1. ZIP: MeteorDefenseVR-Windows-ResultChoice-20261027.zip, 89,613,148 bytes, 222 entries all compared by SHA256 to the build. Archive SHA256: e0aba961483de7b34f1eff750ce4fbbe29ba79c8bab4623519bfd68be873d5d6.

- Editor tests: 15/15 passed (tests.xml), covering result timeout ownership, repeated reset/settings suspension, existing hangar return/replay/calibration flow, failed-return cinematic, and pause priority. These use fixtures and direct test calls, not ordinary player input.
- Extracted standalone fixture: standalone-fixture.txt reports completed=True/passed=True. Non-failed result fixtures remained visible for at least nine seconds without cancelling the timer in the harness. Four synthetic InputSystem pointer cycles selected Menu/Replay exactly once; replay reached launch/countdown/combat and an actual weapon hit using synthetic aiming. Settings modal interception and closing followed by a further nine-second hold passed. Mouse-gaze dwell selected once without pointer click. These are explicit state fixtures and synthetic inputs, not OS/human victory gameplay.
- Ordinary extracted player: no QA flags, mouse/no-webcam, 1280x720 client. Completed tutorial using OS mouse input and natural combat losses. Result frame 104 through 116 remained visible for 20.021 seconds. OS click at (492,478) then produced preparation (117), hangar departure (120/124), cockpit blend (128), countdown (132), stage title (136), and combat with full shield (140/157). Frames 117–157 span 14.710 seconds with short snapshot sampling. Frame 158 shows the first subsequent OS look response; its separate tool-call gap is not described as continuous capture or a hit.
- A further natural loss returned to result (199). OS settings open (200), close (201), and a 13.600-second post-close hold through 213 kept the result visible. OS Menu click at (790,478) produced the hangar menu (214). Dock repair graphics are expected during this immediate menu frame; this is not evidence of completed repair.

Representative images listed above and the final image of each capture batch were visually inspected; not every retained snapshot was reviewed individually. Retained original JPEGs and timestamps are identified by ordinary/retained-sequence.json and manifest.json. Snapshots are not every game frame or a video.

An unrelated Zoom crash-report window interrupted the first combat attempt and triggered focus-loss pause. Its captured frames 42–57 and that attempt's inferred replay are excluded. The report was closed without sending; gameplay was resumed. No acceptance is based on that obscured interval. The subsequent directly observed result holds and button actions are the accepted ordinary evidence.

## Preservation and limits

Fixture and ordinary runs restored original local records and non-session preferences (preservation JSONs); ordinary restoration completed 2026-10-27T05:14:20.8458100Z. ProjectSettings.asset still has SHA256 A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89. Raw logs and user data are excluded. Ordinary player log scan found zero NullReference/MissingReference/MissingMethod/IndexOutOfRange exceptions or shader errors.

Actual OS victory gameplay, HMD, listening, Suno generation and the previously noted threat-label direction tradeoff remain unverified/outside this change. The non-failed result was tested through explicit fixtures, while failed results and both destinations were verified through ordinary OS play. This release does not claim full-game QA completion or a new hangar graphics upgrade.

## Published package

GitHub release 425059421, tag windows-result-choice-20261027, targets product commit 9b483b36b2f9b633256f5fee7540dc53cc628181. Uploaded ZIP and checksum were verified against GitHub asset size/digest. Public download was fetched again at 2026-10-27T05:17:03.9125103Z and matched the tested archive SHA256 and 89,613,148-byte size (public-download.json). The tested extracted build and the public archive therefore contain the same packaged files. Drive updates remain owned by the review chat; none were made here.
