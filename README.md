# VR 운석 맞추기

이동과학차용 아이트래킹 VR 체험 게임입니다. 플레이어는 우주선 조종석에서 지구로 접근하는 운석을 시선으로 조준하고 파괴합니다.

## Unity 프로젝트

- 경로: `MeteorDefenseVR/`
- Unity: `6000.5.10f1`
- Render Pipeline: Universal Render Pipeline 17.5.0
- 현재 개발 단계: STEP 22 + PC 폴리싱 + 3D 그래픽 업그레이드 (Windows / Webcam / Mouse)

## 그래픽 업그레이드 — 2026-08-28

기존 게임 흐름과 입력을 유지하면서 우주·지구, 금속 조종석/격납고, 암석 운석과 열 균열 보스, 청록 HUD/조준, 레이저·폭발을 교체했습니다. 생성 텍스처는 실제 3D 재질과 효과에 사용하며 배경에 콘셉트 화면을 붙인 방식이 아닙니다.

- 비교·변경 사항·이번 빌드의 성능/회귀·남은 한계: [그래픽 업그레이드 보고서](MeteorDefenseVR/Docs/GraphicsUpgrade/REPORT.md)
- 실제 Unity 렌더 검수 이미지: [After](MeteorDefenseVR/Docs/GraphicsUpgrade/After) (`fixture-*`는 연출 검수용 배치)
- 에셋 출처·제작 방식: [ASSETS](MeteorDefenseVR/Docs/GraphicsUpgrade/ASSETS.md)

아래 STEP별 기록과 `Docs/FinalQA`는 이전 단계의 이력입니다. 최신 시각 구성과 검증 결과는 그래픽 업그레이드 보고서를 기준으로 확인하세요. Quest 실기기 성능은 별도 검증이 필요합니다.

## 주요 Scene

- `Assets/_Project/Scenes/Bootstrap.unity`
- `Assets/_Project/Scenes/MeteorDefense.unity`

## 지금 바로 PC에서 플레이

### Windows 실행파일

1. `MeteorDefenseVR/Builds/Windows/MeteorDefenseVR.exe`를 더블클릭합니다. Unity는 필요 없습니다.
2. 기본은 Public Experience + Auto 입력입니다. 화면의 `START`를 바라보거나 마우스를 올려 시작합니다. Enter/Space로도 시작됩니다.
3. 운석을 바라보거나 마우스를 올려 약 0.65초 유지합니다. 클릭 없이 잠금 → 레이저 → 파괴가 진행됩니다.

Public에서는 개발·운영 버튼을 숨깁니다. `R`은 긴급 초기화, `ESC`는 종료입니다. `Start-Operator.cmd`로 실행하면 INPUT MODE / RESET / EXIT가 표시됩니다. `Start-Mouse.cmd`는 카메라 없이 마우스로 실행합니다. 아직 보정하지 않은 웹캠으로 바꾸면 시작 화면으로 돌아가 보정부터 진행합니다. exe만 옮기지 말고 **Windows 폴더 전체**를 복사하세요.

운영 설정은 실행파일 옆 `MeteorDefense-settings.json`에서 변경합니다. 자세한 모드·설정·VR 체크리스트: [운영 안내](MeteorDefenseVR/Docs/FinalQA/OPERATOR_GUIDE.md).

### Unity Editor

1. Unity Hub에서 `MeteorDefenseVR` 폴더를 Unity `6000.5.10f1`로 엽니다.
2. `Assets/_Project/Scenes/MeteorDefense.unity`를 열고 상단 ▶ Play를 누릅니다.
3. 화면의 `START`를 바라보거나 Enter/Space로 시작합니다. 모드는 `PcTestSystem > Experience Configuration`에서 바꿉니다.

이 PC에는 필요한 의존성이 준비되어 있습니다. **새로 clone한 PC**는 Unity를 열기 전에 PowerShell에서 `./MeteorDefenseVR/Tools/Restore-WebcamDependencies.ps1`을 실행해 고정 버전 MediaPipe 패키지를 복원하세요. 다운로드는 개발 환경 준비 때만 하며 게임 실행 중에는 다운로드하지 않습니다. Windows 빌드는 Unity 메뉴 `Meteor Defense > Build Windows PC Test`로 재생성합니다.

### 웹캠 시선 테스트

1. 웹캠을 모니터 위 중앙에 놓고 얼굴 전체와 양쪽 눈이 보이게 합니다. 우선 약 50~70cm 거리에서 시작해 조정하세요. 밝고 고른 정면 조명을 사용하고 역광·안경 반사를 줄입니다.
2. Windows 카메라 설정에서 데스크톱 앱 접근을 허용하고, 다른 화상회의 앱이 카메라를 점유하지 않게 합니다. Auto 모드에서 `START`로 시작합니다.
3. 중앙 → 좌측 → 우측 → 상단 → 하단의 표시점을 고개는 가급적 고정하고 눈으로 따라봅니다. 각 점에서 안정적인 홍채 위치 샘플을 모아 5점 보정합니다.
4. 보정 완료 후 `+` 시선점을 보며 운석을 바라봅니다. `Start-Development.cmd`의 INPUT 메뉴에서만 `시선점 표시`, `얼굴 Debug`, `영상 보기 / 숨기기`를 사용할 수 있습니다. 영상 미리보기는 기본적으로 꺼져 있습니다.
5. 자리를 옮겼거나 커서가 맞지 않으면 Operator 모드의 INPUT MODE → `웹캠 다시 보정`을 누릅니다. 진행 중인 판은 초기화됩니다. 보정이 불안정하면 같은 메뉴에서 마우스 시선을 선택하세요.

웹캠 영상은 **로컬 메모리에서만 처리**하며 저장·녹화·업로드·네트워크 전송하지 않습니다. 얼굴/홍채 랜드마크에서 눈 내부 상대 위치를 계산하고 5점 affine 보정으로 화면 좌표를 근사합니다. 전용 시선 추적 장비를 대체하지 않으며 조명·눈꺼풀·안경·머리 움직임의 영향을 받습니다. 개인별 실제 응시 정확도는 사용자가 직접 보정 후 확인해야 합니다.

### 마우스 / 화면 중앙 테스트

- `마우스 시선`: 커서를 운석 위에 유지하면 LOCKING → LOCK ON → 자동 공격. 마우스 클릭은 필요 없습니다. UI 위에서는 조준하지 않습니다.
- `화면 중앙 시선`: 화면 중앙 Ray로 조준합니다. 우클릭 드래그로 좌우/상하를 제한된 범위에서 둘러볼 수 있습니다. 이동은 없습니다.
- 전체 흐름: Intro → Briefing → Calibration → Tutorial → Practice → Launch → Countdown → Main Game → Boss → Mission Complete → Result → Reset.

### 입력 구조 및 실패 대응

`GazeInputRouter : IGazeProvider`가 기존 `GazeRaycaster`에 연결됩니다. Combat, Meteor, Tutorial은 웹캠 API를 직접 참조하지 않습니다.

- Auto 우선순위: 실제 VR Eyes 데이터 → Webcam → Mouse → Camera Center.
- 웹캠 없음/권한 실패/모델 오류/시작 지연: 마우스로 자동 전환합니다.
- 얼굴 추적이 순간 끊기면 기본 0.18초 유지, 게임 중 4초간 회복하지 못하면 마우스로 전환합니다. 사유는 운영·개발 메뉴에서 확인합니다. 초기 화면에서는 다음 체험자를 기다리며 웹캠을 유지하고, 오류가 난 카메라는 다음 Reset 때 재시도합니다.
- 보정 점당 15초 제한, 흔들리거나 눈 이동 범위가 구분되지 않는 보정은 거부하고 마우스로 진행합니다.
- Mouse/Camera Center는 PC 안내만 표시하고 기기 보정을 생략합니다. VR의 기존 보정 경로는 유지됩니다. 기존 VR 보정 UI는 SDK 자체 보정을 대신하지 않습니다.

Inspector 조정:

- `PcTestSystem > Experience Configuration`: 체험/운영/개발 모드, 기본 입력, 잠금 시간, 본게임 시간, 난이도, 보조, 음량, Debug UI, Operator Controls. 다른 세부 컴포넌트보다 이 운영 설정이 우선합니다.
- `GazeSystem > WebcamGazeProvider`: Smoothing, Sensitivity, Deadzone, Minimum Tracking Confidence, Tracking Grace Period, 추론 FPS, Preferred Camera Name, Preview/Face Debug.
- `PcTestSystem > PcTestControls`: Lock Duration(0.5~0.8초), Target Grace, Collider Expansion(최대1.5배), Magnetism(최대0.3m), Show Gaze Cursor, Show PC UI, Hide UI In VR, Enable Debug Keys, Allow Right Drag Look.
- `PcTestSystem > PcCalibrationController`: 점별 안정 샘플 시간, 전환 대기, 보정 제한 시간.
- `GazeSystem > GazeInputRouter`: Requested Mode. 명령행 `--gaze=mouse`, `--gaze=webcam`, `--gaze=center`, `--no-webcam`도 지원합니다.

PC 보조값은 VR Eye Tracking으로 돌아갈 때 복원합니다. 단계 이동·Tab은 Development에서만 허용되며, `Enable Debug Keys`로 추가 제한할 수 있습니다. R/ESC는 독립적인 `Operator Controls` 설정을 따르므로 Public에서도 사용할 수 있습니다. `Show PC UI`를 끄면 보정 안내까지 숨겨지므로 일반 운영에서는 켜 두세요.

## PC 디버그 입력 (Development 전용, R/ESC 제외)

- `F1`: Intro
- `F2`: Eye Calibration
- `F3`: Tutorial
- `F4`: Practice
- `F5`: Playing
- `F6`: Boss Meteor
- `F7`: Result
- `R`: Reset 후 Boot
- `ESC`: 종료 (일시정지 아님)
- `Tab`: 입력 패널 열기/닫기

### 검증 범위 / VR 실기기 필요 항목

이전 PC 입력 단계의 회귀 테스트는 102개 성공이었으며, 이번 최종 폴리싱에서 새 테스트와 20회 연속 실행을 추가했습니다. 최신 결과는 [최종 QA 보고](MeteorDefenseVR/Docs/FinalQA/REPORT.md)를 확인하세요. 실제 웹캠 보정·전체 플레이는 사용자가 정상 작동을 직접 확인했습니다. 자동 마우스 연속 시험과 실제 사용자 웹캠/HMD 장시간 검증은 구분합니다. 이전 단계 기록은 `MeteorDefenseVR/Docs/PcInputQA/README.md`에 보존합니다.

PC에서는 전체 GameFlow, 마우스 Ray, 잠금, 레이저, 운석 Wave, 점수/HP, 보스, 결과/초기화, HUD/Audio/VFX와 웹캠 근사 입력을 테스트할 수 있습니다. Quest Pro 실제 Eyes 권한·정확도·SDK 보정, HMD 입체 렌더링/성능/멀미/착용 UX는 실기기가 있어야 확인할 수 있습니다. Meta XR SDK는 아직 설치되지 않았으며 이번 PC 입력 추가가 실기기 통합 완료를 의미하지 않습니다.

개발자 QA: `--pc-smoke --gaze=mouse --no-webcam`은 출시 빌드에서 실제 Input System 마우스 이벤트로 전체 흐름을 자동 플레이하고 `Windows/QA/pc-player-smoke.txt`에 결과를 남깁니다. 일반 실행에는 자동 플레이가 없습니다. 숨긴 Windows 창에서는 화면 캡처가 실패하거나 검은 PNG가 될 수 있으므로 UI 검증에는 Editor의 `MeteorDefenseVR.Editor.PcVisualQaCapture.Run`을 사용합니다. 이 도구는 게임 카메라와 PC Canvas만 오프스크린 렌더링하며 바탕화면/웹캠을 촬영하지 않습니다. `--pc-camera-check`는 12초 후 카메라/추적 상태 수치만 로그에 기록하고 종료하며 영상은 저장하지 않습니다. 자동 Unity 테스트에서는 웹캠을 열지 않습니다.

로컬 추론 구현: [MediaPipeUnityPlugin v0.16.3](https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3), [공식 Task API 문서](https://github.com/homuler/MediaPipeUnityPlugin/blob/v0.16.3/docs/Tutorial-Task-API.md). 폰트: Noto Sans KR (OFL, `PcInput/Fonts/OFL.txt`). 상세 의존성은 `MeteorDefenseVR/THIRD_PARTY_PC_INPUT.md`를 참고하세요.

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

## STEP 22 최종 개발 리포트

- 구현 기능, Architecture, Script/Prefab/Scene, Inspector 수치, Placeholder, Known Issue 정리 완료
- 최종 리포트: `MeteorDefenseVR/Docs/STEP22_FINAL_REPORT.md`
- 현재 판정: Editor 기능 개발 완료 / Quest Pro·Meta XR·Android 실기기 통합 대기
