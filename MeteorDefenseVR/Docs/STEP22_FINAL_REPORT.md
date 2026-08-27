# VR 운석 맞추기 최종 개발 리포트

- 프로젝트: `MeteorDefenseVR`
- Unity: `6000.5.10f1`
- 게임명: `METEOR DEFENSE` / VR 운석 맞추기
- 기준 문서: `VR 운석 맞추기 — Codex 단계별 개발 프롬프트.md`, `VR_운석맞추기_게임_스토리보드.pptx`
- 완료 범위: STEP 2~22
- 최종 자동 QA: EditMode 83/83 통과, 전체 상태 흐름 10회 연속 통과

## 구현 완료 기능

- [x] `IGazeProvider` 기반 Eye/Head/Editor Gaze 입력 분리
- [x] 중앙·좌·우·상·하 5점 Calibration과 Tracking 실패 fallback
- [x] Normal/Fast/Large/Boss/Tutorial 운석 데이터와 Lifecycle
- [x] Gaze Focus → Lock Gauge → LOCK ON 단일 타깃 상태 머신
- [x] Lock 완료 후 좌우 Cannon 자동 Laser 공격
- [x] Hit, 파괴, Score Popup과 중복 공격/점수 방지
- [x] 행동형 Tutorial과 5초 Reminder
- [x] 3개 운석 Practice와 본게임 Score 분리
- [x] 고정 카메라 기반 Hangar/Countdown/Launch 연출
- [x] 3 Wave, 총 25개 운석 Spawn과 정면 시야 제한
- [x] World Space HP/Remaining/SCORE HUD
- [x] Player HP, 무적시간, Normal/No Fail 정책
- [x] 3회 Lock 공격이 필요한 Boss Meteor Climax
- [x] Mission Complete, 잔여 운석 정리, 지구 Focus
- [x] Session 통계, Accuracy, S/A/B/C Rank, Result
- [x] Result 8초 후 완전 Reset과 Ready Screen
- [x] 운영자 `R`, `1`~`6` 키와 Release 비활성 토글
- [x] BGM/UI/SFX/Voice/Ambient 5개 Audio 카테고리와 17개 Cue
- [x] Cue Cooldown과 VR 음량 상한
- [x] Reticle/Laser/Hit/Explosion/Debris/Boss/Score VFX
- [x] 어린이용 Gaze tolerance, Lock 시간, Spawn 시야, Text 크기 보정
- [x] Meteor Object Pool, VFX 재사용, Component cache, Shadow 비활성화
- [x] 전체 흐름 10회 반복, Reset 잔존, Missing Script, Console QA

## 주요 Architecture

### GameFlow

`GameFlowManager`가 Boot, Intro, MissionBriefing, EyeCalibration, Tutorial, Practice,
Launch, Countdown, Playing, BossMeteor, MissionComplete, Result, Reset 상태를 단일 진입점으로 관리한다.
각 시스템은 상태 이벤트를 구독하며 같은 상태의 중복 전환은 거부한다.

### Eye Tracking

게임 로직은 `IGazeProvider`만 의존한다. `VREyeGazeProvider`는 Unity XR Eyes를 사용하고
실패 시 Head Gaze로 전환한다. `EditorGazeProvider`는 Mouse/Camera Center로 HMD 없이 테스트한다.
`GazeRaycaster`는 Raycast와 SphereCast magnetism을 결합하고 Collider별 Target을 캐시한다.

### Meteor

`MeteorDefinition`, `MeteorSpawnData`, `MeteorWaveData`가 데이터 계층을 구성한다.
`MeteorController`는 Spawn/Move/Damage/Destroy/ReachPlayer/Reset Lifecycle을 담당한다.
`MeteorSpawner`는 Wave를 실행하고 종류별 Object Pool을 통해 운석을 재사용한다.

### Combat

`GazeLockOnTarget`이 Idle/Focusing/Locked/Destroyed 상태와 단일 활성 타깃을 보장한다.
`LaserWeapon`은 Lock 완료 시 좌우 Cannon을 교대로 발사하고 Hit/Score 이벤트를 전달한다.

### UI

카메라 기준 약 4m 거리에 World Space Text/HUD를 배치한다. 중앙 조준 영역을 비우고
HP, 남은 운석, Score, Warning, Tutorial, Practice, Result를 가장자리와 단계별 화면에 표시한다.

### Audio

`AudioManager`가 5개 카테고리 AudioSource와 `AudioCueLibrary`를 중앙 관리한다.
`GameAudioEventRouter`가 Calibration, Focus, Lock, Laser, Hit, Score, Damage, Boss,
Mission Complete, Result 이벤트를 Cue로 변환하며 Cue별 Cooldown을 적용한다.

### VFX

`CombatVfxController`가 Hit Flash, Meteor Explosion/Debris, Boss Burst를 고정 오브젝트로 재사용한다.
Reticle 회전/Pulse, Laser 폭 Pulse, Score 상승/Fade, Boss Crack emission을 결합한다.

### Result

`GameSessionStats`가 Score, Destroyed, Shots, Locks, Hit, Accuracy, Damage, Combo, Play Time을 기록한다.
`ResultController`가 불변 Snapshot을 생성하고 `SessionRankSettings`로 Rank를 판정한다.

### Reset

`OperationsResetController`가 Wave/Boss/Meteor/VFX/Laser/HP/Stats/HUD/Tutorial/Practice/
Calibration/Launch/MissionComplete/Result/Audio를 정리하고 Boot Ready 화면으로 복귀시킨다.
Main Game Pool은 보존해 다음 체험의 생성 Spike를 줄인다.

## 생성한 Script

### Eye Tracking 및 VR UX

- `IGazeProvider.cs`, `IGazeTarget.cs`: 입력·타깃 인터페이스
- `EditorGazeProvider.cs`, `VREyeGazeProvider.cs`: Editor와 XR 시선 공급자
- `GazeRaycaster.cs`, `GazeTarget.cs`: 시선 판정과 범용 타깃
- `CalibrationPoint.cs`, `CalibrationSequenceController.cs`, `CalibrationStatusView.cs`: 5점 Calibration
- `VrUxSettings.cs`, `VrUxComfortController.cs`: 어린이용 tolerance·시야·텍스트 정책

### Meteor, Combat, Player

- `MeteorDefinition.cs`, `MeteorSpawnData.cs`, `MeteorWaveData.cs`: 운석·Wave 데이터
- `MeteorController.cs`, `MeteorDefenseLine.cs`, `MeteorSpawner.cs`: Lifecycle·방어선·Pool Spawn
- `BossClimaxController.cs`, `BossMeteorView.cs`, `BossExplosionEffect.cs`: Boss 전투와 연출
- `GazeLockOnTarget.cs`, `LockOnReticleView.cs`: Lock 상태와 Reticle
- `LaserWeapon.cs`, `LaserBeamView.cs`, `ScorePopupView.cs`: 공격과 피드백
- `PlayerHealth.cs`, `PlayerDamageFeedbackView.cs`, `GameOverPolicy.cs`: HP와 실패 정책

### Flow, Tutorial, UI, Result

- `BootstrapSceneLoader.cs`: Gameplay Scene additive 로드
- `TutorialController.cs`, `TutorialPulseView.cs`, `TutorialStatusView.cs`: 행동형 Tutorial
- `PracticeController.cs`, `PracticeHudView.cs`: 출격 전 Practice
- `LaunchSequenceController.cs`, `LaunchEnvironmentView.cs`: 저멀미 Launch
- `MissionHudController.cs`, `MissionHudView.cs`: World Space HUD
- `MissionCompleteController.cs`, `MissionCompleteView.cs`, `MeteorCleanupFade.cs`: 완료 연출
- `GameSessionStats.cs`, `GameSessionSnapshot.cs`, `ResultController.cs`, `ResultView.cs`: 결과 통계
- `ReadyScreenController.cs`, `ReadyScreenView.cs`, `ReadyStartGazeTarget.cs`: Ready/Start
- `OperationsResetController.cs`, `OperatorCommand.cs`: 자동 Reset과 운영 모드

### Audio, VFX, 성능

- `AudioCategory.cs`, `AudioCue.cs`, `AudioCueEntry.cs`, `AudioCueLibrary.cs`: Audio 데이터 모델
- `AudioManager.cs`, `GameAudioEventRouter.cs`: 중앙 재생과 이벤트 라우팅
- `CombatVfxController.cs`: 재사용 전투 VFX
- `VrPerformanceController.cs`: 90fps·Physics callback·Pool/VFX 성능 정책

### 자동화와 테스트

- `MeteorAssetSetup.cs`: 운석 Definition/Prefab/Wave 생성·보정
- 테스트 Script 20개: 각 모듈과 10회 전체 흐름, Scene/Prefab Missing Script 검증

## 수정한 Script

- `GameFlowManager.cs`: 전체 상태 전환 API, 중복 방지, Debug fallback
- `ProjectSceneSetup.cs`: 두 Scene과 모든 Runtime 시스템을 결정적 순서로 구성
- `GazeLockOnTarget.cs`: 전역 Audio 이벤트, 0.62초 Lock, 1.25배 tolerance
- `GazeRaycaster.cs`: SphereCast 보조와 Collider Target cache
- `MeteorSpawner.cs`: 시야 제한, Wave 추적, Object Pool, Component cache
- `BossClimaxController.cs`: 3-hit Boss, 재등장, 완전 Reset
- `LaserBeamView.cs`, `LockOnReticleView.cs`, `ScorePopupView.cs`: Game Feel 개선
- `TutorialController.cs`: 5초 안에 이해 가능한 자동 공격 안내
- `OperationsResetController.cs`: Pool 보존 및 전체 시스템 Reset

## 생성한 Prefab

- `MeteorBase.prefab`
- `Meteor_Normal.prefab`
- `Meteor_Fast.prefab`
- `Meteor_Large.prefab`
- `Meteor_Boss.prefab`
- `Meteor_Tutorial.prefab`

각 Prefab은 MeteorController, Collider, GazeLockOnTarget, LockOnReticle을 공유하고
Definition과 크기·속도·HP만 다르게 구성한다.

## Scene 변경

- `Bootstrap.unity`: `GameFlowManager`, `BootstrapSceneLoader`
- `MeteorDefense.unity`: Main Camera, Gaze/Calibration, Tutorial/Practice/Launch,
  Combat/VFX/Audio, Wave Spawner, HUD/Player, Boss, Complete/Result, Ready/Reset,
  VR UX/Performance 시스템
- Build Settings: Bootstrap 0번, MeteorDefense 1번, 모두 Enabled

## Inspector에서 조절 가능한 중요 수치

| 항목 | 기본값 | 위치 |
|---|---:|---|
| Gaze Magnetism Radius | 0.15 | `VrUxSettings.asset` |
| 일반 Lock Time | 0.62초 | `VrUxSettings.asset`, Meteor Prefab |
| Boss Lock Time | 0.78초 | `VrUxSettings.asset`, Boss Controller |
| Gaze Grace Period | 0.18초 | `GazeLockOnTarget` |
| Calibration Dwell | 0.65초 | Calibration Point |
| Tutorial Reminder | 5초 | `TutorialController` |
| Spawn 시야 | 수평 ±32° / 수직 ±20° | `VrUxSettings.asset` |
| Wave 구성 | 3 Wave / 25개 / 약 41초 | `Wave_01~03.asset` |
| Player HP | 100 | `PlayerHealth` |
| 무적시간 | 0.35초 | `PlayerHealth` |
| Boss HP / Damage / Score | 3 / 30 / 500 | `BossClimaxController` |
| Mission Complete 표시 | 4초 | `MissionCompleteController` |
| Result 표시 | 8초 | `ResultController`, Operations |
| Rank S/A/B | 3000 / 2400 / 1500 | `SessionRankSettings.asset` |
| Audio Master 상한 | 0.7 | `AudioManager` |
| Meteor Prewarm | 종류별 2개 | `MeteorSpawner` |
| 목표 Frame Rate | 90fps | `VrPerformanceController` |

## 실제 VR 기기에서 확인해야 하는 항목

1. Android Build Support와 Meta XR SDK 설치
2. Quest Pro에서 Eye Tracking 권한 요청·승인·거부 흐름
3. Unity XR Eyes Ray와 실제 시선의 중심 오차, Head Gaze fallback
4. 어린이 사용자 5초 내 조작 이해와 Lock 성공률
5. Noto Sans KR Font 포함 후 모든 한글 Glyph와 4m 거리 가독성
6. 10회 연속 실제 플레이의 Ready 복귀와 데이터/VFX/Audio 잔존 여부
7. 90fps 지속, CPU/GPU Frame Time, Draw Call, 발열·Thermal Throttling
8. 최종 AudioClip의 음량, 저음, 갑작스러운 Peak, 좌우 Balance
9. Controller 없이 Gaze만으로 Start부터 Result까지 완주 가능 여부
10. 장시간 착용 시 멀미·눈 피로·조종석 진동 허용 수준

## 아직 Placeholder인 Asset

- 우주·지구·격납고·조종석 Mesh와 Texture
- Meteor 고품질 Model, Texture, Crack/Explosion Particle
- Laser Cannon, Engine, Starfield 고품질 VFX
- 17개 Audio Cue의 실제 BGM/SFX/Voice/AudioClip
- 한국어 내장 Font Asset
- Storyboard 수준의 로고, 패널, Warning 그래픽

현재 Placeholder도 실제 Unity Object와 상태 로직으로 동작하며 단순 이미지 복사는 사용하지 않았다.

## 발견한 Known Issue

- `Packages/manifest.json`에 Meta XR SDK가 아직 없어서 Quest Pro 전용 Eye Tracking 권한 API는 미연결이다.
- 설치된 Unity Playback Engine은 Windows/WebGL뿐이며 Android Build Support가 없다.
- 실제 AudioClip이 없어 Audio Cue 연결과 Cooldown만 검증했다.
- 기본 TextMesh Font의 Quest 한국어 Glyph는 보장되지 않으므로 Noto Sans KR 포함이 필요하다.
- 자동 QA는 Editor 시뮬레이션이며 실제 HMD의 시선 정확도·멀미·성능을 증명하지 않는다.
- Scene은 기능 검증용 저비용 Placeholder이므로 최종 전시 품질 Art pass가 필요하다.

## 검증 결과

- Unity Version: 6000.5.10f1
- EditMode: 83 passed / 0 failed / 0 skipped
- 전체 상태 흐름 + Reset: 10회 연속 통과
- Build Scene/Prefab Missing Mono Script: 0
- Compile Error, NullReference, MissingReference, Leak pattern: 0
- GitHub main 반영: STEP 2~21 단계별 검증 Commit 완료

실기기 검증 전 현재 판정은 **Editor 기능 개발 완료 / Quest Pro 통합 대기**다.
