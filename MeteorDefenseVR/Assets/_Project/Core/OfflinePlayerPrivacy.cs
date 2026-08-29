using UnityEngine;
using UnityEngine.UnityConsent;

namespace MeteorDefenseVR.Core
{
    // Defense in depth; pre-build policy also disables native diagnostics before player startup.
    public static class OfflinePlayerPrivacy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void BeforePlayerStartup()
        {
            EndUserConsent.SetConsentState(new ConsentState
            {
                AdsIntent = ConsentStatus.Denied,
                AnalyticsIntent = ConsentStatus.Denied
            });
        }
    }
}
