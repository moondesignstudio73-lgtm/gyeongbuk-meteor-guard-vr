using UnityEditor;
using UnityEditor.Analytics;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.EngineDiagnostics;

namespace MeteorDefenseVR.Editor
{
    // Project/player settings only. Never change the user's global Editor analytics preference.
    [InitializeOnLoad]
    public sealed class OfflinePlayerBuildPolicy : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        static OfflinePlayerBuildPolicy() => Apply();
        public void OnPreprocessBuild(BuildReport report) => Apply();
        private static void Apply()
        {
            EngineDiagnosticsSettings.enabled = false;
            AnalyticsSettings.enabled = false;
            AnalyticsSettings.initializeOnStartup = false;
        }
    }
}
