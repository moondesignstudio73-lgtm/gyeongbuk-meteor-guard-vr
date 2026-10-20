using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MeteorDefenseVR.Combat;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Opt-in QA only. Created by the soak runner, never by normal gameplay.
    public sealed class PcStabilityProbe : IDisposable
    {
        private readonly Camera camera;
        private readonly RenderTexture target, previousTarget;
        private readonly float previousAspect;
        private readonly Canvas canvas;
        private readonly RenderMode previousMode;
        private readonly Camera previousCanvasCamera;
        private readonly float previousPlane;
        private ProfilerRecorder allocation;
        private readonly PcStabilityRenderDriver driver;
        public int RenderedFrames { get; private set; }
        public bool AllocationCounterAvailable => allocation.Valid || (driver != null && driver.AllocationCounterAvailable);
        public bool ProfilerAllocationCounterAvailable => allocation.Valid;
        public string AllocationMeasurement => allocation.Valid ? "ProfilerRecorder GC Allocated In Frame"
            : driver != null && driver.AllocationCounterAvailable ? "GC.GetAllocatedBytesForCurrentThread delta per LateUpdate; main thread only, includes QA; not all-thread allocation"
            : "Per-frame allocation counter unavailable; total/managed heap and GC collections remain recorded";
        public long LastFrameAllocation => allocation.Valid ? allocation.LastValue : driver != null && driver.AllocationCounterAvailable ? driver.LastFrameMainThreadAllocation : -1;

        public PcStabilityProbe(Camera gameCamera, int width = 1280, int height = 720)
        {
            camera = gameCamera;
            previousTarget = camera.targetTexture; previousAspect = camera.aspect;
            target = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR) { name = "QA_GameOnly_RenderTarget" };
            target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
            var overlay = Object.FindAnyObjectByType<PcTestOverlay>();
            canvas = overlay != null ? overlay.GetComponentInChildren<Canvas>(true) : null;
            if (canvas != null)
            {
                previousMode = canvas.renderMode; previousCanvasCamera = canvas.worldCamera; previousPlane = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
            }
            RenderPipelineManager.endCameraRendering += RenderedByPipeline;
            driver = new GameObject("QA_ExplicitGameCameraRender").AddComponent<PcStabilityRenderDriver>();
            driver.GameCamera = camera;
            driver.RenderCompleted += RenderedByDriver;
            // Establish one real render immediately. Some headless D3D11 editor runs can defer
            // LateUpdate/endCameraRendering for several test frames even though Camera.Render is valid.
            camera.Render();
            if(RenderedFrames==0)RenderedFrames=1;
            allocation = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        }
        private void RenderedByPipeline(ScriptableRenderContext context,Camera renderedCamera)
        {if(renderedCamera==camera)RenderedFrames++;}
        private void RenderedByDriver() => RenderedFrames++;

        public void SaveGameOnlyProof(string directory, GazeInputRouter router)
        {
            // Explicit guard: never serialize a webcam frame, preview, landmark, or face data.
            if (RenderedFrames < 1 || router.Webcam.IsStreaming || router.Webcam.ShowWebcamPreview)
                throw new InvalidOperationException("QA proof requires rendered game camera and webcam disabled");
            RenderTexture previous = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            var displayTarget = RenderTexture.GetTemporary(target.width, target.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                // HDR targets contain linear values in a linear-color project. PNG viewers
                // expect display-encoded RGB; direct readback makes the evidence too dark.
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(target, displayTarget);
                RenderTexture.active = displayTarget;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory, "stability-game-render.png"), image.EncodeToPNG());
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(displayTarget);
                Object.Destroy(image);
            }
        }
        public void Dispose()
        {
            RenderPipelineManager.endCameraRendering -= RenderedByPipeline;allocation.Dispose();
            if (driver != null) { driver.RenderCompleted -= RenderedByDriver; Object.Destroy(driver.gameObject); }
            if (canvas != null) { canvas.renderMode = previousMode; canvas.worldCamera = previousCanvasCamera; canvas.planeDistance = previousPlane; }
            if (camera != null) { camera.targetTexture = previousTarget; camera.aspect = previousAspect; }
            target.Release(); Object.Destroy(target);
        }

        [Serializable] public sealed class SubscriptionSnapshot
        {
            public int callbacks, duplicateCallbacks, coroutineHandles;
            public List<string> duplicates = new List<string>();
        }
        public static SubscriptionSnapshot InspectSubscriptions()
        {
            var snapshot = new SubscriptionSnapshot();
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour == null || behaviour.GetType().Namespace == null || !behaviour.GetType().Namespace.StartsWith("MeteorDefenseVR")) continue;
                foreach (FieldInfo field in behaviour.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.FieldType == typeof(Coroutine) && field.GetValue(behaviour) != null) snapshot.coroutineHandles++;
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType)) Count(field.GetValue(behaviour) as Delegate, behaviour.GetType().Name + "." + field.Name, snapshot);
                }
            }
            foreach (FieldInfo field in typeof(GazeLockOnTarget).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) Count(field.GetValue(null) as Delegate, "GazeLockOnTarget." + field.Name, snapshot);
            return snapshot;
        }
        private static void Count(Delegate handler, string field, SubscriptionSnapshot snapshot)
        {
            if (handler == null) return;
            var unique = new HashSet<Delegate>();
            foreach (Delegate callback in handler.GetInvocationList())
            {
                snapshot.callbacks++;
                if (!unique.Add(callback)) { snapshot.duplicateCallbacks++; snapshot.duplicates.Add(field + ":" + callback.Method.Name); }
            }
        }
    }
}
