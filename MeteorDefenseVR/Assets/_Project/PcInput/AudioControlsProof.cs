using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class AudioControlsProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,Action<bool> complete)
        {
            var notes=new List<string>{"Standalone: synthetic InputSystem mouse/keyboard; Editor: synthetic gaze/API state checks because batch Editor did not consume queued input. Actual source DSP callbacks. No human listening, human gameplay or HMD. Radio/AI use an existing music clip only as isolated route-test signal; no speech assets exist."};
            bool passed=true;var owned=new List<GameObject>();var filters=new List<AudioChannelFilter>();
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var manager=Object.FindAnyObjectByType<AudioManager>();var overlay=Object.FindAnyObjectByType<PcTestOverlay>();
            var camera=Camera.main;var simulation=router.Simulation;
            var physical=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();
            foreach(var device in physical)InputSystem.DisableDevice(device);
            var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            var oldBackground=InputSystem.settings.backgroundBehavior;var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            float[] original=Enumerable.Range(0,4).Select(i=>AudioUserPreferences.Volume((UserAudioChannel)i)).ToArray();
            bool[] originalMute=Enumerable.Range(0,4).Select(i=>AudioUserPreferences.Muted((UserAudioChannel)i)).ToArray();
            void Check(bool value,string message){passed &= value;notes.Add(message+"="+value);}
            UnityEngine.UI.Button Button(string name)=>overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name);
            bool renderProbeActive=false;
            IEnumerator Click(string name)
            {
                var button=Button(name);var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
                var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
                if(renderProbeActive||Application.isEditor){overlay.TickSettingsGaze(Vector2.zero,false,.1f);for(int tick=0;tick<9;tick++){overlay.TickSettingsGaze(point,true,.1f);yield return null;}yield return new WaitForSecondsRealtime(.12f);yield break;}
                var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);notes.Add($"mouse {name} active={button.gameObject.activeInHierarchy} point={point} hits={string.Join("/",hits.Select(h=>h.gameObject.name))}");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return new WaitForSecondsRealtime(.08f);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return new WaitForSecondsRealtime(.08f);notes.Add($"mouse observed current={Mouse.current?.deviceId} test={mouse.deviceId} pos={mouse.position.ReadValue()} down={mouse.leftButton.isPressed} module={UnityEngine.EventSystems.EventSystem.current.currentInputModule?.GetType().Name}");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return new WaitForSecondsRealtime(.12f);
            }
            IEnumerator Key(Key key)
            {
                if(Application.isEditor)
                {
                    notes.Add("Editor API fallback for "+key+"; actual input requires standalone run");
                    if(key==UnityEngine.InputSystem.Key.Escape&&!overlay.TryCloseLookSettings())simulation.Resume();
                    yield return new WaitForSecondsRealtime(.15f);yield break;
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return new WaitForSecondsRealtime(.12f);
                notes.Add($"key {key} current={Keyboard.current?.deviceId} test={keyboard.deviceId} down={keyboard[key].isPressed}");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.12f);
            }
            IEnumerator Ready(){float deadline=Time.realtimeSinceStartup+30;while(Object.FindAnyObjectByType<SfPresentationDirector>().IsBootPresenting&&Time.realtimeSinceStartup<deadline)yield return null;yield return new WaitForSecondsRealtime(.2f);}
            try
            {
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(true);
                manager.RequestBgm(AudioCue.BgmBattle);yield return new WaitForSecondsRealtime(1.3f);
                var clip=manager.CurrentBgmClip;Check(clip!=null,"existing music test clip present");
                int baseline=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
                foreach(var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                {
                    var filter=source.GetComponent<AudioChannelFilter>();
                    notes.Add($"source={source.name} clip={source.clip?.name} active={source.gameObject.activeInHierarchy} filter={filter?.Channel} authoredVolume={source.volume} mixer={source.outputAudioMixerGroup?.name ?? "none"}");
                    if(source.gameObject.activeInHierarchy)Check(filter!=null,"active source routed: "+source.name);
                }
                for(int i=0;i<4;i++)
                {
                    var go=new GameObject("QA_Channel_"+i);owned.Add(go);var source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.volume=.1f;
                    var filter=AudioChannelFilter.Attach(source,(UserAudioChannel)i);filter.Measure=true;filters.Add(filter);source.Play();
                }
                for(int i=0;i<4;i++)
                {
                    var channel=(UserAudioChannel)i;var filter=filters[i];
                    foreach(string phase in new[]{"full","zero","restore","mute","unmute"})
                    {
                        AudioUserPreferences.SetVolume(channel,phase=="zero"?0:phase=="full"?1:.4f);AudioUserPreferences.SetMuted(channel,phase=="mute");
                        yield return new WaitForSecondsRealtime(.15f);filter.ResetMeasurement();yield return new WaitForSecondsRealtime(.3f);
                        float expected=AudioUserPreferences.Gain(channel);
                        bool signal=filter.MeasuredBlocks>0&&filter.PeakInputRms>0.0000001f;
                        bool correct=signal&&(expected==0?filter.PeakOutputRms<0.00000001f:Mathf.Abs(filter.PeakOutputRms/filter.PeakInputRms-expected)<.015f);
                        Check(correct,$"DSP {channel}/{phase}: blocks={filter.MeasuredBlocks}, in={filter.PeakInputRms}, out={filter.PeakOutputRms}, expected={expected}");
                        for(int other=0;other<4;other++)Check(Mathf.Abs(filters[other].AppliedGain-AudioUserPreferences.Gain((UserAudioChannel)other))<.0001f,"independent "+other);
                    }
                }
                foreach(var go in owned)Object.Destroy(go);owned.Clear();yield return null;
                foreach(var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                    if(source.TryGetComponent<AudioChannelFilter>(out var filter)){filter.Measure=true;filter.ResetMeasurement();}
                var library=Resources.FindObjectsOfTypeAll<AudioCueLibrary>().First(l=>l.Entries.Length>0);
                foreach(var entry in library.Entries)
                {
                    manager.StopAll();
                    foreach(var filter in Object.FindObjectsByType<AudioChannelFilter>())filter.ResetMeasurement();
                    bool requested=manager.TryPlay(entry.Cue,AudioSettings.dspTime+1000);
                    yield return new WaitForSecondsRealtime(entry.Category==AudioCategory.Bgm?1.3f:.4f);
                    string sourceName=entry.Category==AudioCategory.Bgm?"BGM":entry.Category==AudioCategory.UI?"UI":entry.Category==AudioCategory.Voice?"Voice":entry.Category==AudioCategory.Ambient?"Ambient":"SFX";
                    bool outputSignal=Object.FindObjectsByType<AudioChannelFilter>().Any(f=>f.name==sourceName&&f.PeakOutputRms>0.0000001f);
                    Check(requested&&outputSignal,$"existing cue DSP {entry.Cue} clip={entry.Clip?.name} seconds={entry.Clip?.length} channels={entry.Clip?.channels} source={sourceName}");
                }
                for(int i=0;i<4;i++){AudioUserPreferences.SetVolume((UserAudioChannel)i,1);AudioUserPreferences.SetMuted((UserAudioChannel)i,false);}
                reset.ResetExperience();yield return Ready();
                yield return Click("AudioSettings");Check(overlay.AudioSettingsOpen,Application.isEditor?"Editor gaze opens normal screen":"actual InputSystem mouse opens on normal screen");for(int i=0;i<4;i++){yield return Click("AudioDown"+i);Check(Mathf.Abs(AudioUserPreferences.Volume((UserAudioChannel)i)-.9f)<.001f,"normal input volume down "+i);yield return Click("AudioMute"+i);Check(AudioUserPreferences.Muted((UserAudioChannel)i),"normal input mute "+i);yield return Click("AudioMute"+i);yield return Click("AudioUp"+i);Check(!AudioUserPreferences.Muted((UserAudioChannel)i)&&AudioUserPreferences.Volume((UserAudioChannel)i)>.999f,"normal input restore "+i);}
                yield return Key(UnityEngine.InputSystem.Key.Enter);yield return Key(UnityEngine.InputSystem.Key.Space);Check(flow.CurrentState==GameState.Boot,"normal input modal blocks start");
                yield return Key(UnityEngine.InputSystem.Key.Escape);
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);renderProbeActive=true;yield return new WaitForSecondsRealtime(.15f);
                    yield return Click("AudioSettings");Check(overlay.AudioSettingsOpen&&router.UiModalOpen,"synthetic gaze opens rendered audio "+size);
                    for(int i=0;i<4;i++){yield return Click("AudioDown"+i);yield return Click("AudioMute"+i);Check(AudioUserPreferences.Muted((UserAudioChannel)i),"gaze mute "+i);}
                    var panel=overlay.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="AudioSettingsPanel");
                    for(int frame=0;frame<30;frame++){Check(panel.gameObject.activeInHierarchy&&router.UiModalOpen,"continuous modal frame "+frame);yield return null;}
                    yield return Key(UnityEngine.InputSystem.Key.Enter);yield return Key(UnityEngine.InputSystem.Key.Space);Check(flow.CurrentState==GameState.Boot,"modal blocks Enter/Space start");
                    Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);Directory.CreateDirectory(Path.Combine(output,size.x+"x"+size.y));proof.SaveGameOnlyProof(Path.Combine(output,size.x+"x"+size.y),router);
                    foreach(var label in panel.GetComponentsInChildren<TextMeshProUGUI>())
                    {
                        label.ForceMeshUpdate();Check(label.GetParsedText().Where(c=>!char.IsControl(c)&&!char.IsWhiteSpace(c)).All(c=>label.font.HasCharacter(c,true)),"all glyphs present "+label.name);bool fits=!label.isTextTruncated&&label.textBounds.size.x<=label.rectTransform.rect.width+.1f&&label.textBounds.size.y<=label.rectTransform.rect.height+.1f;
                        float px=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible&&char.IsLetterOrDigit(c.character)).Select(c=>Mathf.Abs(camera.WorldToScreenPoint(label.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(label.transform.TransformPoint(c.bottomLeft)).y)).DefaultIfEmpty(100).Min();
                        Check(fits&&px>=12,$"{size}/{label.name} fit={fits} px={px} text={label.text.Replace('\n','|')}");
                    }
                    foreach(var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
                    {var bounds=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)button.transform);Check(bounds.xMin>=0&&bounds.xMax<=1&&bounds.yMin>=0&&bounds.yMax<=1&&bounds.width*size.x>=44&&bounds.height*size.y>=44,"button bounds "+button.name);}
                    AudioUserPreferences.SetVolume(UserAudioChannel.Music,0);yield return new WaitForSecondsRealtime(.12f);Check(!Button("AudioDown0").interactable,"zero disables down");
                    AudioUserPreferences.SetVolume(UserAudioChannel.Music,1);yield return new WaitForSecondsRealtime(.12f);Check(!Button("AudioUp0").interactable,"full disables up");
                    yield return Key(UnityEngine.InputSystem.Key.Escape);Check(!overlay.AudioSettingsOpen,"Escape closes boot modal");
                    for(int i=0;i<4;i++){AudioUserPreferences.SetVolume((UserAudioChannel)i,1);AudioUserPreferences.SetMuted((UserAudioChannel)i,false);}
                }
                renderProbeActive=false;flow.StartMainGame();yield return new WaitForSecondsRealtime(.3f);simulation.Pause();yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();Check(simulation.IsPaused,"pause entered before click");yield return Click("AudioSettings");
                Check(overlay.AudioSettingsOpen&&simulation.IsPaused,"pause audio opens");yield return Key(UnityEngine.InputSystem.Key.Escape);Check(simulation.IsPaused&&!overlay.AudioSettingsOpen,"first Escape keeps pause");
                yield return Key(UnityEngine.InputSystem.Key.Escape);Check(!simulation.IsPaused,"second Escape resumes");
                reset.ResetExperience();yield return Ready();flow.ShowResult();yield return new WaitForSecondsRealtime(.3f);yield return Click("AudioSettings");
                reset.Tick(20);Check(overlay.AudioSettingsOpen&&Object.FindAnyObjectByType<ResultController>().IsVisible&&!Object.FindAnyObjectByType<HangarLobbyController>().ChooseFromResult(true),"result modal blocks timer and replay");
                yield return Key(UnityEngine.InputSystem.Key.Escape);Check(!overlay.AudioSettingsOpen,"Escape closes result settings");
                yield return Click("AudioSettings");reset.ResetExperience();yield return Ready();Check(!overlay.AudioSettingsOpen&&!router.UiModalOpen,"reset clears audio modal");
                for(int i=0;i<5;i++){simulation.Pause();yield return null;simulation.Resume();reset.ResetExperience();yield return Ready();}
                Check(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length==baseline,"five pause reset loops source count stable");
                var effects=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Where(s=>s.isPlaying&&s.loop&&!manager.OwnsBgmSource(s)&&!manager.OwnsAmbientSource(s)).ToArray();
                Check(effects.Length==0,"no lingering effects loops after reset");
            }
            finally
            {
                foreach(var go in owned)if(go!=null)Object.Destroy(go);
                foreach(var filter in Object.FindObjectsByType<AudioChannelFilter>())filter.Measure=false;
                simulation.Resume();reset.ResetExperience();AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(false);
                for(int i=0;i<4;i++)Check(AudioUserPreferences.Volume((UserAudioChannel)i)==original[i]&&AudioUserPreferences.Muted((UserAudioChannel)i)==originalMute[i],"production preferences preserved "+i);
                InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);foreach(var device in physical)InputSystem.EnableDevice(device);InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
