# 오디오 상태 전환 검사 — 2026-10-22

범위: 기존 음원/소리 설정/HUD/격납고 유지. 신규 생성 및 외부 음악 서비스 접근 없음. 실제 무전/AI 발화 없음, Suno 승인 대기 유지. 기존 BGM의 Suno ID3는 제작 표기 단서이며 계정/요금제/배포 권리 확인이 아니다. 상세 파일 감사는 ../audio-controls-20261022/verification.md 참조.

## 재현 및 수정 1건
음악이 Battle→Boss로 전환되는 중 WindowsVRSimulation.Pause로 AudioListener.pause를 켜면 음원 재생은 멈추지만 AudioManager.Update의 unscaled 시간 페이드는 계속 진행됐다. before-verification.txt의 paused music transition freezes clip and fade=False, before-timeline.csv의 paused-transition/BGM volume은0.1137196→0.000005442032→0.1137529→0.2275로 변했다. 곡 재생 시작 이벤트도 정지 구간에서 발생했다.

수정: AudioListener.pause 동안 음악 전환 및 presentation fade 갱신을 멈춘다. timeScale만0인 Reset의 기존 unscaled 정리는 유지한다. 다른 음량/우선순위/쿨다운/음원은 바꾸지 않았다. 수정 후 같은 정지 구간의 BGM volume은0.07958712로 고정, clip/playback-start-count도 유지됐고 재개 후 전환을 완료했다.

## 연속 관찰과 한계
AudioLifecycleProof는 실제 씬의 상태/API를 제어하고 자연 시간으로 전투→실제 방어막 활성화 및 피격→선체 HP15% 피해→보스 경고/접근→일시정지/재개→실제 보스 피해 파괴→승리→결과→초기화→치명타 피해→5.2초 귀환→결과→재시작을 진행한다. 일반 사용자 자연 플레이와 구분되는 제어된 검사이다. 별도 pc-soak 자연 합성입력 한 판도 배포 실행파일에서 진행한다.

0.1초 간격으로 상태·HP·source.isPlaying·source.volume·source DSP RMS를 CSV에 기록하며 큐 이벤트 타임라인을 별도로 기록한다. pause 시 DSP 콜백이 멈출 수 있어 RMS는 마지막 콜백 값이며 정지 중 새 소리가 났다는 뜻이 아니다. 최종 스피커/믹스 녹음은 아니며 소스별 RMS로 청각적 마스킹을 확정할 수 없다. 실제 청취·사람 직접 플레이·HMD 검증은 미수행이다.

첫 검사의 '방어막 동안 Warning 이벤트가 없어야 함'은 잘못된 조건이었다. 주변 운석의 공간 경고는 HP100에서도 정상이다. 이후 실제 PowerUpManager.Shield 활성화 후 피격을 적용하고 HP/선체 손상등급이 유지되는지 검사하도록 바로잡았다. 별도 게임 결함으로 세지 않았다. 초기 승리 검사는 살아 있는 보스를 두고 상태를 강제 종료해 결과 화면에서 보스 이벤트가 나왔으므로 실제 boss.ReceiveDamage에 따른 파괴/승리 경로로 변경했다. 이 역시 자연 종료 결함으로 주장하지 않는다.

저체력 구간에는 실제 무기/충돌 이벤트와 BGM·UI·방향 경고·공기누출 출력이 함께 기록됐다. 에디터 한 관찰에서 최대 표본 RMS는 SFX0.10623/BGM0.05652/UI0.02882/공간경고0.02075였다. 서로 다른 시간의 소스 수치이며 볼륨 우선순위나 잘 들림의 증거가 아니다. 경고 반복은 관찰 구간에서 제한됐고 승리/패배 이후 새 반복 경고와 초기화 후 잔류 효과 루프는 없었다. 추가 재현 결함이 없어 임의의 ducking/쿨다운 변경을 하지 않았다.

## 에디터 검증
regression.xml: AudioLifecycleTests + BgmIntegrationTests + BgmSceneLifecycleTests 총23/23통과. 반복 Reset/곡 전환/장면 재로드/일시정지 전환 포함. NUnit 루트 시작시간은 도메인 재로드로 각 test-case 시간과 차이가 있으므로 개별 테스트 결과와 타임라인을 기준으로 확인한다. 최종 실행파일 결과는 아래에 기록한다.

첫 Windows 후보 ac7822c에서는 음악 정지 수정은 통과했으나, 이전 자연 플레이가 남긴 캠페인 상태 때문에 보스 파괴가 최종 승리 대신 스테이지 클리어로 이어져 검사 가정이 실패했다. 공개 배포하지 않았다. AudioLifecycleProof에서 최종 승리 전용 검사 동안만 CampaignCompletionHandler를 보관/해제하여 보스의 원래 단일 미션 완료 경로를 검사하고 finally에서 복원하도록 수정했다. 실제 게임 코드는 변경하지 않았으며 캠페인 진행은 별도 자연 플레이 검사와 구분한다. 이후 ffe691a 후보로 재빌드/검사한다.

## 최종 Windows 후보 및 배포
소스 ffe691a366b68a23d09edc2cf53018d649afa13e. 깨끗한 작업트리로 빌드 후 사용자 ProjectSettings 원래 변경을 복원/검증했다. 최종 ZIP222파일 대조통과,89,505,809bytes,SHA2561da0f0d4a3600514dd471e9eeb3b46eb1749a6a43984fbbd7c5bde84777ba18d.

AL20261022b 별도 압축 해제 실행: 2026-10-21T19:52:00Z~19:55:38Z,exit0,passed=true,issues=[]. 자연 합성입력 전투 한 판90.86초/초기화102.10초/89.9FPS. 최종 AudioLifecycle/AudioControls 모두passed=True. 보스 처치에 따른 미션 완료, 정지 중 곡/페이드 고정, 재개 후 전환, 귀환/결과 이후 반복경고 종료, 초기화 후 잔류 효과 루프 없음 확인. 이 실행의 windows-source-timeline.csv와windows-verification.txt를 보존했다. 최종 후보에서 소리 설정의 합성 마우스/키보드·5해상도·기존25큐/4채널출력 검사를 다시 통과했다. 앞 후보에서 통과한 ViewSettings/TelemetryReadable은 실제 제품 변경이 없어 반복하지 않았다(첫 전체보고서 보존).

재실행 저장: 프로세스33136 쓰기 종료→프로세스468 읽기 모두passed=true. 기존 실제 사용자 키와 분리된 QA프로필을 정리했다.

남은 필수 검증: 실제 동시 전투를 사람이 청취하여 공간 경고/BGM/효과음의 지각적 마스킹과 최종 스피커 출력 확인, HMD 실기기 확인. 실제 무전/AI 발화 미구현 및 Suno 승인 대기/권리 증빙 미확인은 그대로다. 이를 완료로 표시하지 않는다.

새 릴리스: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-audio-lifecycle-20261022 . 공개 링크 재다운로드 비교 결과는 public-redownload-verification.json.
