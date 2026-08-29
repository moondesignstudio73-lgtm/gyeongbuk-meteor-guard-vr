using System;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Difficulty;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    public sealed partial class EducationSessionManager
    {
        private EmergencyTrainingState fault;
        private FaultLearningRecord faultRecord;
        private EducationQuestionRecord emergencyQuestion;
        private string[] answerOptions=Array.Empty<string>();
        private int answerOffset, expectedAnswer;
        private float nextAnswerAt;
        private bool waitingForRepair;
        partial void ResetSpecial(){fault=null;faultRecord=null;emergencyQuestion=null;answerOptions=Array.Empty<string>();waitingForRepair=false;}
        public EmergencyTrainingState Fault => fault;
        public string[] AnswerOptions => answerOptions;
        public string QuestionText
        {
            get
            {
                if(fault==null)return "";
                if(Elapsed<nextAnswerAt)return "대응 시간이 소모되고 상태가 악화됐어요. 잠시 후 다시 선택하세요.\n"+fault.Readout;
                string readout=fault.Readout;
                if(Phase==EducationPhase.TriageQuestion)
                {
                    MostUrgentId(out var ttc);
                    return readout+" / 경미한 전력 장애\n"+(ttc>=0?$"가장 빠른 충돌 예상 {ttc:F1}초. 무엇부터 대응할까요?":"외부 위협 없음. 무엇부터 대응할까요?");
                }
                return readout+"\n"+(Phase==EducationPhase.CauseQuestion?"가장 가능성이 높은 원인은 무엇인가요?":"원인: "+EmergencyTrainingState.Cause(fault.Kind)+". 적절한 대응은?");
            }
        }
        partial void BeginSpecialRound()
        {
            fault=null;faultRecord=null;emergencyQuestion=null;waitingForRepair=false;answerOptions=Array.Empty<string>();
            health.ResetHealth();weapon.AutoFireOnLock=false;
            Phase=EducationPhase.Demonstration;message="모의 충돌 상황입니다. 충격 뒤 계통 상태를 살펴보세요.";
            var direction=Stage==EducationStage.Integrated?ThreatDirection.Front:(ThreatDirection)((round-1)%6);
            Spawn(direction,30,11,false,false,true,Stage==EducationStage.Integrated?50:22+round%3*6);
            if(Stage==EducationStage.Integrated)
            {
                Spawn(ThreatDirection.Rear,85,3.2f*SpeedMultiplier(),true,true,false);
                Spawn(ThreatDirection.Right,100,4*SpeedMultiplier(),true,true,false);
                if(difficulty.Selected>=DifficultyLevel.Hard)Spawn(ThreatDirection.Above,105,3.6f*SpeedMultiplier(),true,true,false);
            }
            deadline=Elapsed+8;
        }
        partial void ImpactSpecial(ThreatLearningRecord record,int damage)
        {
            if(!record.Demonstration||Phase!=EducationPhase.Demonstration)return;
            fault=new EmergencyTrainingState();fault.Impact(record.Direction,damage);
            faultRecord=new FaultLearningRecord{Round=round,ThreatId=record.Id,Stage=Stage,Direction=record.Direction,
                Kind=fault.Kind,Damage=damage,At=Elapsed,Severity=fault.Severity,InitialCondition=fault.Condition,FinalCondition=fault.Condition};
            log.Result.Faults.Add(faultRecord);
            audio?.TryPlay(AudioCue.Warning);
            if(Stage==EducationStage.Integrated)
            {
                // Same hull impact also damages a minor circuit; explicitly recorded, not scored as an avoidance failure.
                log.Result.Faults.Add(new FaultLearningRecord{Round=round,ThreatId=record.Id,Stage=Stage,Direction=record.Direction,
                    Kind=TrainingFault.Electrical,Damage=5,At=Elapsed,Severity=.1f,InitialCondition=95,FinalCondition=95,Secondary=true});
                Ask(EducationPhase.TriageQuestion);
            }
            else Ask(EducationPhase.CauseQuestion);
        }
        private void Ask(EducationPhase phase)
        {
            Phase=phase;nextAnswerAt=Elapsed;deadline=Elapsed+settings.questionDeadline*AssistanceTime();SetModal(true);weapon.AutoFireOnLock=false;
            string[] canonical;
            if(phase==EducationPhase.CauseQuestion)
            {
                message="손상 원인 판단";canonical=new[]{EmergencyTrainingState.Cause(fault.Kind),EmergencyTrainingState.Cause((TrainingFault)(((int)fault.Kind+1)%5)),EmergencyTrainingState.Cause((TrainingFault)(((int)fault.Kind+2)%5))};
            }
            else if(phase==EducationPhase.TriageQuestion)
            {message="동시 비상상황 · 우선순위 판단";canonical=new[]{"손상 구역 차단 (압력 / 산소)","경미한 전력 문제 점검","즉시 외부 운석 방어"};}
            else {message="비상 대응";canonical=new[]{EmergencyTrainingState.Action(fault.Kind),"엔진 출력 증가",fault.Kind==TrainingFault.Communication?"계기판 밝기 증가":"통신 재시작"};}
            answerOffset=(round+(phase==EducationPhase.ResponseQuestion?1:phase==EducationPhase.TriageQuestion?2:0))%3;
            answerOptions=new string[3];for(int i=0;i<3;i++)answerOptions[i]=canonical[(i+answerOffset)%3];
            expectedAnswer=(3-answerOffset)%3;
            emergencyQuestion=log.Question(Stage,round,phase==EducationPhase.CauseQuestion?EducationQuestionKind.Cause:EducationQuestionKind.Emergency,phase+"-"+fault.Kind,Elapsed);
            if(emergencyQuestion!=null)emergencyQuestion.Options=(string[])answerOptions.Clone();
            Changed?.Invoke();
        }
        public void SubmitAnswer(int option)
        {
            bool asking=Phase==EducationPhase.CauseQuestion||Phase==EducationPhase.ResponseQuestion||Phase==EducationPhase.TriageQuestion;
            if(!leased||!asking||Time.timeScale<=0||Elapsed<nextAnswerAt||Elapsed>=deadline||option<0||option>=answerOptions.Length||emergencyQuestion==null)return;
            int expected=expectedAnswer;float ttc=-1;
            if(Phase==EducationPhase.TriageQuestion){MostUrgentId(out ttc);expected=(fault.TriageChoice(ttc,settings.emergency)+3-answerOffset)%3;}
            log.Answer(emergencyQuestion,option,expected,Elapsed,ttc);
            if(option!=expected)
            {
                fault.WrongAction();faultRecord.FinalCondition=fault.Condition;nextAnswerAt=Elapsed+settings.emergency.wrongActionDelay;
                if(emergencyQuestion.Attempts>=2){explanation=EmergencyTrainingState.Lesson(fault.Kind);Feedback("대응을 다시 연습해요. "+explanation);}
                else Changed?.Invoke();
                return;
            }
            audio?.TryPlay(AudioCue.CalibrationComplete);
            if(Phase==EducationPhase.CauseQuestion)
            { explanation=EmergencyTrainingState.Lesson(fault.Kind);Ask(EducationPhase.ResponseQuestion);return; }
            if(Phase==EducationPhase.TriageQuestion)
            {
                int action=(option+answerOffset)%3;
                if(action==0)MitigateFault();
                if(action==1)
                {
                    foreach(var r in log.Result.Faults)if(r.Round==round&&r.Secondary){r.MitigatedAt=Elapsed;r.FinalCondition=100;}
                }
                waitingForRepair=!fault.Resolved;
                BeginDefense();return;
            }
            MitigateFault();
            if(Stage==EducationStage.Integrated&&spawner.ActiveCount>0)BeginDefense();
            else Feedback("문제를 완화했어요. "+EmergencyTrainingState.Lesson(fault.Kind));
        }
        private void MitigateFault()
        {fault.Mitigate();faultRecord.MitigatedAt=Elapsed;faultRecord.FinalCondition=fault.Condition;waitingForRepair=false;}
        private void BeginDefense()
        {
            if(spawner.ActiveCount==0)
            {if(fault!=null&&!fault.Resolved)Ask(EducationPhase.ResponseQuestion);else Feedback("위협이 종료됐어요. 대응 결과를 확인하세요.");return;}
            Phase=EducationPhase.Defend;message="외부 위협의 거리·속도를 보고 우선 방어하세요.";SetModal(false);weapon.AutoFireOnLock=true;
            if(priorityQuestion==null)priorityQuestion=log.Question(Stage,round,EducationQuestionKind.Priority,"integrated-threat-priority",Elapsed);
            deadline=Elapsed+45*AssistanceTime();Changed?.Invoke();
        }
        partial void TickSpecial(float dt)
        {
            if(fault!=null&&Phase!=EducationPhase.Feedback){fault.Tick(dt,settings.emergency);if(faultRecord!=null)faultRecord.FinalCondition=fault.Condition;}
            if(Phase==EducationPhase.Demonstration&&Elapsed>=deadline){Feedback("모의 충돌이 완료되지 않았습니다. 다음 상황으로 진행합니다.");return;}
            bool asking=Phase==EducationPhase.CauseQuestion||Phase==EducationPhase.ResponseQuestion||Phase==EducationPhase.TriageQuestion;
            if(asking&&Elapsed>=deadline)
            {
                int expected=expectedAnswer;float ttc=-1;
                if(Phase==EducationPhase.TriageQuestion){MostUrgentId(out ttc);expected=(fault.TriageChoice(ttc,settings.emergency)+3-answerOffset)%3;}
                if(emergencyQuestion!=null&&emergencyQuestion.Attempts==0)log.Answer(emergencyQuestion,-1,expected,Elapsed,ttc);
                if(Phase==EducationPhase.CauseQuestion){explanation="응답 시간이 끝났어요. "+EmergencyTrainingState.Lesson(fault.Kind);Ask(EducationPhase.ResponseQuestion);}
                else if(Stage==EducationStage.Integrated&&spawner.ActiveCount>0){waitingForRepair=true;BeginDefense();}
                else Feedback("대응 시간이 끝났어요. "+EmergencyTrainingState.Lesson(fault.Kind));
            }
            if(Stage==EducationStage.Integrated&&Phase==EducationPhase.Defend&&spawner.ActiveCount==0&&waitingForRepair&&!fault.Resolved)
            {waitingForRepair=false;Ask(EducationPhase.ResponseQuestion);}
        }
    }
}
