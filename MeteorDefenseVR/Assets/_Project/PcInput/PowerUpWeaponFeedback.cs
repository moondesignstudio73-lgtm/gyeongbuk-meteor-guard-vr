using System.Text;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;
namespace MeteorDefenseVR.PcInput
{
    // One readable status list owns all persistent buffs; existing weapon indicators stay event-driven.
    [DisallowMultipleComponent]
    public sealed class PowerUpWeaponFeedback : MonoBehaviour
    {
        private PowerUpManager manager;
        private PowerUpHudView legacy;
        private LaserWeapon weapon;
        private TMP_Text label, summary;
        private RectTransform panel, graphics;
        private UnityEngine.UI.Image track, charge, first, second;
        private readonly StringBuilder text = new StringBuilder(256);
        private readonly char[] digits = new char[12];
        private float pulse;
        private int shotCount;
        private bool bound;
        public float RapidRemaining { get; private set; }
        public float MultiRemaining { get; private set; }
        public bool Visible { get; private set; }
        public float AuxiliaryPulse => pulse;
        public int LastAdditionalShots => shotCount;
        public TMP_Text Readout => label;
        public float Charge => charge != null ? charge.rectTransform.sizeDelta.x / 70f : 0;
        public bool EndingSoon { get; private set; }
        public void Configure(TMP_Text existing, TMP_Text threat)
        {
            label=existing;summary=threat;panel=(RectTransform)transform;
            manager=PowerUpManager.Instance;weapon=FindAnyObjectByType<LaserWeapon>();legacy=FindAnyObjectByType<PowerUpHudView>();
            label.font=threat.font;label.fontSharedMaterial=threat.fontSharedMaterial;
            label.fontSize=30;label.enableAutoSizing=false;label.lineSpacing=4;
            label.alignment=TextAlignmentOptions.TopLeft;label.color=Color.white;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;
            var rect=label.rectTransform;rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;
            rect.anchoredPosition=new Vector2(16,10);rect.sizeDelta=new Vector2(368,110);
            graphics=new GameObject("WeaponFeedbackIndicators",typeof(RectTransform)).GetComponent<RectTransform>();
            graphics.SetParent(panel,false);graphics.anchorMin=graphics.anchorMax=Vector2.zero;graphics.anchoredPosition=Vector2.zero;graphics.sizeDelta=Vector2.zero;
            track=Make("ChargeTrack",new Vector2(70,10));track.color=new Color(.10f,.22f,.26f);
            charge=Make("RapidCharge",new Vector2(70,10));
            first=Make("AuxiliaryMuzzle1",new Vector2(28,12));second=Make("AuxiliaryMuzzle2",new Vector2(28,12));
            Bind();Tick(0);
        }
        private UnityEngine.UI.Image Make(string name,Vector2 size)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.transform.SetParent(graphics,false);image.raycastTarget=false;image.material=panel.GetComponent<UnityEngine.UI.Image>().material;
            var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,.5f);rect.sizeDelta=size;return image;
        }
        private void Bind()
        {
            if(!bound&&manager!=null){manager.MultiLaserFired+=Fired;bound=true;}
            // Suppress only after the full replacement, including immediate notices, is connected.
            if(label!=null&&manager!=null)legacy?.SetIntegratedPresentation(true);
        }
        private void OnEnable()=>Bind();
        private void OnDisable()
        {
            if(bound&&manager!=null)manager.MultiLaserFired-=Fired;
            bound=false;pulse=0;shotCount=0;Present(false);legacy?.SetIntegratedPresentation(false);
        }
        private void Fired(int count){if(manager!=null&&!manager.TimersPaused&&Time.timeScale>0){pulse=.22f;shotCount=count;}}
        private void Update()=>Tick(Time.unscaledDeltaTime);
        public void Tick(float dt)
        {
            if(label==null||manager==null)return;
            if(legacy==null) { legacy=FindAnyObjectByType<PowerUpHudView>(FindObjectsInactive.Include); legacy?.SetIntegratedPresentation(true); }
            RapidRemaining=MultiRemaining=0;EndingSoon=false;int rapidRow=-1,multiRow=-1;
            text.Clear();
            for(int i=0;i<manager.ActiveBuffs.Count;i++)
            {
                var buff=manager.ActiveBuffs[i];
                if(buff.Type==PowerUpType.RapidFire){RapidRemaining=buff.Remaining;rapidRow=i;}
                if(buff.Type==PowerUpType.MultiLaser){MultiRemaining=buff.Remaining;multiRow=i;}
                bool ending=buff.ShieldHits<0&&buff.Remaining<=2;EndingSoon|=ending;
                if(i>0)text.Append('\n');if(ending)text.Append("<color=#FFC766>");
                text.Append(PowerUpHudView.PlayerName(buff.Type)).Append(' ');
                AppendCount(buff.ShieldHits>=0?buff.ShieldHits:Mathf.CeilToInt(buff.Remaining));
                text.Append(buff.ShieldHits>=0?"회":"초");if(ending)text.Append("</color>");
            }
            var state=GameFlowManager.Instance!=null?GameFlowManager.Instance.CurrentState:GameState.Boot;
            Present((state==GameState.Playing||state==GameState.BossMeteor)&&manager.ActiveBuffCount>0);
            if(!Visible||MultiRemaining<=0){pulse=0;shotCount=0;}
            if(!manager.TimersPaused&&Time.timeScale>0)pulse=Mathf.Max(0,pulse-Mathf.Max(0,dt));
            if(!Visible)return;
            label.SetText(text);label.color=Color.white;
            float height=label.rectTransform.rect.height;
            Place(track,rapidRow,306,height);Place(charge,rapidRow,306,height);
            Place(first,multiRow,306,height);Place(second,multiRow,348,height);
            charge.rectTransform.sizeDelta=new Vector2(RapidRemaining>0?70*weapon.ChargeNormalized:0,10);charge.color=Tint(RapidRemaining);
            first.color=Color.Lerp(Tint(MultiRemaining)*new Color(1,1,1,.5f),Color.white,pulse/.22f);
            second.color=Color.Lerp(Tint(MultiRemaining)*new Color(1,1,1,.5f),Color.white,shotCount>=2?pulse/.22f:0);
        }
        private void AppendCount(int value)
        {
            value=Mathf.Max(0,value);int cursor=digits.Length;
            do{digits[--cursor]=(char)('0'+value%10);value/=10;}while(value>0);
            text.Append(digits,cursor,digits.Length-cursor);
        }
        private static void Place(UnityEngine.UI.Image image,int row,float x,float height)
        {image.gameObject.SetActive(row>=0);if(row>=0)image.rectTransform.anchoredPosition=new Vector2(x,height-16-row*50);}
        private static Color Tint(float time)=>time<=2?new Color(1,.65f,.22f):new Color(.18f,.92f,1);
        private void Present(bool visible)
        {
            Visible=visible;if(label==null)return;
            label.gameObject.SetActive(visible);graphics.gameObject.SetActive(visible);
            float extra=visible?manager.ActiveBuffCount*54+8:0;
            panel.sizeDelta=new Vector2(400,160+extra);summary.rectTransform.offsetMin=new Vector2(16,8+extra);
            label.rectTransform.sizeDelta=new Vector2(368,Mathf.Max(56,extra+2));
        }
    }
}
