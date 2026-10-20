# 연습 HUD 가림 수정 — 2026-10-21

## 재현과 수정
- 이전 공개 ZIP: windows-flight-20261020, SHA256 94848f2e90c7c144ae03cd8e74423cdb15f02a8fd5c8281bd6607e473b0f90ba.
- 재현 증거: ../windows-flight-20261020/native-practice-hud-occlusion.png. OS 마우스를 도구로 조작한 실제 창 캡처이며, 인간의 직접 입력 검사가 아니다.
- 원인: 연습 HUD와 안내 문구만 깊이 검사를 받는 3D TMP로 남아 시점 이동 중 조종석 프레임 뒤로 가려졌다.
- 수정: 연습 전용 화면 안전 영역과 기존 always-on-top HUD 재질을 사용한다. 데이터는 원래 PracticeHudView/PracticeController에서 유지한다. HUD 32, 안내 40 기준 글자 크기를 고정하며 AutoSize로 축소하지 않는다.
- PC 화면 상단 HUD 86.5–93.5%, 안내 74–84% 높이에 분리한다. XR에서는 동일 Canvas를 1.6m 앞에 표시하며 추적 카메라 위치/회전은 수정하지 않는다. 실제 HMD 검증은 하지 않았다.

## 검증 구분
- 1920×1080, 1280×720, 1024×768, 800×600, 2560×1080 × 7개 방향: 실제 Unity 게임 카메라 RenderTexture 렌더. 네이티브 창 크기 변경 검사가 아니다.
- MouseGaze 모드의 시점 제어 경로를 테스트 코드로 호출했다. 사람의 물리 마우스 조작이라고 간주하지 않는다.
- 원래 상태를 진행시킨 승리 검사와 강제 점수/피해 fixture를 쓰는 패배 귀환 검사는 별도 기록한다.
- Suno 신규 음악, 실제 HMD/눈 추적, 최대 점수의 전체 결과 화면 가독성 전수 검증은 이 수정 완료 범위에 포함하지 않는다.
## 최종 실행·배포 결과
- 최종 빌드 커밋: `5d6a0dc49d7d443accf504b302792c761c78506e`, worktreeClean=true. HUD 수정 커밋은 `21941d0`이며 다음 커밋은 QA의 격납고 메뉴 판정만 바로잡았다.
- 릴리스: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-hud-safe-20261021
- ZIP: `MeteorDefenseVR-Windows-20261021.zip`, 89,402,341 bytes.
- SHA256: `f70114d299a8f0685089b2b2d18678c1cc5e5fdb4370a2bcaca310c15808d896`.
- GitHub 자산 digest 및 ZIP 내부 222개 파일과 원본 빌드의 크기/해시 일치.
- 최종 추출: `FinalExtract/MeteorDefenseVR-Windows-20261021/`. 실행파일 `MeteorDefenseVR.exe`. 실제 창 시작은 동봉된 `Start-Mouse.cmd`로 확인. `native-final-start.png`에 기록했으나 다른 앱 오류 창이 중앙을 가렸다. 해당 앱 접근이 Computer Use 도구에서 허용되지 않아 네이티브 창 전 구간 조작은 미완료로 구분한다.
- 전체 진행 검사는 **동일 추출 exe**에 `--pc-soak --pc-soak-rounds=1 --pc-delivery-qa --no-webcam -batchmode`를 전달했다. 게임 카메라 렌더를 유지하는 합성 마우스 입력 검사이며 인간의 직접 입력이 아니다.
- 실행 경로/PID/파일 해시: `final-extracted-run.json`. 실행 로그: `final-extracted-delivery.log`.
- 자동 진행: 시작→안내→보정→튜토리얼→연습→출격→전투→보스→승리 결과→초기화 통과. `FinalExtract/MeteorDefenseVR-Windows-20261021/QA/pc-soak.json` passed=true, issues=[]; 종료 코드 0.
- 패배 귀환 별도 fixture: 정상 난이도에서 메인 게임 시작/138,940점/피해 100을 지정했다. 실제 자연 패배 입력 검사가 아니다. pending, 문 닫기 전 점수 보존, 결과 공개, 공개 후 4.7초 가시성, 격납고 메뉴 복귀가 모두 true.
- `QA/Delivery/victory-*`, `failure-approach`, `failure-landing`, `failure-door-closing`, `failure-result`, `failure-after-result`, `restart-standby`에 실제 실행본 렌더 증거가 있다.
- 관련 회귀: `../hud-safe-regression-20261021.xml` 11/11 통과. 안전 영역 렌더 35개(5개 해상도×7개 방향), 고정 글자 크기·문구 폭·카메라 위치 유지 확인. 기존 네이티브 가림 캡처와 `instruction-before.png`를 이전 상태 근거로 보존했다.
- 이전 `CleanExtract` 검사는 승리 전체 진행을 통과했지만 패배 후 GameState가 Result를 떠날 것으로 가정해 실패로 기록됐다. 실제 귀환 메뉴 스크린샷과 기존 BaseFlightCinematicTests는 메뉴 준비 상태를 확인하므로, QA 조건만 `lobby.IsMenuReady`로 수정하고 최종 빌드에서 재검사했다. 이전 실패 로그를 지우거나 통과로 바꾸지 않았다.
- 남은 검증: 사람이 실제 창에서 전 구간 직접 조작, HMD/눈 추적, 최대 점수 결과 화면과 모든 스테이지 배너의 화면 비율별 가독성 전수 확인. Suno 신규 음원은 미포함. Drive 문서와 다른 채팅의 상태 파일은 수정하지 않았다.
- 최종 자동 진행 수치: score=4980, destroyed=21, errors=0, warnings=0, result=90.869987487793s, reset=102.116027832031s. FPS 수치는 증거 캡처 비용을 포함하므로 성능 개선 비교에 사용하지 않는다.
