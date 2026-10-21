# 오디오 자산·제어 감사 (구현 전)
범위: 기존 소리에 대한 BGM/효과음/무전/AI 독립 음량·음소거·저장 및 경로 정리. Suno 접근승인 대기는 유지하고 재요청/우회/다른 서비스 생성/새 음원 제작을 하지 않는다.

초기 소스 조사: AudioManager는Bgm/UI/Sfx/Voice/Ambient 다섯 AudioSource를 직접 조절하며 .mixer 자산은 없다. BGM/SFX/Voice setter와BGM mute만 있고 독립4채널 플레이어UI/저장은 확인되지 않는다. UI/Ambient와 포탑 servo/charge, 파워업 획득소리 등 별도 경로가 있어 기존 음량을 유지하며 효과음 채널에 연결할 필요가 있다. 실제 믹서 자산을 만들거나 내부API로 수정하지 않는다.

파일: Audio/BGM의 Intro/Tutorial/Battle/Boss/Result.mp3 5개, Audio/Generated WAV18개, Art/CockpitDamage WAV4개. Generated는FinalAudioSetup.WriteCue의 결정적 합성 소스가 확인됨(외부 오디오 의존 없음). BGM은4d07eb4에 기존 파일로 편입되어 현재까지 출처/배포허가 문서가 확인되지 않았으므로 권리 검증 완료로 표시하지 않는다. 상세 GUID/길이/클립실체/재생경로는 추가 조사한다. Voice 분류에 실제 발화가 있다는 뜻은 아니며 무전/AI 녹음 완료를 주장하지 않는다.

완료조건: 기존 믹스 기본값 보존, 네 사용자 채널별 값/음소거/저장, 존재 클립 출력 경로 및 0/음소거/복원 수치, 별도 앱 재시작 저장, 일시정지/초기화 루프 및 AudioSource 누적, 설정 뒤 입력 차단/Esc/기존 시점설정 회귀,5해상도 원본 가독성. PCM/출력 수치와 청취 수행은 분리. 사용자 ProjectSettings·기존 PlayerPrefs 보존. 수정 시 클린 빌드/ZIP별도실행/새GitHub릴리스, Drive 변경 없음.

## 출처 추가 조사 (2026-10-22)
BGM 편입은 4d07eb461961287c7d54d2b9c2313ac9ce80de4d(2026-08-30 KST)이며 커밋 메시지는 snapshot이다. 해당 파일들의 .meta userData는 비어 있다. 내부 ID3를 직접 읽은 결과는 bgm-metadata.json, 파일 해시/GUID 및25큐 연결은 asset-inventory.json에 보존했다.
- Intro: Starlight Horizon / moondesignstudio73 / made with suno /2026-08-27T19:35:41Z/373245c6-d36d-4ccd-9250-fd05a76ceb06.
- Tutorial: Digital Calm /동일 artist/2026-08-27T19:39:35Z/cfa0364d-b7f8-4d66-aad6-21c7c699fe9d.
- Battle: Neon Horizon /동일 artist/2026-08-27T19:40:13Z/0121dec6-4199-44ec-96ee-566df77023c5.
- Boss 및 Result: Asteroid Impact /동일 artist/2026-08-27T19:41:09Z/bb502ee0-a54e-473b-82fd-e1aed001710d. SHA256가 동일한 파일이다.
판정: 위 제작 표기가 로컬 파일에 있다는 것은 확인됨. 실제 계정/곡 페이지와 대조한 것은 아니며, 생성 당시 요금제·배포 허가·권리 이전을 입증하는 자료는 없음. 따라서 출처 단서는 확인, 배포 권한은 미확인으로 유지. 기존 문서 STEP22_FINAL_REPORT와 PACKAGE_REVIEW도 권리 검증 완료 증거가 아니다. Suno에 새 접근/승인을 요청하거나 대기 중인 접근을 우회하지 않았다.

Generated WAV18개는 FinalAudioSetup의 기존 결정적 합성 코드와 대응하며, CabinAirLeak/HullImpactLayer/GlassImpactLayer는 CockpitImpactAssets.Sound의 기존 합성 코드가 확인됨. 기존 GlassHit.wav는4d07eb4 편입이며 현 AudioCueLibrary의GlassHit는GlassImpactLayer를 사용한다. 미사용 GlassHit.wav의 원 제작 증빙은 확정하지 않음. 포탑 servo/charge와 파워업8종은 기존 런타임 합성 코드에 근거하며 새 음원을 만들지 않았다.

## 구현 경로
기존 AudioSource.volume, BGM fade와 trim은 유지하고 소스 DSP에 사용자 gain을 곱한다. 기본 gain1로 기존 믹스를 유지. Music은 AudioManager의BGM, Effects는UI/SFX/기존Voice비프/Ambient/포탑/파워업/공간경고/누기/보스. 씬 소스 및 런타임 파워업·보스 생성 경로에 필터 부착. 보스의 사용되지 않는 선택적 구형 PlayClipAtPoint 경로는 기존SFX소스를 사용하도록 바꿔 임시AudioSource를 추가하지 않는다. 믹서 에셋은 없음, 모든 원본 outputAudioMixerGroup=null. 무전/AI실제 음성은없으며UI에음성준비중 표시.

Prefs는MeteorDefense.Audio.v1 별도 네 볼륨/네 mute. QA는별도QA키로 검사 후 복원하며 기존 사용자키를 덮어쓰지 않는다. 첫 자동검사에서 네 채널DSP full/0/40%/mute/unmute 및Laser/Countdown/BossWarning/GlassHit/PlayerDamage 신호 확인. UI 검사에는 보완할 입력 좌표/캡처 디렉터리 문제가 발견되어 전체 통과로 처리하지 않았다. 디지털 PCM 수치와 실제 스피커 청취는 별개이며 청취는 미수행이다.

## 구현 및 검증 진행 (2026-10-22)
AudioUserPreferences의 네 독립 채널과 PlayerPrefs 저장을 추가했다. 기존 소스의 authored volume/fade는 유지하고 AudioChannelFilter에서 최종 사용자 gain을 곱한다. 실제 발화 자산은 없으므로 기존 Voice 비프 큐는 효과음으로 분류하며 무전/AI 항목은 준비 중으로 표시한다. 새 음악·음성 생성이나 외부 서비스 접근은 하지 않았다.

새 UI 글자 중 듣/효 누락을 실제 렌더에서 발견하여 기존 PcTestKorean 정적 글꼴을 Unity API로 추가 베이크했다. 글꼴 문자 포함 여부를 검사에 추가했다. Boot/Result에서도 Esc로 설정을 닫도록 입력 순서를 수정했다.

에디터: final-editor-2.xml 1/1 통과. 네 채널의 full/zero/restore/mute/unmute DSP 수치, 기존 25큐 출력, 다섯 해상도 글자/버튼, 연속 모달 상태, 결과 타이머/재출격 차단, 일시정지·초기화 반복을 확인. 배치 에디터는 합성 입력 이벤트를 소비하지 않아 시선/API 경로를 명시적으로 사용했다. Windows 입력 검사로 대체 주장하지 않는다. 첫 강화 검사에서 해상도 복원 직후 배치 갱신 전 일시정지 클릭이 실패했고 레이아웃 반영 후 검사하도록 수정한 뒤 통과했다.

첫 미배포 빌드 f468705는 Windows 검사 실행기의 오디오 검사 호출 누락을 발견해 중단했다. 해당 ZIP은 unreleased-f468705.zip으로 보존했으며 배포하지 않았다. 호출을 추가한 최종 후보 60d96889c9956100b94ed8a3819f33600a6d4ad3는 깨끗한 작업트리에서 빌드했고 사용자 ProjectSettings 변경은 보존/복원했다.

## 최종 Windows 결과
60d9688 빌드 ZIP을 AC20261022b의 별도 폴더에 압축 해제하여 실행. 2026-10-21T18:45:24Z~18:52:36Z, exit 0, pc-soak passed=true, issues=[]. 자연 전투 한 판 result90.91초/reset102.15초/89.9FPS. TelemetryReadable, IntroTransition, ViewSettings, AudioControls 검사가 완료됐다.

AudioControls/verification.txt의 actual InputSystem mouse opens, 네 채널 down/mute/restore, Enter/Space 뒤 시작 차단, pause/첫Esc닫기/둘째Esc재개, result타이머·재출격차단/결과Esc/reset닫기 모두True. 5해상도(800×600,1024×768,1280×720,1920×1080,2560×1080) 렌더는 합성 시선으로 조작하고 글자포함/경계/버튼크기 및 각30연속프레임 모달상태를 검사했다. 일반 화면의 실제InputSystem 마우스·키보드 경로와 해상도 렌더용 시선 경로를 구분한다. 800×600과1920×1080 최종 실행 렌더를 직접 이미지로 재확인했다.

네 채널×5단계 출력 수치 통과, 기존25큐 출력 신호 모두 확인. 예: Music full in/out0.009046497, zero out0, .4복원 in0.008703294/out0.003481318, mute out0, unmute in0.007660439/out0.003064176. Radio/AI 경로 검사는 기존 음악을 신호로 사용했으며 발화 검증이 아니다. AudioSource 누적/잔류루프 없음과 사용자 설정 보존 검사를 통과했다. 사람의 청취/직접플레이/HMD는 수행하지 않았다.

별도 실행 프로세스24488에서 QA설정(.2/.4/.6/.8, 홀수채널음소거)을 저장 후 종료하고, 프로세스40116에서 읽어 일치 확인. 두 실행 모두 exit0/passed=true. QA전용 키를 사용하며 기존 실제 사용자 설정을 덮어쓰지 않았다.

최종 ZIP: MeteorDefenseVR-Windows-AudioControls-20261022.zip,89,502,563bytes,222파일 대조통과,SHA256663b53527964a821ad5cd3b8ee4647d1354764d416d6fb30e6ea6ade27e0a18a. 배포 링크: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-audio-controls-20261022 . 공개 재다운로드 검증은 public-redownload-verification.json 참조.
