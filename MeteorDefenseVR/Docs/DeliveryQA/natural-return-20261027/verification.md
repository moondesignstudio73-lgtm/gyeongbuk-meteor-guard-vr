# Ordinary public-player natural loss, return and replay

Product remains `00b1fd6e94efdd8ef3da04b53a2bf6c116217e61`, release `windows-threat-safe-area-20261027`. Used the separate public ZIP extraction previously verified against all 222 archive files. ZIP SHA256 was checked again before launch: `19776735694688f462d815c562cde3bb83b4e75ad621189c812533efcae5556f`.

One ordinary Windows process, 1280x720 client (1282x752 OS captures), RTX 3070 / Direct3D 12. Arguments were mouse/no-webcam, window dimensions/fullscreen off and log destination only. No QA harness flags, state/damage injection, development hotkeys or synthetic InputSystem events. Agent OS mouse input selected Hard, completed training and later selected replay. The first combat was left to take real meteor damage and fail. This is agent-operated PC evidence, not human gameplay or HMD acceptance.

## Evidence sequence

`sequence.json` records 101 original JPEG screenshots in capture order, with UTC timestamps recorded after each capture/save. `manifest.json` hashes every image. These are timestamped samples, **not continuous video or game-frame identifiers**. There are gaps between capture batches. The key loss/return batch, samples 48–77, has intervals of 0.347–0.448 seconds (mean 0.3663 seconds).

| Samples | Observed behavior |
|---|---|
| 0–7 | Ready, Hard selection, ordinary start, training and three practice targets completed |
| 8–47 | Combat samples with score 0 and shield decreasing through real impacts; lower left/right telemetry stays separate |
| 48–49 | 20% shield, damage 80, nearby threats and red danger accents |
| 50–52 | 10% shield, damage 90; short “위험!” banner on dark backing |
| 53–54 | Full long warning “방어막이 약해졌어요! 운석을 막아 주세요.” above and between the lower side panels, visibly legible without clipping |
| 55 | Long warning fades before the final impact/return transition |
| 56 | First blended return sample: cockpit cracks/red accents still visible through the cinematic fade; combat text panels already absent |
| 57–61 | Craft approaches/moves inward through the open hangar; no combat warning text or damage cracks remain visible |
| 62–67 | Interior approach/landing portion and drone scan, followed by wider view; sample 66 shows the landing gear at the deck. The exact contact instant is between samples, not asserted |
| 68–69 | Hangar doors closing |
| 70–77 | Failed result, score 0, destroyed 0, accuracy 0%, rank C; doors form the closed backdrop; no old threat or low-health warning remains |
| 78 | A result Replay click was attempted, but the captured destination is already the hangar menu. The preceding capture gap is 17.52 seconds, exceeding the result timeout. Do not count this as a successful result-button selection |
| 79 | OS selection of the hangar's Replay control produces “출격 준비” |
| 80–99 | Back in combat with shield initially 100%, score 0 and 21/21 meteors; subsequent real impacts reduce shield. The 79→80 gap is 13.85 seconds, so departure cinematic/countdown were not captured in this replay |
| 100 | First deliberate post-replay mouse-look input changes the camera view, while fixed lower panels remain in place. No successful weapon hit is claimed for this input |

All samples 48–72 and representative earlier/result/replay samples were individually visually inspected. Not every one of the 101 samples was separately reviewed at full size. The archive retains all samples for review.

## Findings and practical limits

No newly established overlap, clipping, sustained contrast failure or post-return warning residue was reproduced in the reviewed samples. The long warning in 53–54 stays above the lower panels; 55 is its fade-out. The short danger banner in 50–51 is separate from those lower panels. Lower status/target text remains readable against dark backing. The transitional cracks/red accents visible at 56 are gone by 57 and do not persist through landing/result. Settings entry controls remain visible along the bottom during the cinematic; they do not cover the reviewed craft/door action or result text in this 1280x720 view. No modal was opened during this sequence.

This does not establish every-frame correctness, numeric contrast compliance, every camera angle, other resolutions, full campaign behavior, human readability, or a successful direct result Replay click. Exact return smoke/damage animation and the precise landing contact are not separately accepted. Replay preparation and subsequent combat/first look input are observed, but the intervening departure animation/countdown remains a capture gap. No repeated run was substituted for this single natural-loss bundle.

The engine's GameFlow logs are compiled only for Editor/development builds, so this normal player emitted no state-transition trace. `runtime-summary.txt` contains only allowlisted graphics lines; `run-summary.json` records zero matches for Exception/Error/Assertion-failed/crash patterns in the private player log. That absence is not proof of an error-free game. State descriptions above come from visible screenshots and ordered OS actions, not invented engine events. Raw player logs are kept private.

## Preservation and disposition

User primary/backup/temp record files and non-session settings were backed up before launch. A valid empty ranking document isolated user data; it did not control combat state. After the final observation, only the verified launched process was stopped. Original records were restored and hash-compared; every non-session preference was restored and compared. `preservation.json` confirms completion at 2026-10-27T01:06:57.1673894Z. User ProjectSettings stayed SHA256 `A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89`.

Evidence/coverage update only: no product changes, build, release or Drive-link change. Previously included hangar graphics are not new work in this review. Actual audio listening, HMD and Suno approval/work remain incomplete; no approval was bypassed. The arbitrary-direction intuition gap of fixed threat-side slots remains open. The already established native-2560 capture limitation was not retried here.
