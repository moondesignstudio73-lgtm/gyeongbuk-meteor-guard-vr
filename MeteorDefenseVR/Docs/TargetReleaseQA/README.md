# Target release regression QA

## Fix scope

- Targeting is invalidated before destruction, explosion, or pool return.
- Gaze, turret, lock reticle, HUD feedback, spectator latch, threat indicators, and radar blips release the defeated meteor synchronously.
- Reticle views explicitly reset progress and visibility before reuse.

## Test results

- Focused target-release regression: 3 passed, 0 failed.
- Full EditMode regression: 362 total, 358 passed, 2 failed, 2 skipped.
  - The spectator failure expected the removed 0.35-second `LOCK ON` latch. Its expectation was updated to the required immediate `TARGET SEARCH` state; focused rerun passed 1/1.
  - The Windows simulation failure was a nondeterministic pooled auto-fire timeout on cycle 5; unchanged test passed on isolated rerun 1/1.
- Windows build: succeeded with the authored scenes preserved.
- Windows player boot smoke: stayed alive for 20 seconds; 0 NullReference, MissingReference, crash, or assertion lines.

## Evidence

- `focused-results-9.xml`
- `full-results-2.xml`
- `spectator-results.xml`
- `windows-sim-results.xml`
- `windows-build-2.log`
- `player-smoke.log`

## Known issues

- The full test run performs real-time replay loops and PlayerSettings-triggered domain reloads, so it is substantially slower than the focused suite.
- No remaining target-reticle or boss-marker persistence issue was reproduced.
