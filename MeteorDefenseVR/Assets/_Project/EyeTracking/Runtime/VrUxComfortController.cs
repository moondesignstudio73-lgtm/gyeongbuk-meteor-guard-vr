using System;
using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    [DisallowMultipleComponent]
    public sealed class VrUxComfortController : MonoBehaviour
    {
        [SerializeField] private VrUxSettings settings;
        [SerializeField] private Camera vrCamera;
        [SerializeField] private GazeRaycaster gazeRaycaster;
        [SerializeField] private MeteorSpawner meteorSpawner;
        [SerializeField] private TextMesh[] readableTexts = Array.Empty<TextMesh>();

        public bool IsApplied { get; private set; }

        private void Start() => ApplySettings();

        public void Configure(
            VrUxSettings uxSettings,
            Camera camera,
            GazeRaycaster raycaster,
            MeteorSpawner spawner,
            TextMesh[] texts)
        {
            settings = uxSettings;
            vrCamera = camera;
            gazeRaycaster = raycaster;
            meteorSpawner = spawner;
            readableTexts = texts ?? Array.Empty<TextMesh>();
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (settings == null) return;
            if (gazeRaycaster != null) gazeRaycaster.GazeMagnetismRadius = settings.GazeMagnetismRadius;
            meteorSpawner?.SetSpawnLimits(settings.HorizontalSpawnLimit, settings.VerticalSpawnLimit);
            if (vrCamera != null)
            {
                vrCamera.nearClipPlane = Mathf.Min(vrCamera.nearClipPlane, 0.05f);
                vrCamera.farClipPlane = Mathf.Max(vrCamera.farClipPlane, 60f);
            }
            EnforceReadableTextSize(readableTexts, settings.MinimumTextSize);
            IsApplied = true;
        }

        public static void EnforceReadableTextSize(TextMesh[] texts, float minimumSize)
        {
            if (texts == null) return;
            float safeMinimum = Mathf.Max(0.025f, minimumSize);
            foreach (TextMesh text in texts)
                if (text != null && text.characterSize < safeMinimum) text.characterSize = safeMinimum;
        }
    }
}
