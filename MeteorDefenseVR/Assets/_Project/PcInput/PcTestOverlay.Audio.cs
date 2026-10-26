using MeteorDefenseVR.Audio;
using TMPro;
using UnityEngine;
namespace MeteorDefenseVR.PcInput
{
    public sealed partial class PcTestOverlay
    {
        private GameObject audioSettingsPanel,audioSettingsButton;
        private bool audioSettingsOpen;
        private readonly TextMeshProUGUI[] audioValues=new TextMeshProUGUI[4];
        private readonly UnityEngine.UI.Button[] audioDown=new UnityEngine.UI.Button[4],audioUp=new UnityEngine.UI.Button[4],audioMute=new UnityEngine.UI.Button[4];
        public bool AudioSettingsOpen=>settingsOpen&&audioSettingsOpen;
        private void BuildAudioSettings()
        {
            ((RectTransform)lookSettingsButton.transform).anchoredPosition=new Vector2(-180,48);
            audioSettingsButton=SettingsButton(canvasObject.transform,"AudioSettings","소리 설정",Vector2.zero,new Vector2(320,110),OpenAudioSettings).gameObject;
            var entry=(RectTransform)audioSettingsButton.transform;entry.anchorMin=entry.anchorMax=entry.pivot=new Vector2(.5f,0);entry.anchoredPosition=new Vector2(180,48);
            audioSettingsPanel=Rect("AudioSettingsPanel",canvasObject.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(1400,1030)).gameObject;
            Background(audioSettingsPanel,FlightUiTheme.Ink);
            FlightUiTheme.Plate(audioSettingsPanel.GetComponent<UnityEngine.UI.Image>());
            var title=Label(audioSettingsPanel.transform,"AudioTitle","소리 설정",new Vector2(36,-24),new Vector2(1328,88),56);
            title.fontStyle=FontStyles.Bold;title.color=FlightUiTheme.Accent;
            for(int i=0;i<4;i++)
            {
                var channel=(UserAudioChannel)i;float y=-135-i*170;
                SettingsRow(audioSettingsPanel.transform,"AudioRow"+i,new Vector2(24,y+4),new Vector2(1352,158));
                audioValues[i]=Label(audioSettingsPanel.transform,"AudioValue"+i,"",new Vector2(36,y),new Vector2(610,150),46);
                audioDown[i]=SettingsButton(audioSettingsPanel.transform,"AudioDown"+i,"−",new Vector2(680,y),new Vector2(140,120),()=>AudioUserPreferences.SetVolume(channel,AudioUserPreferences.Volume(channel)-.1f));
                audioUp[i]=SettingsButton(audioSettingsPanel.transform,"AudioUp"+i,"+",new Vector2(840,y),new Vector2(140,120),()=>AudioUserPreferences.SetVolume(channel,AudioUserPreferences.Volume(channel)+.1f));
                audioMute[i]=SettingsButton(audioSettingsPanel.transform,"AudioMute"+i,"음소거",new Vector2(1000,y),new Vector2(340,120),()=>AudioUserPreferences.SetMuted(channel,!AudioUserPreferences.Muted(channel)));
            }
            Label(audioSettingsPanel.transform,"AudioHelp","변경한 소리 설정은 자동으로 저장돼요.",new Vector2(36,-820),new Vector2(1328,60),40);
            SettingsButton(audioSettingsPanel.transform,"AudioClose","닫기 / Esc",new Vector2(500,-900),new Vector2(400,110),CloseLookSettings);
            audioSettingsPanel.SetActive(false);
        }
        private void OpenAudioSettings()
        {SetLookSettings(true);audioSettingsOpen=true;lookSettingsPanel.SetActive(false);audioSettingsPanel.SetActive(true);RefreshAudioSettings();}
        private static readonly string[] audioChannelNames={"배경음악","효과음","무전","AI 안내"};
        private void RefreshAudioSettings()
        {
            string[] names={"배경음악","효과음","무전","AI 안내"};
            for(int i=0;i<4;i++)
            {
                var channel=(UserAudioChannel)i;float value=AudioUserPreferences.Volume(channel);bool mute=AudioUserPreferences.Muted(channel);
                audioValues[i].text=audioChannelNames[i]+"  "+Mathf.RoundToInt(value*100)+"%"+(i>=2?"\n음성 준비 중":"");
                audioDown[i].interactable=value>.0001f;audioUp[i].interactable=value<.9999f;
                audioMute[i].GetComponentInChildren<TextMeshProUGUI>().text=mute?"소리 켜기":"음소거";
                audioMute[i].GetComponent<UIInteractionAnimator>().SetSelected(mute);
            }
        }
    }
}
