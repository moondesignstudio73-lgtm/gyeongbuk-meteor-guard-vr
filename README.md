# VR 운석 맞추기

이동과학차용 아이트래킹 VR 체험 게임입니다. 플레이어는 우주선 조종석에서 지구로 접근하는 운석을 시선으로 조준하고 파괴합니다.

## Unity 프로젝트

- 경로: `MeteorDefenseVR/`
- Unity: `6000.5.10f1`
- Render Pipeline: Universal Render Pipeline 17.5.0
- 현재 개발 단계: STEP 4 - Meteor Base System

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
