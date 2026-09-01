using System.Collections;
using System.IO;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Tutorial;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class TutorialSafeZoneTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [Test]
        public void AuthoredTutorialUsesPlayerHudSafeZoneAndHidesWorldPresentation()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var view=Object.FindAnyObjectByType<TutorialSafeZoneView>(FindObjectsInactive.Include);var camera=Camera.main;
            Assert.That(view,Is.Not.Null);Assert.That(view.HudCanvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(view.HudCanvas.transform.parent,Is.Null,"Desktop Screen Space HUD must not inherit rapid camera rotations");
            Assert.That(view.HudCanvas.worldCamera,Is.SameAs(camera));Assert.That(view.HudCanvas.planeDistance,Is.InRange(.4f,.5f));
            Assert.That(view.HudCanvas.sortingOrder,Is.GreaterThanOrEqualTo(220));
            RectTransform panel=view.SafePanel;Assert.That(panel.anchorMin.x,Is.EqualTo(.19f).Within(.001));Assert.That(panel.anchorMax.x,Is.EqualTo(.81f).Within(.001));
            Assert.That(panel.anchorMin.y,Is.GreaterThanOrEqualTo(.45f));Assert.That(panel.anchorMax.y,Is.LessThanOrEqualTo(.80f));
            Assert.That(panel.anchorMin.y,Is.GreaterThan(.55f),"Main tutorial copy must clear the center reticle/meteor zone");
            int hudLayer=LayerMask.NameToLayer("Player HUD");Assert.That(view.gameObject.layer,Is.EqualTo(hudLayer));Assert.That(camera.cullingMask&(1<<hudLayer),Is.Not.Zero);
            Assert.That(view.InstructionText.font,Is.Not.Null);Assert.That(view.InstructionText.font.name,Does.Contain("PcTestKorean"));
            Assert.That(view.SafePanel.GetComponent<UnityEngine.UI.Image>().material.name,Is.EqualTo("GameplayHudAlwaysOnTop"));
            Assert.That(view.InstructionText.fontSharedMaterial.name,Is.EqualTo("GameplayHudFontAlwaysOnTop"));
            Assert.That(view.InstructionText.fontSharedMaterial.renderQueue,Is.GreaterThanOrEqualTo(4000));
            Assert.That(view.InstructionText.canvasRenderer.cullTransparentMesh,Is.False);
            var legacyStatus=Object.FindAnyObjectByType<TutorialStatusView>(FindObjectsInactive.Include);
            var bridge=legacyStatus.GetComponent<WorldTextPresentation>();
            Assert.That(bridge.enabled,Is.False,"The world-space TMP bridge must never compete with the safe-zone Canvas");
            Assert.That(view.SuppressedWorldRenderers.Count,Is.GreaterThanOrEqualTo(2));
            view.SuppressWorldPresentation();
            foreach(Renderer legacy in view.SuppressedWorldRenderers)
            {Assert.That(legacy.enabled,Is.False,legacy.name);Assert.That(legacy.forceRenderingOff,Is.True,legacy.name);}
            view.ApplyCanvasMode(true);Assert.That(view.HudCanvas.renderMode,Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(view.HudCanvas.transform.localPosition.z,Is.EqualTo(view.VrDepth).Within(.001));
            view.ApplyCanvasMode(false);Assert.That(view.HudCanvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceCamera));
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator TutorialRemainsInSafeZoneThroughSixWindowsVrViews()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);yield return new EnterPlayMode();yield return null;
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.WindowsVRSimulation);
            var simulation=router.Simulation;var camera=Camera.main;var flow=GameFlowManager.Instance;flow.StartTutorial();yield return new WaitForSecondsRealtime(.25f);
            var view=Object.FindAnyObjectByType<TutorialSafeZoneView>();var tutorial=Object.FindAnyObjectByType<TutorialController>();
            Assert.That(view.IsVisible,Is.True);Assert.That(view.CurrentInstruction,Is.EqualTo(tutorial.Instruction));Assert.That(view.InstructionText.text,Does.Contain("LOCK ON"));
            foreach(Renderer legacy in view.SuppressedWorldRenderers)legacy.enabled=true;
            Object.FindAnyObjectByType<TutorialStatusView>(FindObjectsInactive.Include).SetInstruction(tutorial.Instruction);
            foreach(Renderer legacy in view.SuppressedWorldRenderers)
            {Assert.That(legacy.enabled,Is.False,"Tutorial text changes must not revive "+legacy.name);Assert.That(legacy.forceRenderingOff,Is.True,legacy.name);}
            TextMeshProUGUI lowerHint=null;foreach(var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))if(text.name=="InputHelp"){lowerHint=text;break;}
            Assert.That(lowerHint,Is.Not.Null);Assert.That(lowerHint.rectTransform.anchorMax.y,Is.EqualTo(0).Within(.001));
            Assert.That(view.SafePanel.anchorMin.y,Is.GreaterThan(.55f),"Bottom hint and main tutorial panel use separate bands");
            using(var render=new PcStabilityProbe(camera))
            {
                foreach((string name,float yaw,float pitch) in new[]{("Front",0f,0f),("Left",-90f,0f),("Right",90f,0f),("Rear",180f,0f),("Above",0f,65f),("Below",0f,-65f)})
                {
                    simulation.Recenter();simulation.ApplyLook(new Vector2(yaw/.1f,-pitch/.09f),1);yield return new WaitForSecondsRealtime(.12f);Canvas.ForceUpdateCanvases();
                    Assert.That(view.IsVisible,Is.True,name);Assert.That(view.SafePanel.anchorMin.x,Is.EqualTo(.19f).Within(.001),name);Assert.That(view.SafePanel.anchorMax.x,Is.EqualTo(.81f).Within(.001),name);
                    Assert.That(view.CurrentInstruction,Is.EqualTo(tutorial.Instruction),name);
                    // Capture only after an explicit final render. The QA driver and the simulated
                    // look controller both render in LateUpdate; reading between those callbacks can
                    // otherwise save the previous camera orientation's transient Canvas mesh.
                    view.InstructionText.ForceMeshUpdate(true,true);Canvas.ForceUpdateCanvases();
                    bool rendered=false;for(int attempt=0;attempt<4&&!rendered;attempt++){camera.Render();rendered=TutorialPixelsVisible(camera);}
                    Assert.That(rendered,Is.True,name+" tutorial pixels");
                    Assert.That(view.InstructionText.textInfo.characterCount,Is.GreaterThan(40),name+" combined text mesh");
                    string folder=Path.Combine("Docs/TutorialSafeZoneQA",name);Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);
                }
            }
            yield return new ExitPlayMode();
        }

        private static bool TutorialPixelsVisible(Camera camera)
        {
            RenderTexture target=camera.targetTexture,previous=RenderTexture.active;if(target==null)return false;
            var sample=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target;sample.ReadPixels(new Rect(0,0,target.width,target.height),0,0);sample.Apply(false,false);
                Color32[] pixels=sample.GetPixels32();int hits=0;
                int minX=Mathf.RoundToInt(target.width*.26f),maxX=Mathf.RoundToInt(target.width*.74f);
                int minY=Mathf.RoundToInt(target.height*.60f),maxY=Mathf.RoundToInt(target.height*.76f);
                for(int y=minY;y<maxY&&hits<120;y++)for(int x=minX;x<maxX&&hits<120;x++)
                {Color32 c=pixels[y*target.width+x];if(c.g>105&&c.b>105&&c.r>45)hits++;}
                return hits>=120;
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(sample);}
        }
    }
}
