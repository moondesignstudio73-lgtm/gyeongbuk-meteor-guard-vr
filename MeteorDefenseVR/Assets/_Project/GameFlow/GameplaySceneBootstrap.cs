using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameplaySceneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (GameFlowManager.Instance != null) return;
            GameObject runtimeFlow = new GameObject("GameFlowRuntime");
            runtimeFlow.AddComponent<GameFlowManager>();
        }
    }
}
