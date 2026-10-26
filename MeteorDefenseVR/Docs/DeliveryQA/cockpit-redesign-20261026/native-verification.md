# Native and published package verification

Public release: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-cockpit-redesign-20261026

Product commit `f5234e877b0b0e2d1df8e60ff705b8dfab5b9017`; normal non-development Windows build, clean build identity, Unity 6000.5.10f1. ZIP 89,597,317 bytes / 222 files. SHA-256 `a75b1173a67e71479cc490447e6f7a8770a8f753925e5480880f3b8efaadbe6a`.

The ZIP was independently extracted and every extracted file compared against the build. The presentation process ran 2026-10-26 13:52:47.785Z–13:54:26.021Z, native exit 0, RTX 3070 / Direct3D12. Both first start and forced-result replay reached Playing with 62/63 continuous frames, no observed hangar reversal and zero camera translation. The gallery plus sequence/state captures total 205 images.

The PowerShell runner initially concatenated its one-element presentation argument with `--no-webcam`, giving the output scenario directory `display--no-webcam`. The presentation fixture still ran and passed, but this process must NOT be described as having received a separate `--no-webcam` flag. The script was corrected to use a typed string array. The following soak command had multiple arguments and correctly received the separate flag. The published bytes are unchanged by this runner correction.

The independent soak process ran 13:54:26.025Z–13:58:36.282Z, native exit 0. Two complete synthetic-input combat rounds passed (about 90.9 seconds to result each), followed by an explicitly forced failure/return fixture and power-up display fixtures. These are not two human playthroughs. Preflight and UI fixtures deliberately inject states/damage/buffs and are separate from the two input-driven combat rounds. `pc-soak.json` reports no issues. Power-up simultaneous and expiry views were checked at 800×600 and 1280×720; selected actual render originals were inspected. Return landing and door-closing originals were also inspected. The cinematic return set/model itself predates this update; this update primarily changes the cockpit-visible bay and departure presentation.

Operator preferences and ranking files were compared before/after both processes. Only Unity session bookkeeping changed; no gameplay preferences or rankings required restoration. The user's pre-existing ProjectSettings file was restored byte-for-byte after building (SHA-256 `A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89`).

Published release ID 424319147. Uploaded ZIP/checksum asset sizes and GitHub SHA-256 digests were checked. The public ZIP was downloaded again at 2026-10-26T14:00:34.0929005Z and its size/hash matched the tested ZIP. This is a byte-identity check of the public download, not an additional public-download gameplay run.

The full local frame sets remain under this QA directory; selected originals, continuous replay videos, CSVs and reports are committed. `evidence-manifest.csv` inventories the full local accepted sets. Rejected harness attempts and complete engine logs are retained locally but not published. Actual OS mouse operation, physical webcam endurance and HMD comfort remain outside this verification.
