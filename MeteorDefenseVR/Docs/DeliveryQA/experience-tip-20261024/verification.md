# First Experience tip lifecycle
Baseline ordinary public product531b0d7. This audit uses fresh views/scenes, supported Resources DifficultySettings default Experience+lock before runtime scene initialization, or Normal+unlocked and actual synthetic gaze selection. No private state reset: reflection only reads extremeNoticeRemaining. 800x600 and1280x720, separate initial startup, first start-screen selection, first hangar selection. Prepared Result is used for hangar access, not a natural combat round or OS/HMD run.

Before: six observation tests complete with validate=false. Both locked defaults first show after about5.2s with tip remaining0, hidden remaining falls fromabout1.65 to0. Thus baseline test Passed means observation completed, not product defect absent. Unlocked start/hangar first selection tips appear. Before originals retained.

Minimal fix1: RefreshVisibility before timer update; decrement only while panel activeInHierarchy, tip visibility follows panel. Hidden remaining stays1.8; first display retainsabout1.79. Timer-only six validations pass five and fail locked800 on tip/prompt glyph overlap. Additional locked800 observation captures actual simultaneous overlapping text at200ms (TimerOnly/800x600/locked-default/visible-200ms). No private timer reset used.

Minimal fix2: only locked start screens interpolate tip vertical position125 to100 over4:3–16:9; unlocked and hangar positions unchanged. Final two locked Editor regressions passed; their200ms originals individually inspected, both lines separated. Other four start/hangar paths passed in timer-only run and will be rerun in final ordinary executable. Fonts, cards, selection/check/start behavior unchanged.

QA corrections retained: initial compile errors obsolete GetInstanceID and missing Visual import, corrected to GetEntityId/import. Initial selection fixtures failed because the existing .4s screen input gate was not respected; added real .6s wait, did not modify gate. A second baseline was interrupted before completion to correct the missing start wait. Initial XML and interruption note retained. These are fixture errors, not product input defects. ProjectSettings user edit untouched. Full logs/private preservation snapshots excluded.

Normal release ZIP build and six fresh-process checks pending. Existing26-state lock suite not repeated. Suno and shared reviewer state untouched.
