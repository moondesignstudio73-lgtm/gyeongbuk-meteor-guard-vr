# Replay transition supplemental-menu lifetime — 2026-10-27

Ordinary screenshot 11-explicit-relaunch.jpg from the preceding release showed lower settings and education controls behind the replay preparation panel. Source review found ReplayPreparing keeps the core flow at Boot for at least 0.35 seconds while boot presentation is dismissed. The normal Boot checks therefore exposed those controls. This was not an intentional menu stage: HangarLobbyView hides its own menu actions for ReplayPreparing and Departing.

The focused baseline reproduces three visible entries (look, audio, education), accepts all three entry callbacks during ReplayPreparing, and observes three stale entries at the departure boundary: nine recorded violations. The completed diagnostic test is not a product pass. Its rendered preparation frame matches the ordinary observation. The departure-named image is another preparation sample; notes.txt records its actual phase. It is not evidence of fully departed motion.

Change: a phase-specific gate for Returning, ReplayPreparing and Departing; immediate phase notification hides the entries before render, while normal update visibility and shared callback guards reject stale mouse/gaze selections. Education registration/operator/training entry uses the same gate. Normal title/base settings remain available. No changes to cinematic art, movement, combat, audio, or threat labels.

Focused Editor validation: zero violations. It checks title settings open/close and education entry, base-menu settings open/close, replay preparation rendering and stale callbacks, departure visibility, combat entry without retained buttons or modal flags, and result-to-base settings open/close. Explicit core result/menu actions are used; this is not ordinary OS input, natural whole-round gameplay, return-motion acceptance or HMD/audio validation. The prior 256/600 threat suites were not repeated.

Excluded fixture failures: the first attempt checked before boot UI readiness; the second omitted the capture directory. Bounded readiness and capture-directory creation/error propagation were added to the fixture only. No partial or failed build is being released. Editor batch rankings are in-memory; no participant was submitted and settings values were not changed. Native execution uses the existing backup/restore wrapper.

Managed worktree only. Original checkout/index lock and Suno approval untouched.

## Extracted release

Product 372a75f438e40f5832ffc05bf14cc6f6943ca7f3; clean non-development build. ZIP 89,620,759 bytes, 222 entries individually hash-verified; SHA256 147a191a65a30af5f9ea627b56fd06a96c94aa88b13cc72ee2576c0a4856d6b9.

Extracted Windows player on RTX 3070 / Direct3D12 passed the same short opt-in fixture with zero violations. Native replay preparation and base-return renders were visually inspected; five stage renders and notes are retained. The departure-named native image also records ReplayPreparing in notes; the loop separately checks departure visibility until Playing. Records and non-session preferences were restored and verified. No new ordinary OS playthrough is claimed for this package; preceding 527a889 ordinary play supplied the original observation. HMD/listening/return-motion continuity remain unverified.
