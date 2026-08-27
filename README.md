# VR 운석 맞추기

이동과학차용 아이트래킹 VR 체험 게임입니다. 플레이어는 우주선 조종석에서 지구로 접근하는 운석을 시선으로 조준하고 파괴합니다.

## Unity 프로젝트

- 경로: `MeteorDefenseVR/`
- Unity: `6000.5.10f1`
- Render Pipeline: Universal Render Pipeline 17.5.0
- 현재 개발 단계: STEP 21 - Full QA

## 주요 Scene

- `Assets/_Project/Scenes/Bootstrap.unity`
- `Assets/_Project/Scenes/MeteorDefense.unity`

## STEP 1 디버그 입력

- `F1`: Intro
- `F2`: Eye Calibration
- `F3`: Tutorial
- `F4`: Playing
- `F5`: Boss Meteor
- `F6`: Result
- `R`: Reset 후 Boot

## STEP 2 시선 입력

- `IGazeProvider`: 게임 로직과 입력 장치의 경계
- `EditorGazeProvider`: 마우스 위치 또는 화면 중앙 Ray
- `VREyeGazeProvider`: Unity XR Eyes 데이터, 미지원 시 Head Gaze fallback
- `GazeRaycaster`: LayerMask 기반 타깃 감지 및 Enter/Stay/Exit 전달

Meta XR SDK는 아직 설치되지 않았습니다. 현재 VR 공급자는 Unity XR 표준 Eyes 기능을 사용하며,
Quest Pro의 실제 시선 권한과 Meta XR 연동은 공식 SDK 도입 단계에서 실기기 검증이 필요합니다.

## STEP 3 시선 초기 설정

- 중앙 → 좌측 → 우측 → 상단 → 하단 순서의 5개 Calibration Point
- 기본 응시 시간 0.65초 및 이탈 시 진행도 감쇠
- Tracking 실패 시 재시도 또는 Head Gaze Mode fallback
- 완료 문구 표시 후 `Tutorial` 상태로 자동 전환
- `MeteorDefense` Scene에 실제 3D 포인트와 안내 TextMesh 구성

## STEP 4 운석 시스템

- `MeteorController`: Spawn, Move, ReceiveDamage, Destroy, ReachPlayer, Reset
- `MeteorDefinition`: Type, HP, Speed, Damage, Score, Size, Targetable 설정
- `MeteorDefenseLine`: 방어선 Trigger 통과 처리
- Normal/Fast/Large/Boss/Tutorial ScriptableObject 정의
- 공통 `MeteorBase`와 5종 Prefab Variant 플레이스홀더

## STEP 5 Gaze Lock-On

- `Idle → Focusing → Locked → Destroyed` 상태 모델
- 기본 Lock 시간 0.7초, 이탈 감쇠 및 0.15초 Grace Period
- 단일 활성 타깃 보장과 새 운석 주시 시 이전 Lock 즉시 해제
- Collider Expansion 및 `GazeRaycaster` SphereCast Magnetism 옵션
- Cyan/Green 진행 링과 `LOCKING`/`LOCK ON` World Space 표시
- Lock-On Reticle Material과 MeteorBase Prefab 연결

## STEP 6 자동 레이저 공격

- Lock 완료 시 `LaserWeapon`이 자동으로 Ray/Beam 기반 즉시 공격
- 좌우 Cannon을 교대로 사용하고 0.08초 중복 발사 방지
- 일반 운석 즉시 파괴, 대형·Boss 운석은 공격 후 다음 Lock을 요구
- `LaserBeamView`와 Muzzle/Hit/Audio/Camera Feedback 연결점
- 파괴 위치에 `+100` 형태의 World Space 점수 Popup
- `MeteorDefense` Scene에 CombatSystem, 양쪽 Cannon, Beam, Popup 구성

## STEP 7 행동형 튜토리얼

- Tutorial 상태에서 피해·점수·이동이 없는 전용 운석 1개 생성
- 실제 Gaze Lock-On과 Laser 공격으로만 완료
- 기본 안내 `운석을 바라보세요.` 및 5초 후 재안내·Pulse
- 성공 시 `좋아요! 이런 방식으로 운석을 파괴하세요.` 표시
- 약 1.5초 후 Practice 상태로 자동 전환
- 실패나 시간 초과로 Game Over가 발생하지 않는 체험형 흐름

## STEP 8 출격 전 연습

- Practice 상태에서 1~3개의 연습 운석을 순서대로 생성
- 실제 Gaze Lock-On과 레이저 공격으로 파괴하며 남은 운석과 연습 점수를 HUD에 표시
- 놓친 운석은 피해나 실패 처리 없이 다시 생성해 끝까지 연습 가능
- 연습 점수는 기본적으로 본 게임 점수에서 제외하며 설정으로 포함 가능
- 모든 운석 파괴 시 `준비 완료!`를 표시한 뒤 Launch 상태로 자동 전환

## STEP 9 우주선 출격 연출

- `HANGAR → DOOR OPEN → ENGINE START → 3 → 2 → 1 → LAUNCH → SPACE → MISSION START` 순서
- 시간 주입이 가능한 상태 머신으로 구성해 연출 속도와 단계 전환을 Inspector에서 조절 가능
- 격납고 양문, 엔진 조명, 전진하는 별 배경, 조종석 지오메트리 진동 플레이스홀더 구성
- 카메라는 고정하고 조종석 진동도 최대 6mm로 제한해 VR 멀미를 최소화
- Door/Engine/Countdown/Launch/Mission Start별 AudioClip 연결 지점 제공
- 완료 시 `Launch → Countdown → Playing` GameFlow 전환

## STEP 10 본게임 운석 Spawn

- `MeteorWaveData`와 `MeteorSpawnData` ScriptableObject 기반 Wave 편집 구조
- Type, Prefab, Count, Interval, Delay, Speed, HP, Damage, Score, Size, Spawn Area 설정
- 기본 3개 Wave, 총 25개 운석, 예상 약 41초의 본게임 스케줄 구성
- Normal → Normal/Fast → Normal/Fast/Large 순서로 단계적 구성
- HMD 정면 기준 수평 ±35°, 수직 ±25° 안에서만 생성하며 Inspector에서 축소 조절 가능
- Playing 상태에서 자동 시작하고 파괴·방어선 도달 운석의 활성/완료 수를 추적

## STEP 11 VR 미션 HUD

- 카메라 기준 약 4m 앞 상단 가장자리에 배치한 World Space HUD
- 좌측 HP 8칸 Bar, 상단 중앙 남은 운석, 우측 SCORE 구성
- Dark/Cyan/Green의 최소한 Sci-Fi 패널로 중앙 플레이 시야를 비우는 배치
- Spawn 시작·운석 완료·ScoreAwarded 이벤트를 통해 수치 자동 갱신
- `+점수 GOOD!`, 고득점 `NICE!`, 발사 시 `LOCK ON` 피드백 제공
- 다음 단계의 PlayerHealth가 직접 연결할 수 있는 HP 갱신 API 포함

## STEP 12 HP 및 실패 조건

- `PlayerHealth`: Max HP, 운석별 Damage, 기본 Damage, 무적시간 설정
- 운석 방어 실패 시 HP/HUD 감소와 `WARNING`, 붉은 조종석 Light, Impact Audio 연결점
- 기본 0.35초 무적시간으로 짧은 시간에 겹친 충돌의 중복 피해 방지
- `Normal Mode`: HP 0에서 `MISSION FAILED`, Spawn 중단, Result 전환
- `No Fail Experience Mode`: HP 0에서도 체험을 계속하며 결과 계산용 누적 피해 보존
- 이동과학차 어린이 체험을 고려해 Scene 기본 정책은 No Fail Mode

## STEP 13 대형 운석 클라이맥스

- 일반 Wave 출현 완료 후 `WARNING / LARGE METEOR DETECTED` 경고와 마지막 운석 자막 표시
- Boss HP 3, Damage 30, Score 500, 느린 속도와 큰 크기 구성
- Laser Damage 1 기준으로 세 번의 독립 Gaze Lock-On 공격 필요
- 붉은 균열 Emission Material과 피해 비례 Crack Light 강화
- 최종 파괴 시 강한 팽창 폭발·Flash Light·Explosion Audio 연결점 제공
- 방어선을 통과하면 PlayerHealth 피해 후 Boss 재등장, 최종 파괴 시 MissionComplete 전환

## STEP 14 미션 완료 연출

- MissionComplete 진입 즉시 일반 Spawn을 중단하고 활성 운석을 분리
- 잔여 운석은 이동을 멈춘 뒤 0.65초 동안 축소 소거
- 지구 플레이스홀더 방향에 `MISSION COMPLETE`와 성공 자막 표시
- 지구 Focus Light와 미세한 Scale Pulse로 시선을 유도하며 VR 카메라는 고정
- Victory BGM/SFX용 AudioSource 연결점 제공
- 기본 4초 표시 후 Result 상태로 자동 전환

## STEP 15 결과 통계와 Rank

- `GameSessionStats`가 Score, 파괴/전체, Shots/Locks, Hit, Accuracy, Damage, Combo, Play Time 기록
- Result 진입 시 불변 `GameSessionSnapshot`을 생성해 결과값 고정
- `MISSION RESULT`, SCORE, DESTROYED, ACCURACY, RANK, 성공 문구 표시
- `SessionRankSettings.asset`에서 S/A/B/C 점수 기준 수정 가능
- 기본 Rank 기준 S 3000, A 2400, B 1500, 나머지 C
- Result 표시 시간은 Inspector에서 7~10초 범위로 설정

## STEP 16 자동 초기화와 운영 모드

- Result 화면을 기본 8초간 표시한 뒤 모든 런타임 상태를 초기화하고 Boot로 복귀
- 점수, HP, 통계, 운석, VFX, Wave, Boss, 튜토리얼, 연습, Calibration, HUD, Audio를 일괄 정리
- Boot 상태에서 `METEOR DEFENSE / START` Ready 화면 표시
- START 버튼 시선 응시 0.8초 또는 `Space`/`Enter`로 체험 시작
- 운영자 키 `R`, `1`~`6`으로 Reset 및 주요 단계 즉시 이동
- Release 빌드에서 운영자 키를 끌 수 있는 Inspector 토글 제공
- Bootstrap Scene이 MeteorDefense Scene을 Additive 방식으로 자동 로드

## STEP 17 Sound 시스템

- `AudioManager`에서 BGM, UI, SFX, Voice, Ambient 5개 카테고리를 중앙 관리
- Intro부터 Result까지 17개 필수 Audio Cue와 교체 가능한 AudioClip 슬롯 구성
- 시선 Focus/Lock, Calibration, Laser/Hit/Explosion/Score, 피해, Boss, Launch, Mission Complete 이벤트 자동 연결
- Cue별 Cooldown으로 짧은 효과음의 과도한 중첩 방지
- Master 및 Cue 볼륨을 최대 0.7로 제한하고 2D 재생·Doppler 비활성화로 VR 급격한 음량 변화를 완화
- 실제 최종 음원은 `Assets/_Project/Audio/AudioCueLibrary.asset`의 비어 있는 Clip 슬롯에 연결

## STEP 18 VFX 및 Game Feel

- Lock Progress Ring 회전과 Lock 완료 Pulse로 응시 진행 상태를 명확하게 표현
- Laser Beam 두께를 4cm 기준으로 보정하고 짧은 수명 동안 폭 Pulse 후 자연스럽게 소멸
- 재사용형 `CombatVfxController`로 Hit Flash → Meteor Explosion → Debris 순서 구성
- Boss 전용 대형 Burst와 기존 Crack/Flash 연출을 함께 재생
- Score Popup에 등장 Scale과 상승·Fade Out 적용
- Cockpit Geometry만 최대 6mm 진동시키며 VR 카메라는 고정
- Reset 시 모든 전투 VFX를 즉시 정리해 다음 체험에 잔상이 남지 않도록 처리

## STEP 19 VR UX 최적화

- 어린이·VR 초보자 기준 시선 Magnetism 반경을 0.15로 확대하고 타깃 Collider 허용 범위를 1.25배로 적용
- 일반 Lock 0.62초, Boss Lock 0.78초와 0.18초 이탈 Grace Period 적용
- 운석 Spawn을 정면 기준 수평 ±32°, 수직 ±20° 안으로 축소
- 튜토리얼 시작 즉시 `바라보기 → LOCK ON → 자동 공격` 조작 원리를 안내
- World Space Text의 Character Size를 최소 0.032로 보정
- VR Camera는 고정하며 Near Clip 0.05 이하, Far Clip 60 이상을 보장

한글 Font Glyph와 최종 가독성은 Quest Pro Runtime에 설치·포함되는 한국어 폰트가 결정하므로
Android 빌드에 Noto Sans KR 계열 폰트를 포함한 뒤 실제 HMD에서 최종 검증해야 합니다.

## STEP 20 성능 최적화

- 운석 종류별 2개를 사전 생성하는 Object Pool을 적용해 Wave 중 Instantiate/Destroy Spike 최소화
- 파괴·방어 실패·Reset 시 운석을 Pool로 반환하고 다음 Session에서 동일 인스턴스 재사용
- Hit/Explosion/Boss VFX는 STEP 18의 고정 오브젝트를 재사용하며 Reset 시 즉시 정리
- Gaze Collider→Target과 Pool GameObject→Controller/LockTarget을 캐시해 반복 Component 검색 제거
- Runtime 목표 90fps, Physics Collision Callback 재사용, VSync 비활성 정책 구성
- Meteor·VFX·Light Shadow를 비활성화하고 Post Processing을 사용하지 않는 경량 Scene 유지
- Unity 생성 폴더 `Library`, `Temp`, `Logs`, `Build` 등은 `.gitignore`에서 제외

실제 Draw Call, GPU/CPU Frame Time, Thermal Throttling과 90fps 지속 여부는 Quest Pro에서
OVR Metrics Tool 또는 Unity Profiler를 연결해 최종 측정해야 합니다.

## STEP 21 전체 QA

- Start부터 Result와 Reset까지 전체 상태 흐름을 10회 연속 자동 반복
- 동일 상태 중복 전환 거부, Transition Event 횟수, Session 데이터 초기화 검증
- Score, Shot, Hit, HP, Result Snapshot의 이전 Session 잔존 여부 검사
- Build Settings의 Bootstrap/MeteorDefense 순서·활성 상태 확인
- Build Scene 및 `_Project` Prefab의 Missing Mono Script 검사
- 전체 EditMode 테스트 83개 통과 및 Compile/NullReference/MissingReference Console 오류 없음
- 세부 결과와 Quest Pro 실기기 항목: `MeteorDefenseVR/Docs/STEP21_QA.md`
