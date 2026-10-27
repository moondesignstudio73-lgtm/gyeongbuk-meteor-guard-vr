# Player-controlled result screen (2026-10-27)

This changes the prior eight-second result policy recorded in STEP22_FINAL_REPORT.md. A player can need longer to read the score and feedback; a late gaze selection must not compete with automatic return. The active interactive hangar/result presentation now owns an explicit-choice policy: only Replay or Menu ends the result. Settings suspension/resume and repeated results cannot restart an automatic timer. Disabling the interactive presentation retains the pre-existing bare-controller fallback; no kiosk mode or new settings UI has been invented.

Authorization scope was verified by reading the user-authored automation instructions in review thread 01a15103-0291-73e2-9cab-052a615bd708, 2026-10-27T04:55:50.342Z: evidence-based usability improvements, implementation, verification and GitHub executable delivery were delegated. Suno and HMD restrictions are unchanged. This is a reading/selection improvement, not a test-only timeout extension.

Validation and executable evidence will be recorded below after execution. The earlier 00b1fd6 public package is unchanged until a replacement is built, tested and uploaded.
