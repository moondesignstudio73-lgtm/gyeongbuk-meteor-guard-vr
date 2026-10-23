# Readable menu text follow-up — in progress
Review rejected adoption of release56b81ea: disabled text became darker than baseline and800 difficulty descriptions / record footer remained too small. Previous14tests did not establish readability. Old release will remain unchanged. Scope restore required disabled/selected text contrast; enlarge/reflow difficulty descriptions and records lower notices. Preserve user ProjectSettings/HUD/gaze/audio and Suno pending.
Plan: leave label colors intact; use neutral surfaces and an amber edge marker for disabled state. Enlarge difficulty cards/font within wider panels without removing words; expand record panel and lower notice rows. Review actual800/wide originals before final build; run affected inputs/bounds/gaze reentry, fresh extraction and separate release.

Initial revised render2/2 tests passed. Actual800 images show readable difficulty descriptions and record storage/page notices; disabled labels retain original contrast with amber edge. Refinement: explicit semantic line breaks keep 우주/운석/차근차근 intact; move experience notice clear of cards; use wider records panel for readable tabs/column headers/body too, keeping footer space. Extend final fixture with actual view minimum/maximum controls at both sizes (20states total) and boundary assertions.

Visual review rejected intermediate wide record frame: top decorative border leaves viewport after expansion (saved intermediate-wide-frame-clip.png). Required labels are visible but do not accept final layout until fixed. Planned decorative-frame downward offset preserves content/collider positions and avoids introducing a gaze transition geometry change. Final render pending.

Regression13/14 passed; disabled-feedback lifecycle failed after hide/show because state remains Normal until next Update. Added immediate OnEnable Refresh so a disabled control reopens visibly disabled without a transient normal state. No label tinting/input change. Shifted decorative records frame/glass/rail down65units; content/colliders stay fixed. Retest lifecycle/render/UI motion next.

Final-checks3/3 passed, resolving disabled lifecycle failure;14 unique affected tests have passing latest results. Actual800/wide record labels readable and upper border inside viewport. Tightened decorative frame bottom to remain above cockpit (555high, center-22.5, same top255) and added spacing below tab borders to column headings/first row. One final render-only run validates this visual refinement; input/collider positions unchanged.

Final render1/1 passed (20actual scene images, both sizes); minimum start/lobby locked originals now readable with semantic lines and original high-contrast labels; current check retained. Both record originals show readable lower notice, separated table headers, full frame above cockpit. Final executable inspection still pending; tests alone are not visual acceptance. User ProjectSettings remains excluded.

Rejected private candidate f11a426 after direct wide start original:150-high cards at wide projection overlap instruction/prompt and outer card edges touch cockpit pillars (intermediate-wide-start-overlap.png). Not packaged/published. No font reduction: keep300/240 type, narrow card width/spacing slightly and remove extra vertical padding on wide start (150→116 card height), retaining full two-line descriptions. Lobby retains taller cards. Validate wide start plus new collider geometry before a new build.

Final-fit suite passed; actual wide+minimum locked start originals reviewed: instruction/prompt fully visible, card edges clear of cockpit, all description words readable, unchanged300/240 fonts. Same ray/collider menu and difficulty regressions passed. New source build next; f11a426 private candidate remains unshipped.

Final source f624718 built successfully with clean worktree identity; user ProjectSettings restored and compared. Prior f11a426 build not packaged. Remaining acceptance: fresh extracted player20 originals, actual round/menu/pointer regressions, then separate public ZIP and redownload verification.

Package prepared:89,544,996bytes /222files, SHA2565cad44ade5b8c372c1bfb1e9047e3d40125a8ee3c37c27d21152f81e954ebab1. Fresh RMT20261023 extraction/run starting; not public yet.

## Final extracted executable review
- Source f624718cec44e895b14db1252606891ca5d5115b. Fresh ZIP extraction verified all 222 files; one synthetic-input round completed with zero logged errors/warnings and no duplicate callbacks. Menu transition gaze proof passed.
- Visually inspected all 20 final executable PNGs in Extracted: ten states each at 800x600 and 2560x1080 (start selected/locked, retry, unavailable lobby, difficulty selected/locked, records, audio bounds, view minimum/maximum). Wide images were displayed at 2048x864 by the viewer; full-resolution originals retained.
- Required disabled text remains readable with amber edges; selected check remains visible. Enlarged difficulty descriptions and records footer fit without overlap or clipping in these fixtures. Wide start cards clear the heading, prompt and cockpit pillars. No words removed or fonts shrunk to fit.
- Final synthetic InputSystem pointer proof passed all four result exits, modal interception/close, subsequent menu use, and gaze guard checks. This is controlled executable rendering/input verification, not human OS play or HMD validation. Regression tests are supporting evidence, not blanket visual acceptance.
- Prior 56b81ea release remains untouched. The earlier dimmed text/too-small descriptions were rejected; intermediate frame clipping and wide card overlaps were repaired before this package.

## Published delivery
- Release: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-readable-menu-text-20261023 (421344449), source f624718cec44e895b14db1252606891ca5d5115b.
- ZIP: 89,544,996 bytes, 222 files. SHA256 5cad44ade5b8c372c1bfb1e9047e3d40125a8ee3c37c27d21152f81e954ebab1.
- Public asset downloaded again and matched this SHA256 on 2026-10-23T00:55:03Z. Original selection-state release preserved. User ProjectSettings change preserved; no new Suno/audio content.
