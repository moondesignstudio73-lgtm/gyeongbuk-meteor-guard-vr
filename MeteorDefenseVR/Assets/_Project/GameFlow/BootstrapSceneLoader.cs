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

        private IEnumerator Start()
        {
            Scene existing = SceneManager.GetSceneByName(gameplaySceneName);
            if (existing.IsValid() && existing.isLoaded) yield break;
            LoadSceneMode mode = loadAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
            AsyncOperation operation = SceneManager.LoadSceneAsync(gameplaySceneName, mode);
            if (operation != null) yield return operation;
        }
    }
}
