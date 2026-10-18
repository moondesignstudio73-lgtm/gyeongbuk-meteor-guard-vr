using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class HologramReadabilityTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { if(Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator TelemetryLinesHaveSeparateVerticalSpace()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            foreach(var scan in Object.FindObjectsByType<MeteorDefenseVR.Visual.CockpitHologramScan>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var marker=scan.ScanLine;
                Assert.That(marker,Is.Not.Null);
                float right=marker.anchoredPosition.x+marker.rect.width*.5f;
                foreach(var label in marker.parent.GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(right,Is.LessThan(label.rectTransform.anchoredPosition.x),"Scan marker must remain left of telemetry text: "+label.name);
            }
            foreach(string name in new[]{"LiveShipStatus","LiveSystemsStatus","LiveThreatAnalysis","LiveWeaponStatus"})
            {
                var text=Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name==name);
                text.ForceMeshUpdate(true);
                Debug.Log($"[HologramReadability] {name} font={text.fontSize} spacing={text.lineSpacing} rect={text.rectTransform.rect} lines={text.textInfo.lineCount}");
                float previousBottom=float.PositiveInfinity;
                for(int i=0;i<text.textInfo.lineCount;i++)
                {
                    var line=text.textInfo.lineInfo[i];
                    Debug.Log($"[HologramReadability] {name} line{i}: top={line.ascender:F3} bottom={line.descender:F3}");
                    if(i>0) Assert.That(previousBottom-line.ascender,Is.GreaterThanOrEqualTo(0),$"{name}: lines {i-1}/{i} overlap");
                    previousBottom=line.descender;
                }
                Assert.That(text.isTextTruncated,Is.False,name+" must show every status line");
            }
        }
    }
}
