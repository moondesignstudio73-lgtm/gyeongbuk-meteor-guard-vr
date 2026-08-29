# Cockpit Visual Restore + 360 Interior QA

## Safe restore point

- Branch: `codex/cockpit-visual-restore`
- Pre-change snapshot: `4d07eb4 chore: snapshot latest gameplay before cockpit visual restore`
- No whole-project reset or historical scene checkout was used.

## Regression cause

`CommercialVisualPolishSetup.RestoreClassicCockpitOnly()` correctly re-enabled the five-monitor `PremiumCockpit`, but it disabled every `PanoramicCanopy` child except `CanopyGlass360`. That removed the metal shell, navigation lights, and rear visual context, leaving the 180-degree view as an exposed skybox with a sparse service shelf.

## Restored visual layer

- Preserved the established wrapped five-monitor console (`PortSecondary`, `PortPrimary`, `Navigation`, `StarboardPrimary`, `StarboardSecondary`).
- Re-enabled the four decorative avionics screens and kept only `Navigation/LiveRadar` as a live physical monitor.
- Added a closed rear pressure shell with rear bulkheads, framed rear observation glass, ceiling spine/ribs, floor bridges, bounded floor glass, side observation glass, guide lights, and mechanical panels.
- Kept `PanoramicCanopy/CanopyGlass360` for low-cost side/roof/floor glazing while leaving the intrusive replacement cage disabled.
- Saved the visual snapshot as `Assets/Art/Premium/Prefabs/CockpitVisual.prefab`.

## Systems layer

- Runtime telemetry now lives under `CockpitGeometry/CockpitSystems`, separate from `PremiumCockpit`.
- Left hologram: live Shield, Hull, Engine, Damage, gaze/sensor diagnostics.
- Right hologram: live contacts, threat vector/range, score, top/bottom turret, target and firing state.
- Holograms are visible only during Launch, Countdown, Playing, and Boss states; Tutorial and menus remain uncluttered.
- Normal opacity is subdued; damage and time-to-collision raise the relevant panel opacity and pulse.
- Existing radar world projection, lock candidate, view cone, Boss marker, damage presentation, turret authority, laser, and reset logic are reused rather than duplicated.

## Visual proof

Before and after captures are stored under:

- `Docs/CockpitRestoreQA/Before/Front.png`
- `Docs/CockpitRestoreQA/Before/Left.png`
- `Docs/CockpitRestoreQA/Before/Right.png`
- `Docs/CockpitRestoreQA/Before/Rear.png`
- `Docs/CockpitRestoreQA/After/Front/stability-game-render.png`
- `Docs/CockpitRestoreQA/After/Left/stability-game-render.png`
- `Docs/CockpitRestoreQA/After/Right/stability-game-render.png`
- `Docs/CockpitRestoreQA/After/Rear/stability-game-render.png`

## Verification

- Focused cockpit/radar/turret tests: 31 passed / 0 failed.
- Four-heading Playing-state render test: 1 passed / 0 failed.
- Full Unity regression: 358 total / 356 passed / 0 failed / 2 explicitly skipped.
  - Difficulty soak requires the explicit `--difficulty-soak` option.
  - Startup hitch capture requires the explicit `--hitch-profile` option.
- Windows current-scene build: succeeded.
- Windows player smoke: remained alive for 20 seconds; no NullReference, MissingReference, fatal exception, or crash.

## Device-side check

Real HMD lens readability and the subjective side-hologram distance still require a final event-headset check. Automated QA covers Windows VR Simulation, camera rotation, runtime bindings, and rendered 0/90/180/270-degree views.
