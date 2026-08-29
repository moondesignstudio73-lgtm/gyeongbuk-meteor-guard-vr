using System;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    public enum TrainingFault { HullPressure, Electrical, Communication, Oxygen, Propulsion }
    [Serializable] public sealed class EmergencyTrainingSettings
    {
        [Range(1,8)] public float immediateCollisionSeconds=4;
        [Range(30,80)] public float criticalPressure=65;
        [Range(30,80)] public float criticalOxygen=65;
        [Range(.1f,3)] public float deteriorationPerSecond=.8f;
        [Range(1,5)] public float wrongActionDelay=2;
    }
    [Serializable] public sealed class FaultLearningRecord
    {
        public int Round, ThreatId, Damage;
        public EducationStage Stage;
        public TrainingFault Kind;
        public ThreatDirection Direction;
        public float At, Severity, InitialCondition, MitigatedAt=-1, FinalCondition;
        public bool Secondary;
    }
    // Educational subsystem indicators, not a real spacecraft/medical simulator.
    // No changes to head tracking, camera sensitivity or primary combat HUD are made.
    public sealed class EmergencyTrainingState
    {
        public TrainingFault Kind { get; private set; }
        public float Severity { get; private set; }
        public float Condition { get; private set; }=100;
        public float Oxygen { get; private set; }=100;
        public bool Resolved { get; private set; }
        public int WrongActions { get; private set; }
        public static TrainingFault FromDirection(ThreatDirection d) => d switch
        { ThreatDirection.Left=>TrainingFault.Electrical,ThreatDirection.Right=>TrainingFault.Communication,
          ThreatDirection.Above=>TrainingFault.Oxygen,ThreatDirection.Rear=>TrainingFault.Propulsion,_=>TrainingFault.HullPressure };
        public void Impact(ThreatDirection direction,int damage)
        {
            Kind=FromDirection(direction);Severity=Mathf.Clamp01(damage/50f);Condition=Mathf.Clamp(100-damage,10,100);
            Oxygen=Kind==TrainingFault.HullPressure?Mathf.Max(10,100-damage*.5f):Kind==TrainingFault.Oxygen?Condition:100;
            Resolved=false;WrongActions=0;
        }
        public void Tick(float dt,EmergencyTrainingSettings settings)
        {
            if(Resolved||dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            Condition=Mathf.Max(5,Condition-dt*Severity*Mathf.Clamp(settings.deteriorationPerSecond,.1f,3));
            if(Kind==TrainingFault.Oxygen)Oxygen=Condition;
            else if(Kind==TrainingFault.HullPressure)Oxygen=Mathf.Max(5,Oxygen-dt*Severity*.45f);
        }
        public void WrongAction(){WrongActions++;Severity=Mathf.Min(1,Severity+.15f);Condition=Mathf.Max(5,Condition-8);if(Kind==TrainingFault.Oxygen)Oxygen=Condition;}
        public void Mitigate(){Resolved=true;Condition=Mathf.Min(100,Condition+20);Oxygen=Mathf.Min(100,Oxygen+10);}
        public bool LifeCritical(EmergencyTrainingSettings settings) => !Resolved &&
            (Kind==TrainingFault.HullPressure&&Condition<settings.criticalPressure||Oxygen<settings.criticalOxygen);
        // An immediate collision takes precedence; otherwise life support precedes a mild electrical fault.
        public int TriageChoice(float ttc,EmergencyTrainingSettings settings) => ttc>=0&&ttc<settings.immediateCollisionSeconds?2:LifeCritical(settings)?0:1;
        public string Readout => Kind switch
        {
            TrainingFault.HullPressure=>$"압력 {Condition:F0}% / 산소 {Oxygen:F0}% · 계속 감소 중",
            TrainingFault.Electrical=>$"전력 안정도 {Condition:F0}% · 계기판 깜빡임 / 스파크",
            TrainingFault.Communication=>$"통신 연결 {Condition:F0}% · 신호 끊김 / 잡음",
            TrainingFault.Oxygen=>$"산소 공급 {Oxygen:F0}% · 공급량 감소 경고",
            _=>$"추진 응답 {Condition:F0}% · 명령 대비 출력 부족"
        };
        public static string Cause(TrainingFault kind)=>kind switch
        {TrainingFault.HullPressure=>"선체 또는 창문 파손",TrainingFault.Electrical=>"전기 배선 또는 전력 계통 손상",TrainingFault.Communication=>"통신 계통 손상",TrainingFault.Oxygen=>"산소 공급 계통 손상",_=>"추진 제어 계통 손상"};
        public static string Action(TrainingFault kind)=>kind switch
        {TrainingFault.HullPressure=>"손상 구역 차단",TrainingFault.Electrical=>"손상 회로 차단 후 예비 전력 사용",TrainingFault.Communication=>"예비 통신 채널로 전환",TrainingFault.Oxygen=>"예비 산소 공급 계통으로 전환",_=>"손상 추진기 차단 후 보조 제어 사용"};
        public static string Lesson(TrainingFault kind)=>kind switch
        {TrainingFault.HullPressure=>"압력 저하는 선체 틈으로 공기가 빠져나가는 신호예요. 손상 구역을 먼저 차단해요.",
         TrainingFault.Electrical=>"스파크와 전력 불안정은 배선 손상의 단서예요. 손상 회로를 격리한 뒤 예비 전력을 사용해요.",
         TrainingFault.Communication=>"통신 잡음과 연결 저하는 통신 계통을 점검할 단서예요. 예비 채널로 전환해요.",
         TrainingFault.Oxygen=>"산소 공급 감소는 생명 유지 계통의 위험 신호예요. 예비 공급 계통으로 전환해요.",
         _=>"명령에 비해 출력이 부족하면 추진 제어 계통을 점검해요. 손상 추진기를 분리하고 보조 제어를 사용해요."};
    }
}
