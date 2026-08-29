# PC input dependencies / privacy

## Local webcam inference

- MediaPipeUnityPlugin **v0.16.3**, homuler, MIT license.
- Source: https://github.com/homuler/MediaPipeUnityPlugin/tree/v0.16.3
- Release: https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3
- Archive: `com.github.homuler.mediapipe-0.16.3.tgz`
- SHA-256: `CC3E77A219E0B99618AE3BE64C31A566197DEEDC69C1E136ACF52D65D7CF2E79`
- Unity references the local tarball in `DependencyCache/`; run `Tools/Restore-WebcamDependencies.ps1` before opening a fresh clone. The cache is deliberately not committed.
- Model: upstream package's `face_landmarker_v2_with_blendshapes.bytes`, referenced as a scene TextAsset and included in the Windows player. No model download occurs at runtime.
- Runtime: Windows x64, CPU, FaceLandmarker VIDEO mode, single face, default camera request 640×480 / inference 15 FPS. Upstream does not support Windows GPU inference and marks Windows support experimental.
- MediaPipe upstream: https://github.com/google-ai-edge/mediapipe (Apache-2.0); face-landmarker documentation: https://developers.google.com/edge/mediapipe/solutions/vision/face_landmarker
- This application derives approximate gaze from iris positions inside each eye plus its own local five-point affine calibration. Face landmarks are not themselves a vendor-provided calibrated eye tracker.

WebCamTexture pixels and model results stay in process memory. No camera image is written to disk, uploaded, or sent to a server. Preview defaults off. Camera buffers are released on mode change/exit. Calibration is session-only. Face debug shows numeric quality/features, not a recording. The optional mouse-only QA command saves rendered game UI only after stopping the webcam; camera QA writes status numbers only.

Managed exceptions and camera/face timeouts cause fallback. A fatal error in an experimental native library cannot be caught by C#; if a particular machine crashes inside the camera library, launch with `--gaze=mouse --no-webcam` and report its Player.log. This is a PC test path, not a Quest Pro tracking replacement or completed Android validation.

## Korean UI font

Noto Sans KR, SIL Open Font License 1.1. Source font and full license are in `Assets/_Project/PcInput/Fonts/`.
Source: https://github.com/google/fonts/tree/main/ofl/notosanskr
PC labels use a static TextMeshPro SDF atlas generated from the project's UI strings. Existing world-space gameplay UI is retained.

## MediaPipeUnityPlugin MIT notice

MIT License

Copyright (c) 2021 homuler

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
