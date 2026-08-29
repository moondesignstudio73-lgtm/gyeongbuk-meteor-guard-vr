using System;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    [DisallowMultipleComponent]
    public sealed class LocalRankingController : MonoBehaviour
    {
        [SerializeField,Range(50,100)] private int maximumRecords=100;
        private GameFlowManager flow;
        private ResultController result;
        private bool initialized, eligible, completed, recorded;
        private string sessionId;
        private int retryAttempts;
        private float nextRetry;
        public LocalRankingStore Store { get; private set; }
        public static string StoragePath => Path.Combine(Application.persistentDataPath,LocalRankingStore.FileName);
        public void Initialize(GameFlowManager gameFlow,ResultController results,LocalRankingStore injectedStore=null)
        {
            Unbind(); flow=gameFlow; result=results;
            if(Store==null || injectedStore!=null)
            {
                // Automated tests must never contaminate the operator's real historical records.
                Store=injectedStore??new LocalRankingStore(Application.isBatchMode?null:StoragePath,maximumRecords);
                Store.Load(); eligible=completed=recorded=false; sessionId=null;
            }
            initialized=true; Bind();
        }
        private void OnEnable() { if(initialized) Bind(); }
        private void OnDisable() => Unbind();
        private void Update()
        {
            // Rare I/O failures retry outside combat only. No per-frame disk access and no main-thread sleeps.
            if(Store==null || !Store.HasPendingWrite || retryAttempts>=3 || Time.unscaledTime<nextRetry || flow==null) return;
            if(flow.CurrentState!=GameState.Result && flow.CurrentState!=GameState.Boot) return;
            retryAttempts++; nextRetry=Time.unscaledTime+1f; Store.RetryPendingSave();
        }
        private void OnApplicationQuit() { if(Store!=null && Store.HasPendingWrite) Store.RetryPendingSave(); }
        private void Bind()
        {
            Unbind();
            if(flow!=null) flow.OnStateChanged+=OnState;
            if(result!=null) result.ResultShown+=OnResult;
        }
        private void Unbind()
        {
            if(flow!=null) flow.OnStateChanged-=OnState;
            if(result!=null) result.ResultShown-=OnResult;
        }
        private void OnState(GameState state)
        {
            if(state==GameState.Boot) { eligible=completed=recorded=false; sessionId=null; }
            else if(state==GameState.Playing && !eligible)
            { eligible=true; completed=recorded=false; sessionId=Guid.NewGuid().ToString("N"); }
            else if(state==GameState.MissionComplete && eligible) completed=true;
        }
        private void OnResult(GameSessionSnapshot snapshot,string rank)
        {
            if(!eligible || !completed || recorded || snapshot==null) return;
            recorded=true;
            Store.Add(LocalMissionRecord.FromResult(snapshot,rank,sessionId,DateTimeOffset.UtcNow));
            retryAttempts=0; nextRetry=Time.unscaledTime+.5f;
        }
        // Operator-only editor tooling calls this; not wired to public or gaze UI.
        public bool ClearHistory() => Store!=null && Store.Clear();
    }
}
