using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class BootstrapSceneLoader : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "MeteorDefense";
        [SerializeField] private bool loadAdditively = true;
        public float Progress { get; private set; }
        public bool IsDone { get; private set; }

        private IEnumerator Start()
        {
            // Present the lightweight bootstrap UI before asset integration begins.
            yield return null;
            Scene gameplay = SceneManager.GetSceneByName(gameplaySceneName);
            if (!gameplay.IsValid() || !gameplay.isLoaded)
            {
                LoadSceneMode mode = loadAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
                var previousPriority = Application.backgroundLoadingPriority;
                try
                {
                    Application.backgroundLoadingPriority = ThreadPriority.Low;
                    AsyncOperation operation = SceneManager.LoadSceneAsync(gameplaySceneName, mode);
                    while (operation != null && !operation.isDone)
                    {
                        Progress = Mathf.Clamp01(operation.progress / .9f);
                        yield return null;
                    }
                }
                finally { Application.backgroundLoadingPriority = previousPriority; }
                gameplay = SceneManager.GetSceneByName(gameplaySceneName);
            }

            // Additive loading keeps Bootstrap active unless explicitly changed. The active
            // scene supplies the skybox and ambient lighting, even when its camera is elsewhere.
            if (gameplay.IsValid() && gameplay.isLoaded) SceneManager.SetActiveScene(gameplay);
            IsDone = gameplay.IsValid() && gameplay.isLoaded;
            Progress = IsDone ? 1f : Progress;
        }
    }
}
