# STEP 21 전체 QA

## 자동 검증 범위

- 정상 상태 흐름: Boot → Intro → MissionBriefing → EyeCalibration → Tutorial → Practice → Launch → Countdown → Playing → BossMeteor → MissionComplete → Result → Reset → Boot
- 위 상태 흐름과 Reset을 한 테스트 안에서 10회 연속 반복
- 동일 상태 중복 전환 거부 및 Transition Event 중복 여부
- Score, Shot, Hit, HP, Result Snapshot이 Session마다 초기화되는지 확인
- 운석 파괴 중복 방지, 단일 Lock, Wave Spawn 종료, Boss 완료, Audio Cooldown, VFX Reset은 각 모듈 테스트로 회귀 검증
- Build Settings의 Bootstrap/MeteorDefense Scene 순서 및 활성화 확인
- Build Scene과 `_Project` Prefab의 Missing Mono Script 검사
- Console의 Compile Error, NullReference, MissingReference, Leak 패턴 검사

## 자동화 한계와 실기기 QA

다음 항목은 Quest Pro 실기기와 최종 Android 빌드가 있어야 판정할 수 있다.

- Meta XR Eye Tracking 권한 승인과 실제 동공 Ray 정확도
- Noto Sans KR 포함 후 한글 Glyph와 HMD 가독성
- 10회 실제 착용 플레이에서 멀미·피로·어린이 조준 성공률
- 90fps 지속 여부, CPU/GPU Frame Time, Draw Call, Thermal Throttling
- 최종 AudioClip 음량, 저음 압력, 좌우 Balance

실기기에서는 10회 연속 체험 후 매 회 Ready 화면 복귀, 이전 Score/HP/VFX/Audio 잔존 여부를 확인한다.
