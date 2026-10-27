# Threat direction placement review — 2026-10-27

## Confirmed defect and bounded change

Previously a label relocated around protected HUD panels kept its parked side for the target's lifetime. A rear target swept from local x=-35 to +35, z=-25: the text changed from `◀ REAR` to `REAR ▶`, but the label stayed at viewport x=0.0867. The lower summary correctly remained `뒤쪽`. A front/top sweep similarly left `RIGHT ▶` on the left while the summary said `오른쪽`.

Only label placement changes. A horizontal component beyond 18% of target distance allows a stale opposite-side slot to be reconsidered, with a strong preference for the target's current side. Inside that centre band the existing slot remains stable. The full safe side lanes are searched; the existing expanded HUD/aim/notice exclusion regions, label size, font, and clearance remain authoritative. Slot spacing adds one unit to avoid falsely rejecting touching expanded bounds. Side switches jump between safe slots instead of travelling across the aiming area. Existing safety fallback still takes precedence if a desired side has no available slot; this is not a proof for every possible crowded scene.

No direction text, threat selection, timing, sound, or lower-summary classification was changed. Rear/above combinations can have a rear-turn arrow and a height-based summary because both spatial properties are true; the existing classifiers have different priorities. Human interpretation of that combination remains untested.

## Evidence and scope

- Controlled pooled targets, frozen movement with explicit camera-relative positions; actual rendered sequence at 800x600 and 1280x720. Not natural gameplay or OS/HMD input.
- Baseline: 182 observed labels, 55 samples on the opposite horizontal half using |local x| > 10. This metric is placement disagreement, not 55 incorrect arrow strings. Rear crossing contributed 8; front crossing 7; top crossing 8; four-target crossing 32. All sampled direction strings matched their local-position classification.
- Matching After sequence: 182 observed labels, 0 opposite-half samples. Original and changed rear-crossing and four-target images were visually inspected at 800px; changed four-target image also inspected at 1280px. Four right-side targets were separately visible on the right without shrinking text.
- Final reusable fixture adds centre jitter (+/-1 at z=-25): 206 label observations, 0 opposite-half samples; centre positions stable within 0.002 viewport units; no label overlap, central aiming overlap, truncated text, or font-size change. Final Editor test passed.
- Separate regression suite: 4/4 passed (direction sequence, protected layout, critical warning clearance, priority/pool reuse). Protected-layout regression checked 600 rendered frames at 800x600, 1024x768, 1280x720, 1920x1080, and 2560x1080: 0 violations, with monotonic-sweep placement, clear/reuse/pause/resume/reset checks. Detailed traces and fresh verification summaries retained here; historical QA files were restored after this run.

The raw frames and target coordinates allow inspection of direction and placement. They do not measure human intuition, VR comfort, or HMD readability. Suno access remains pending separately.

## Delivery isolation

The original checkout retained a Git index lock. Automated approval rejected deletion of that lock without supplying a specific reason. No forced deletion or lock bypass was performed. A managed worktree at the accepted ResultContrast source was created for committing/building this update. The user's original ProjectSettings and player data remain preserved. Build/package/native verification will be recorded here after completion.

The initial worktree import rewrote settings with equivalent normalized contents and triggered the clean-source build guard; index normalization produced no staged setting changes. Historical primary-checkout QA files and the original source were restored from HEAD after the verified source was committed in the managed worktree. The primary checkout's locked index can still show stale modification flags despite empty content diffs. Its user ProjectSettings hash remains A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89.

## Excluded native runs and fixture corrections

1. First native attempt started the opt-in fixture in Bootstrap before the game scene existed and threw a null-reference exception. A bounded scene-readiness wait was added to the fixture.
2. The next attempt reported passed=True but had only 40 label observations. Visual inspection found pause screens caused by focus/cursor state; this run is explicitly rejected as acceptance evidence. The fixture now rejects pause/noncombat states and checks the exact expected visible-label count per sample. Native automated rendering uses batch mode to avoid focus-driven pause and is not classified as OS interaction.
3. The strict fixture initially failed compilation because its GameState namespace import was missing; this was corrected. No failed package was published. Only the final package and its successful full-coverage native run may qualify for release.

Samples wait 0.16 seconds after explicit target movement; they do not certify every sub-frame transition or human comprehension. The production direction-text update cadence is unchanged.

## Accepted final package

- Product commit a4c06864fb8a52b40b19f126627678052967118c on codex/threat-direction. Clean nondevelopment Unity 6000.5.10f1 Windows build. ZIP 89,617,094 bytes; all 222 archive entries matched the build files individually. SHA256 88a70b9cd95cffe8c3d7a990ec3dce0b26880ce40aa2c29d8709afd7fba09a75.
- Extracted final ZIP, Direct3D12 / RTX 3070, opt-in batch render fixture: passed=True, 144 images, 206 observed labels, 0 opposite-half samples. Exact expected label counts and active/unpaused combat were asserted at every sample. Native 800px rear-crossing, 800px four-target and 1280px four-target images were visually inspected; all displayed actual gameplay and correct-side labels, unlike the excluded paused run. See Native/trace.csv and Native/runtime.txt.
- Separate ordinary 1280x720 launch, without proof or batch flags: actual OS mouse selected Hard, destroyed three tutorial targets, entered combat, and observed natural threat warnings and damage. Ordinary/ sequence records this smoke test. No natural victory completion or HMD acceptance is claimed.
- Player records and non-session preferences restored after both final runs; native-preservation.json and ordinary-preservation.json retained. No audio work or Drive update was performed here.
