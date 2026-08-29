using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using MeteorDefenseVR.EyeTracking;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(-300)]
    public sealed class WebcamGazeProvider : MonoBehaviour, IGazeProvider, IHeadRotationSource, IBlinkSource
    {
        [SerializeField] private Camera gazeCamera;
        [SerializeField] private TextAsset faceLandmarkerModel;
        [SerializeField] private string preferredCameraName;
        [SerializeField, Range(5, 30)] private int inferenceFramesPerSecond = 15;
        [SerializeField, Range(.01f, .5f)] private float gazeSmoothing = .12f;
        [SerializeField, Range(.4f, 2f)] private float gazeSensitivity = 1f;
        [SerializeField, Range(0f, .04f)] private float gazeDeadzone = .006f;
        [SerializeField, Range(.3f, .95f)] private float minimumTrackingConfidence = .55f;
        [SerializeField, Range(.05f, .4f)] private float trackingGracePeriod = .18f;
        [SerializeField] private bool showWebcamPreview;
        [SerializeField] private bool showFaceDebug;
        private WebCamTexture webcam;
        private Texture2D cpuTexture;
        private Color32[] pixels, uprightPixels;
        private FaceLandmarker landmarker;
        private FaceLandmarkerResult result;
        private Coroutine startup;
        private float lastValidTime = -100f, nextInference, lastInference;
        private float lastFrameTime;
        private long timestamp;
        private bool captureRequested;
        private float lastHeadTime = -100f;
        private float lastBlinkTime = -100f;
        public Quaternion HeadRotation { get; private set; } = Quaternion.identity;
        public bool HasFreshHeadRotation => IsStreaming && Time.unscaledTime - lastHeadTime < .25f;
        public bool HasReliableBlink => IsStreaming && Time.unscaledTime - lastBlinkTime < .25f;
        public bool AreEyesClosed { get; private set; }
        private readonly Vector2[] landmarkBuffer = new Vector2[478];
        public WebcamCalibrationMap Calibration { get; } = new WebcamCalibrationMap();
        public Vector2 RawFeatures { get; private set; } = new Vector2(.5f, .5f);
        public Vector2 GazeViewport { get; private set; } = new Vector2(.5f, .5f);
        public float Confidence { get; private set; }
        public int SampleNumber { get; private set; }
        public bool HasFreshTracking => IsStreaming && Confidence >= minimumTrackingConfidence && Time.unscaledTime - lastValidTime < .25f;
        public bool IsTrackingValid => IsStreaming && Time.unscaledTime - lastValidTime <= trackingGracePeriod;
        public bool IsStreaming => webcam != null && webcam.isPlaying && webcam.width > 16 && landmarker != null;
        public bool IsStarting => startup != null;
        public bool Failed { get; private set; }
        public string Status { get; private set; } = "Webcam off";
        public Texture PreviewTexture => cpuTexture;
        public bool ShowWebcamPreview { get => showWebcamPreview; set => showWebcamPreview = value; }
        public bool ShowFaceDebug { get => showFaceDebug; set => showFaceDebug = value; }
        public void Configure(Camera camera, TextAsset model) { gazeCamera = camera; faceLandmarkerModel = model; }

        public void BeginCapture()
        {
            if (captureRequested || !isActiveAndEnabled) return;
            captureRequested = true;
            Failed = false;
            Status = "Starting webcam...";
            startup = StartCoroutine(StartCamera());
        }
        private IEnumerator StartCamera()
        {
            yield return null;
            // Batch regression tests must never activate the user's camera.
            if (Application.isBatchMode) { Fail("Webcam disabled in automated tests"); yield break; }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                AsyncOperation permission = Application.RequestUserAuthorization(UserAuthorization.WebCam);
                float requestedAt = Time.realtimeSinceStartup;
                while (!permission.isDone && Time.realtimeSinceStartup - requestedAt < 8f) yield return null;
                if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { Fail("Webcam permission denied"); yield break; }
            }
            try
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length == 0) { Fail("No webcam found"); yield break; }
                string deviceName = devices[0].name;
                foreach (WebCamDevice device in devices) if (device.name == preferredCameraName) deviceName = device.name;
                // TextAsset.bytes returns a managed copy. Avoid copying the multi-MB model twice.
                byte[] modelBytes = faceLandmarkerModel != null ? faceLandmarkerModel.bytes : null;
                if (modelBytes == null || modelBytes.Length < 1000) { Fail("Local face model missing"); yield break; }
                landmarker = CreateLocalLandmarker(modelBytes, minimumTrackingConfidence);
                result = FaceLandmarkerResult.Alloc(1, outputFaceTransformationMatrixes: true);
                webcam = new WebCamTexture(deviceName, 640, 480, 30);
                webcam.Play();
            }
            catch (Exception exception) { Fail("Webcam unavailable: " + exception.GetType().Name); yield break; }
            float startTime = Time.realtimeSinceStartup;
            while (webcam != null && (!webcam.didUpdateThisFrame || webcam.width <= 16))
            {
                if (Time.realtimeSinceStartup - startTime > 8f) { Fail("Webcam startup timed out"); yield break; }
                yield return null;
            }
            startup = null;
            lastFrameTime = Time.realtimeSinceStartup;
            lastInference = Time.unscaledTime;
            Status = "Find your face; then calibrate";
#else
            Fail("Webcam test is supported on Windows x64");
            yield break;
#endif
        }

        public static FaceLandmarker CreateLocalLandmarker(byte[] model, float confidence = .55f) => FaceLandmarker.CreateFromOptions(
            new FaceLandmarkerOptions(new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: model),
                RunningMode.VIDEO, numFaces: 1, minFaceDetectionConfidence: confidence,
                minFacePresenceConfidence: confidence, minTrackingConfidence: confidence, outputFaceTransformationMatrixes: true));

        private void Update()
        {
            // A disconnected/stalled device can keep isPlaying=true. Do not wait forever at Boot.
            if (captureRequested && startup == null && webcam != null)
            {
                if (webcam.didUpdateThisFrame) lastFrameTime = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - lastFrameTime > 5f)
                { Fail("Webcam frames unavailable - mouse fallback. Retry webcam when ready."); return; }
            }
            if (!IsStreaming || !webcam.didUpdateThisFrame || Time.unscaledTime < nextInference) return;
            nextInference = Time.unscaledTime + 1f / inferenceFramesPerSecond;
            try
            {
                pixels = webcam.GetPixels32(pixels);
                bool rotated = webcam.videoRotationAngle % 180 != 0;
                int width = rotated ? webcam.height : webcam.width;
                int height = rotated ? webcam.width : webcam.height;
                if (uprightPixels == null || uprightPixels.Length != pixels.Length) uprightPixels = new Color32[pixels.Length];
                CopyUpright(pixels, uprightPixels, webcam.width, webcam.height, webcam.videoRotationAngle, webcam.videoVerticallyMirrored);
                if (cpuTexture == null || cpuTexture.width != width || cpuTexture.height != height)
                {
                    if (cpuTexture != null) Destroy(cpuTexture);
                    cpuTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                }
                cpuTexture.SetPixels32(uprightPixels);
                if (showWebcamPreview) cpuTexture.Apply(false, false);
                using (var input = new Mediapipe.Image(cpuTexture))
                {
                    timestamp = Math.Max(timestamp + 1, (long)(Time.realtimeSinceStartupAsDouble * 1000));
                    bool detected = landmarker.TryDetectForVideo(input, timestamp, null, ref result);
                    Confidence = 0f;
                    if (detected && result.facialTransformationMatrixes != null && result.facialTransformationMatrixes.Count > 0 &&
                        WebcamHeadTrackingProvider.TryConvertFaceMatrix(result.facialTransformationMatrixes[0], out var headRotation))
                    { HeadRotation = headRotation; lastHeadTime = Time.unscaledTime; }
                    if (detected && result.faceLandmarks != null && result.faceLandmarks.Count > 0)
                    {
                        var landmarks = result.faceLandmarks[0].landmarks;
                        if (landmarks.Count >= 478)
                        {
                            for (int i = 0; i < 478; i++) landmarkBuffer[i] = new Vector2(landmarks[i].x, landmarks[i].y);
                            UpdateBlinkState(landmarkBuffer);
                            if (TryExtractEyeFeatures(landmarkBuffer, out Vector2 raw, out float quality))
                            {
                                Confidence = quality;
                                if (quality >= minimumTrackingConfidence)
                                {
                                    RawFeatures = raw;
                                    Vector2 estimated = (Calibration.Map(raw) - Vector2.one * .5f) * gazeSensitivity + Vector2.one * .5f;
                                    estimated = new Vector2(Mathf.Clamp01(estimated.x), Mathf.Clamp01(estimated.y));
                                    GazeViewport = WebcamCalibrationMap.Smooth(GazeViewport, estimated, Time.unscaledTime - lastInference, gazeSmoothing, gazeDeadzone);
                                    lastValidTime = Time.unscaledTime;
                                    SampleNumber++;
                                    Status = Calibration.IsCalibrated ? "Webcam gaze (approximate)" : "Webcam ready - calibration needed";
                                }
                            }
                        }
                    }
                    if (Confidence < minimumTrackingConfidence) Status = "Webcam tracking lost - face camera / open eyes";
                }
                lastInference = Time.unscaledTime;
            }
            catch (Exception exception) { Fail("Webcam inference unavailable: " + exception.GetType().Name); }
        }

        public Ray GetGazeRay() => gazeCamera != null ? gazeCamera.ViewportPointToRay(GazeViewport) : new Ray(transform.position, transform.forward);
        public Vector3 GetGazeOrigin() => GetGazeRay().origin;
        public Vector3 GetGazeDirection() => GetGazeRay().direction;
        public void Fail(string reason) { Failed = true; Status = reason; ReleaseCapture(); }
        public void StopCapture() { Status = "Webcam off"; ReleaseCapture(); }
        private void OnDisable() => StopCapture();
        private void ReleaseCapture()
        {
            captureRequested = false;
            if (startup != null) StopCoroutine(startup);
            startup = null;
            if (webcam != null) { webcam.Stop(); Destroy(webcam); webcam = null; }
            if (landmarker != null) { ((IDisposable)landmarker).Dispose(); landmarker = null; }
            if (cpuTexture != null) { Destroy(cpuTexture); cpuTexture = null; }
            if (pixels != null) Array.Clear(pixels, 0, pixels.Length);
            if (uprightPixels != null) Array.Clear(uprightPixels, 0, uprightPixels.Length);
            pixels = uprightPixels = null;
            Array.Clear(landmarkBuffer, 0, landmarkBuffer.Length);
            result = default;
            RawFeatures = GazeViewport = new Vector2(.5f, .5f);
            SampleNumber = 0;
            nextInference = lastInference = 0;
            Calibration.Reset();
            Confidence = 0f;
            lastValidTime = -100f;
            lastHeadTime = -100f; HeadRotation = Quaternion.identity;
            lastBlinkTime = -100f; AreEyesClosed = false;
        }

        private void UpdateBlinkState(IReadOnlyList<Vector2> points)
        {
            float leftWidth = Vector2.Distance(points[33], points[133]);
            float rightWidth = Vector2.Distance(points[362], points[263]);
            if (leftWidth < .01f || rightWidth < .01f) return;
            float leftOpening = Vector2.Distance(points[159], points[145]) / leftWidth;
            float rightOpening = Vector2.Distance(points[386], points[374]) / rightWidth;
            AreEyesClosed = leftOpening < .105f && rightOpening < .105f;
            lastBlinkTime = Time.unscaledTime;
        }

        // Iris positions are measured relative to each eye, not face position alone.
        public static bool TryExtractEyeFeatures(IReadOnlyList<Vector2> points, out Vector2 feature, out float quality)
        {
            feature = default; quality = 0f;
            if (points == null || points.Count < 478) return false;
            if (!EyeFeature(points[33], points[133], points[159], points[145], points[468], out Vector2 left, out float q1)) return false;
            if (!EyeFeature(points[362], points[263], points[386], points[374], points[473], out Vector2 right, out float q2)) return false;
            feature = (left + right) * .5f;
            quality = Mathf.Min(q1, q2);
            return WebcamCalibrationMap.Finite(feature);
        }
        private static bool EyeFeature(Vector2 a, Vector2 b, Vector2 top, Vector2 bottom, Vector2 iris, out Vector2 feature, out float quality)
        {
            feature = default; quality = 0f;
            Vector2 axis = b - a;
            float width = axis.magnitude;
            float opening = Vector2.Distance(top, bottom);
            if (width < .015f || opening / width < .1f) return false;
            Vector2 vertical = new Vector2(-axis.y, axis.x).normalized;
            if (vertical.y < 0f) vertical = -vertical;
            float x = Vector2.Dot(iris - a, axis.normalized) / width;
            float y = .5f + Vector2.Dot(iris - (top + bottom) * .5f, vertical) / opening;
            if (x < .1f || x > .9f || y < -.2f || y > 1.2f) return false;
            feature = new Vector2(x, y);
            // Geometry quality supplements MediaPipe's configured detection/presence thresholds.
            quality = Mathf.Clamp01((opening / width - .08f) / .12f);
            return true;
        }
        public static void CopyUpright(Color32[] source, Color32[] destination, int width, int height, int rotation, bool mirroredVertically)
        {
            rotation = (rotation % 360 + 360) % 360;
            int outWidth = rotation % 180 == 0 ? width : height;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int topY = mirroredVertically ? y : height - 1 - y;
                int dx = x, dy = topY;
                if (rotation == 90) { dx = height - 1 - topY; dy = x; }
                else if (rotation == 180) { dx = width - 1 - x; dy = height - 1 - topY; }
                else if (rotation == 270) { dx = topY; dy = width - 1 - x; }
                destination[dy * outWidth + dx] = source[y * width + x];
            }
        }
    }
}
