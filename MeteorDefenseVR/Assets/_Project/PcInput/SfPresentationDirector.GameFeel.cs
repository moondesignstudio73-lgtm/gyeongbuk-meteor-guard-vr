using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Event-driven additions to the existing presentation director, not a second game flow.
    public sealed partial class SfPresentationDirector
    {
        private ReadyPrewarmController preparation;
        private MeteorSpawner spawner;
        private LaserWeapon weapon;
        private BossClimaxController boss;
        private LaunchSequenceController launch;
        private AudioManager audio;
        private TextMeshPro flightNotice, praise;
        private UnityEngine.UI.Image preparationBar;
        private bool ambientStarted;
        private float noticeAge = 10f, noticeDuration, praiseAge = 10f, lastKill = -10f, praiseAllowedAt, dangerAllowedAt;
        private int streak, noticePriority;
        public int WaveNoticeCount { get; private set; }
        public int PraiseCount { get; private set; }
        public int ConsecutiveDestructions => streak;
        public bool IsPreparing => preparation != null && !preparation.IsComplete;

        private void InitializeFeel(TMP_FontAsset font, Material material)
        {
            preparation = GetComponent<ReadyPrewarmController>();
            spawner = FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            weapon = FindAnyObjectByType<LaserWeapon>(FindObjectsInactive.Include);
            boss = FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);
            launch = FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include);
            audio = FindAnyObjectByType<AudioManager>();
            flightNotice = Text("FlightNotice", font, material); flightNotice.fontSize = 1f;
            flightNotice.text = "WAVE 01 / APPROACH";
            flightNotice.rectTransform.sizeDelta = new Vector2(4.8f,.65f);
            flightNotice.transform.localPosition = new Vector3(0,.6f,2.65f); flightNotice.enabled = false;
            praise = Text("CombatPraise", font, material); praise.fontSize = .9f;
            praise.text = "NICE / COMBO x3";
            praise.rectTransform.sizeDelta = new Vector2(4,.5f);
            praise.transform.localPosition = new Vector3(0,-.4f,2.65f); praise.enabled = false;
            preparationBar = Image("PreparationProgress", overlay, new Vector2(-160,-36), new Vector2(0,3));
            preparationBar.rectTransform.pivot = new Vector2(0,.5f); preparationBar.color = Cyan;
            preparationBar.gameObject.SetActive(false);
        }
        private void BindFeel()
        {
            UnbindFeel();
            if (spawner != null) spawner.WaveStarted += WaveStarted;
            if (weapon != null) weapon.Hit += CombatHit;
            if (boss != null) boss.StageChanged += BossStage;
            if (launch != null) launch.AudioCueRequested += LaunchPulse;
        }
        private void UnbindFeel()
        {
            if (spawner != null) spawner.WaveStarted -= WaveStarted;
            if (weapon != null) weapon.Hit -= CombatHit;
            if (boss != null) boss.StageChanged -= BossStage;
            if (launch != null) launch.AudioCueRequested -= LaunchPulse;
            if (audio != null) { audio.SetPresentationBgmGain(1f); if (ambientStarted) audio.StopAmbient(); }
            ambientStarted = false;
            if (flightNotice != null) flightNotice.enabled = false;
            if (praise != null) praise.enabled = false;
        }
        private void TransitionFeel(GameState state)
        {
            if (state != GameState.Boot && ambientStarted) { audio?.StopAmbient(); ambientStarted = false; }
            if (state != GameState.Playing)
            {
                if (flightNotice != null) flightNotice.enabled = false;
                if (praise != null) praise.enabled = false;
                noticeAge = praiseAge = 10f; streak = 0;
            }
            if (state != GameState.BossMeteor) audio?.SetPresentationBgmGain(1f);
            if (state == GameState.Boot || state == GameState.Reset)
            { lastKill = -10f; praiseAllowedAt = dangerAllowedAt = 0; noticePriority = 0; WaveNoticeCount = PraiseCount = 0; }
        }
        private void WaveStarted(int index, MeteorWaveData wave)
        {
            if (flow == null || flow.CurrentState != GameState.Playing) return;
            ShowNotice(wave.DisplayName, Cyan, 1.15f, 1); Pulse(Cyan); WaveNoticeCount++;
            audio?.TryPlay(AudioCue.Countdown);
        }
        private void CombatHit(MeteorController meteor, Vector3 position)
        {
            if (flow == null || flow.CurrentState != GameState.Playing || meteor == null) return;
            if (meteor.State != MeteorLifecycleState.Destroyed) return;
            streak = Time.unscaledTime - lastKill <= 4f ? streak + 1 : 1; lastKill = Time.unscaledTime;
            if (streak % 3 != 0 || Time.unscaledTime < praiseAllowedAt) return;
            praise.text = streak == 3 ? "NICE / COMBO x3" : streak == 6 ? "GREAT / COMBO x6" : "GREAT LOCK";
            praise.enabled = true; praiseAge = 0f; praiseAllowedAt = Time.unscaledTime + 4f; PraiseCount++;
        }
        private void BossStage(BossClimaxStage stage, string message)
        {
            bool warning = stage == BossClimaxStage.Warning || stage == BossClimaxStage.Narration;
            audio?.SetPresentationBgmGain(warning ? .28f : 1f);
            if (stage == BossClimaxStage.Warning) { Pulse(Warning); flightNotice.enabled = praise.enabled = false; }
            if (stage == BossClimaxStage.Active) Pulse(Warning);
        }
        private void LaunchPulse(LaunchAudioCue cue)
        {
            if (cue == LaunchAudioCue.Countdown || cue == LaunchAudioCue.MissionStart) Pulse(Cyan);
        }
        private void ShowNotice(string message, Color tint, float duration, int priority)
        {
            if (noticeAge < noticeDuration && noticePriority > priority) return;
            flightNotice.text = message; flightNotice.color = tint; flightNotice.enabled = true;
            noticeAge = 0f; noticeDuration = duration; noticePriority = priority;
        }
        private void TickFeel(float dt)
        {
            preparationBar.gameObject.SetActive(booting && IsPreparing);
            if (booting && IsPreparing)
                preparationBar.rectTransform.sizeDelta = new Vector2(320f * preparation.CompletedSteps / preparation.TotalSteps,3);
            if (flow.CurrentState == GameState.Boot && !booting && !IsPreparing && !ambientStarted)
            { ambientStarted = true; audio?.TryPlay(AudioCue.IntroSpaceAmbient); }
            noticeAge += dt; praiseAge += dt;
            if (flightNotice.enabled)
            {
                var tint = flightNotice.color; tint.a = Mathf.Clamp01(noticeAge / .08f) * Mathf.Clamp01((noticeDuration - noticeAge) / .25f);
                flightNotice.color = tint;
                flightNotice.transform.localScale = Vector3.one * Mathf.Lerp(1.04f,1,WorldTextMotion.Ease(noticeAge/.2f));
                if (noticeAge >= noticeDuration) flightNotice.enabled = false;
            }
            if (praise.enabled)
            {
                praise.color = new Color(.56f,1,.77f,Mathf.Clamp01((.85f-praiseAge)/.25f));
                praise.transform.localScale = Vector3.one * (1f + .1f * WorldTextMotion.Pulse(praiseAge,.22f));
                if (praiseAge >= .85f) praise.enabled = false;
            }
            bool combat = flow.CurrentState == GameState.Playing || flow.CurrentState == GameState.BossMeteor;
            if (combat && hud != null && hud.CurrentHealth > 0 && hud.CurrentHealth <= hud.MaxHealth * .3f && Time.unscaledTime >= dangerAllowedAt)
            {
                dangerAllowedAt = Time.unscaledTime + 6f;
                if (flow.CurrentState == GameState.Playing) ShowNotice("SHIELD LOW / 방어막 위험", Warning,1.1f,2);
                Pulse(Warning); audio?.TryPlay(AudioCue.Warning);
            }
        }
        private void DestroyFeel()
        {
            if (flightNotice != null) Destroy(flightNotice.gameObject);
            if (praise != null) Destroy(praise.gameObject);
        }
    }
}
