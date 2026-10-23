using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Visual;


using UnityEngine;
using UnityEngine.InputSystem;

using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class SelectionStateProof
    {
        private static void Check(bool condition,string message) { if(!condition) throw new System.InvalidOperationException(message); }
        private static IEnumerator WaitForChoices(DifficultySelectionView selection)
        {
            float deadline=Time.unscaledTime+15;
            while(!selection.Choices[0].isActiveAndEnabled && Time.unscaledTime<deadline) yield return null;
            Check(selection.Choices[0].isActiveAndEnabled,"Difficulty screen not ready");
        }        public static IEnumerator Run(string output)
        {
            yield return new WaitForSecondsRealtime(3);
            var reset=Object.FindAnyObjectByType<OperationsResetController>();var flow=GameFlowManager.Instance;
            var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();
            var difficulty=Object.FindAnyObjectByType<DifficultyManager>();var selection=Object.FindAnyObjectByType<DifficultySelectionView>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var result=Object.FindAnyObjectByType<ResultController>();
            var overlay=Object.FindAnyObjectByType<PcTestOverlay>();var health=Object.FindAnyObjectByType<PlayerHealth>();
            float originalSensitivity=router.Simulation.Sensitivity;
            var camera=Camera.main;bool locked=difficulty.Settings.lockDifficultySelection,enabled=caster.enabled;
            var physical=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);
            caster.enabled=false;router.InputSuspended=true;lobby.Ranking.Initialize(flow,result,new LocalRankingStore(null));
            AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(true);
            UnityEngine.UI.Button Button(string name)=>overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name);
            try
            {
                foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    using var render=new PcStabilityProbe(camera,size.x,size.y);
                    IEnumerator Save(string state, System.Action prepare = null)
                    {
                        
                        yield return new WaitForSecondsRealtime(.5f); prepare?.Invoke(); Canvas.ForceUpdateCanvases();camera.GetComponent<WorldUiSafeArea>().Apply();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);
                        var buttons=Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude);
                        File.WriteAllLines(Path.Combine(folder,"state.txt"),new[]{"Controlled scene-camera render with prepared states; not OS/HMD observation. Check enclosing build identity for executable provenance."}.Concat(buttons.Select(b=>$"{b.name}: interactable={b.interactable}, state={b.GetComponent<UIInteractionAnimator>()?.State}, gaze={b.GetComponent<UIInteractionAnimator>()?.GazeProgress}")));
                    }
                    difficulty.Settings.lockDifficultySelection=false;reset.ResetExperience();router.InputSuspended=true;yield return WaitForChoices(selection); yield return new WaitForSecondsRealtime(.5f);
                    difficulty.Select(DifficultyLevel.Hard);yield return Save("start-selected-dwell",()=>{selection.Choices[1].OnGazeEnter(default);selection.Choices[1].OnGazeStay(default,.35f);selection.Choices[1].SetFocus(true);Check(selection.Choices[1].Progress>0 && selection.Choices[1].Progress<1,"Difficulty dwell missing");});selection.Choices[1].OnGazeExit();
                    difficulty.Settings.lockDifficultySelection=true;difficulty.ApplyOperatorDefaults();yield return Save("start-locked-selected");
                    difficulty.Settings.lockDifficultySelection=false;flow.StartMainGame();yield return new WaitForSecondsRealtime(.2f);health.RestoreCheckpoint(health.MaxHealth);health.Tick(1);health.ApplyImpactDamage(100,health.transform.position);
                    float end=Time.unscaledTime+10;while(!result.IsVisible&&Time.unscaledTime<end)yield return null;Check(result.IsVisible,"Result did not appear");reset.CancelResultTimer();Object.FindAnyObjectByType<ResultActionsView>().Menu.Click();yield return new WaitForSecondsRealtime(.5f);
                    lobby.View.Buttons[8].OnGazeEnter(default);lobby.View.Buttons[8].OnGazeStay(default,.35f);yield return Save("retry-dwell");lobby.View.Buttons[8].OnGazeExit();
                    difficulty.Settings.lockDifficultySelection=true;lobby.View.Buttons[1].SetAvailable(false);lobby.View.SetRetryAvailable(false);yield return Save("lobby-unavailable");
                    difficulty.Settings.lockDifficultySelection=false;lobby.View.Buttons[1].SetAvailable(true);lobby.Queue(HangarLobbyController.MenuAction.Difficulty);yield return new WaitForSecondsRealtime(.6f);
                    difficulty.Select(DifficultyLevel.Hard);yield return Save("lobby-difficulty-selected-dwell",()=>{selection.Choices[1].OnGazeEnter(default);selection.Choices[1].OnGazeStay(default,.2f);selection.Choices[1].SetFocus(true);Check(selection.Choices[1].Progress>0 && selection.Choices[1].Progress<1,"Difficulty dwell missing");});selection.Choices[1].OnGazeExit();
                    difficulty.Settings.lockDifficultySelection=true;difficulty.SetLobbySelection(true);yield return Save("lobby-difficulty-locked");difficulty.Settings.lockDifficultySelection=false;
                    lobby.Queue(HangarLobbyController.MenuAction.Back);yield return new WaitForSecondsRealtime(.35f);lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return new WaitForSecondsRealtime(.6f);
                    lobby.View.Records.Present(lobby.Ranking.Store,HangarRecordView.RecordTab.TopTen);yield return Save("records-selected-dwell",()=>{lobby.View.Records.TabButtons[2].OnGazeEnter(default);lobby.View.Records.TabButtons[2].OnGazeStay(default,.35f);Check(lobby.View.Records.TabButtons[2].Progress>0 && lobby.View.Records.TabButtons[2].Progress<1,"Records dwell missing");});
                    reset.ResetExperience();router.InputSuspended=true;yield return WaitForChoices(selection); yield return new WaitForSecondsRealtime(.5f);
                    AudioUserPreferences.SetVolume(UserAudioChannel.Music,0);AudioUserPreferences.SetVolume(UserAudioChannel.Effects,1);Button("AudioSettings").onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);
                    var target=Button("AudioMute0");var point=camera.WorldToScreenPoint(target.transform.TransformPoint(((RectTransform)target.transform).rect.center));overlay.TickSettingsGaze(point,true,.35f);yield return Save("audio-bounds-dwell");overlay.TryCloseLookSettings();
                    Button("LookSettings").onClick.Invoke();router.Simulation.Sensitivity=.1f;yield return Save("view-minimum");
                    Check(!Button("SensitivityDown").interactable,"Minimum sensitivity must disable minus");
                    Button("SensitivityUp").onClick.Invoke();Check(Mathf.Abs(router.Simulation.Sensitivity-.2f)<.001f,"Available plus must work after minimum");
                    router.Simulation.Sensitivity=2;yield return Save("view-maximum");Check(!Button("SensitivityUp").interactable,"Maximum sensitivity must disable plus");
                    overlay.TryCloseLookSettings();router.Simulation.Sensitivity=originalSensitivity;
                }
            }
            finally
            {
                router.Simulation.Sensitivity=originalSensitivity;difficulty.Settings.lockDifficultySelection=locked;overlay.TryCloseLookSettings();reset.ResetExperience();router.InputSuspended=false;caster.enabled=enabled;
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(false);foreach(var d in physical)InputSystem.EnableDevice(d);
            }
        }
    }
}
