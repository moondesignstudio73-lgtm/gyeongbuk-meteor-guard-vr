# Ordinary public build: replay follow-up

Product commit remains 00b1fd6e94efdd8ef3da04b53a2bf6c116217e61. No source, binary, release, or approval status changed. Used the same separately extracted public Windows ThreatSafeArea ZIP, SHA256 19776735694688f462d815c562cde3bb83b4e75ad621189c812533efcae5556f, ordinary mouse/no-webcam mode, 1280x720 client (1282x752 captured window). Natural Hard combat loss; no state or damage injection. Empty valid local record fixture was temporary.

## Direct result selection remains unverified

Frame 110 at 02:06:37.090 UTC shows the failed result and Replay button. OS click was attempted at 02:06:45.771 UTC, 8.681 seconds later. Frame 111 at 02:06:46.907 already shows the hangar menu, not replay preparation. The normal result timeout is eight seconds. This attempt missed the result selection window; it is neither a successful button test nor evidence of a defective button. Pointer positioning alone did not establish successful gaze dwell. The first screenshot after activation was unrelated stale imagery and is excluded from all retained evidence.

## Hangar replay departure captured

An independently observed hangar Replay button was clicked at (517,240). Frame 152 shows preparation. Frames 153–192 were captured in the same action call, with no further input: 0.344–0.403 seconds between observations (mean 0.367725), spanning 14.709 seconds after frame 152. This closes the prior missing departure interval for the **hangar menu** route only.

Individually inspected representative frames: 152 preparation; 156 open hangar and craft; 160 advancing craft; 164 cockpit blend; 168 countdown 1; 172 stage 1; 176/180/184/188 and 192 live combat with restored full shield. No repeated menu opening/closing or retained failure warning was seen in those inspected samples. The 41 snapshots are observations, not every rendered frame or a video; not every retained image received individual visual review.

Frame 193 follows the first post-replay mouse look action and shows changed view. It is 9.261 seconds after frame 192: do not describe this input as continuously captured from departure or claim a hit. No direct result-to-replay acceptance is awarded.

## Preservation and limits

The owned player process was stopped. Original records and non-session preferences were restored and checked at 02:07:52.961 UTC (see preservation.json). ProjectSettings.asset retains SHA256 A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89. No private preferences, record contents, or raw logs are published. retained-sequence.json and manifest.json identify the 44 retained original JPEGs; omitted indices are not included in this evidence set.

Actual HMD testing, Suno approval/work, listening, native 2560 capture, and the parked threat-label direction tradeoff remain outside this result. No Drive document or other thread was messaged.

## Follow-up: tool latency and existing result policy

The installed Computer Use guidance requires a two-cell state-derived input loop: observe and stop, inspect, then one input and immediate refresh. A same-cell screenshot-conditioned input loop would bypass that requirement; a future result button cannot be clicked using a pre-result screenshot. The observed 8.681-second observation-to-click latency exceeds the eight-second selection window. No further identical natural-loss attempt was made. This is a limitation of the current validation method, not proof that every possible OS control method is too slow.

STEP22_FINAL_REPORT.md explicitly lists result display for eight seconds followed by reset (lines 27 and 179). OperationsResetController.Tick implements that timer. HangarLobbyController installs BeginReturn as the timeout handler; GAMEPLAY_UPDATE_20261019.md states that the return cinematic must not consume the result time. These establish an intentional timed flow rather than an accidental disappearance.

Source review also identifies an accessibility/usability candidate: reading the result and HangarMenuButton.OnGazeStay do not suspend or renew the timer. Suspension is currently connected to look/audio settings panels through PcTestOverlay, and pending return presentation pauses elapsed time. A late gaze selection can therefore compete with timeout. This is a code-path finding, not an observed failed gaze selection.

Recommended product decision: use explicit Replay/Menu selection for player sessions and retain automatic return as a separately specified unattended-operation policy. Removing the documented timed flow is not necessary to diagnose the reported flicker or to upgrade hangar graphics. No product policy was silently changed merely to make the automation test pass. This follow-up contains evidence and a proposal only; direct-result Replay/Menu regression and any new policy implementation remain open. No new package was produced.
