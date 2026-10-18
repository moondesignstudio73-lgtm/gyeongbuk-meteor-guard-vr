using System.Collections;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Tutorial;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.PcInput
{
    // Preparation only: never advances GameFlow, plays a cue, enables gameplay UI or reads a camera.
    [DisallowMultipleComponent]
    public sealed class ReadyPrewarmController : MonoBehaviour
    {
        public bool IsComplete { get; private set; }
        public int PreparedTextCount { get; private set; }
        public string Stage { get; private set; } = "출격을 준비하고 있어요";
        public int CompletedSteps { get; private set; }
        public int TotalSteps { get; private set; } = 6;
        private static readonly ProfilerMarker TextMarker = new ProfilerMarker("MD.Prewarm.Text");
        private static readonly ProfilerMarker SpecialMeteorMarker = new ProfilerMarker("MD.Prewarm.SpecialMeteor");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var ready = root.GetComponentInChildren<ReadyScreenController>(true);
                if (ready == null) continue;
                if (ready.GetComponent<ReadyPrewarmController>() == null)
                    ready.gameObject.AddComponent<ReadyPrewarmController>();
                return;
            }
        }

        private T InScene<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private IEnumerator Start()
        {
            // Let the title and its existing UI finish their first frame first.
            yield return null;
            yield return null;
            var audio = InScene<AudioManager>();
            Stage = "출격을 준비하고 있어요";
            if (audio != null) yield return audio.PreloadClips();
            CompletedSteps++;
            Stage = "우주선을 준비하고 있어요";
            var spawner = InScene<MeteorSpawner>();
            if (spawner != null) yield return spawner.PrewarmIncrementally();
            CompletedSteps++;
            yield return null;
            using (SpecialMeteorMarker.Auto()) InScene<TutorialController>()?.PrewarmMeteor();
            yield return null;
            using (SpecialMeteorMarker.Auto()) InScene<PracticeController>()?.PrewarmMeteor();
            yield return null;
            using (SpecialMeteorMarker.Auto()) InScene<BossClimaxController>()?.PrewarmMeteor();
            CompletedSteps++;
            yield return null;

            Stage = "우주선을 준비하고 있어요";
            var cockpitDamage = InScene<MeteorDefenseVR.Player.CockpitDamagePresentation>();
            if (cockpitDamage != null) yield return cockpitDamage.PrewarmParticles();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (LaserBeamView beam in root.GetComponentsInChildren<LaserBeamView>(true))
                {
                    if (beam == null) continue;
                    beam.PrewarmGeometry();
                    yield return null;
                }
            }
            CompletedSteps = 4;
            Stage = "잠시만 기다려 주세요";
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                // A QA scene switch can unload a root between GetRootGameObjects and
                // this incremental pass. Treat that as cancellation, not an exception.
                if (root == null) continue;
                // Never enable state roots: that would fire OnEnable/gameplay events.
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text == null) continue;
                    using (TextMarker.Auto())
                    {
                        var bridge = text.transform.parent != null ? text.transform.parent.GetComponent<WorldTextPresentation>() : null;
                        if (bridge != null && bridge.Display == text) bridge.PrewarmVisual(transform);
                        else text.ForceMeshUpdate(true);
                    }
                    PreparedTextCount++;
                    yield return null;
                }
            }
            CompletedSteps = 5;
            // Warm actual Canvas geometry without activating gameplay roots.
            Canvas.ForceUpdateCanvases();
            Stage = "출격 준비 완료!";
            CompletedSteps = TotalSteps;
            IsComplete = true;
        }
    }
}
