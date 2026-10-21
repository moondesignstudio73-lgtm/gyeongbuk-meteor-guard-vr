# 출격·귀환 오디오 단계 감사 — 2026-10-22

이번에는 제품 수정과 새 배포를 하지 않았다. 현재 배포인 windows-campaign-audio-20261022의 제품 코드를 유지한 에디터 실행에서 기존 연결/정리 계약을 점검했다. 추가한 것은 테스트와 증거 문서뿐이다. 이번 감사가 기존 Windows ZIP 안에서 새로 실행된 것은 아니며, 이전 Windows 캠페인/설정 검증과 구분한다.

## 실행 결과
FlightAudioAuditTests 1/1 통과(editor.xml). 기본 연출 시간을 유지한 실제 LaunchSequenceController 단계와 AudioCueRequested/CuePlaybackRequested 이벤트를 기록했다. 제어된 상태/피해 입력 검사이며 사람의 플레이/청취/HMD 검증이 아니다. 성공 구분 검사는 CompleteMission 상태 진입을 명시적으로 요청한 독립 검사이고, 자연 캠페인 승리 증거는 이전 campaign-audio 감사에 있다.

| 실제 시각(검사 시작 후) | 연출 단계 | 기존 요청과 출력 | 판정 |
|---|---|---|---|
|5.055초|격납고 출격 시작|BgmBattle 선택|기존 연결 확인|
|5.434초|문 열기|Door 요청 발생, 대응 재생 큐 없음|전용 문 자산 없음, 미완료|
|6.136초|엔진 시작|Engine 요청과 EngineStart 재생 같은 단계|기존 0.7초 엔진 시동음 연결 확인|
|7.405초|가속|Launch 요청과 Launch 재생 같은 단계|0.28초 전자음만 있음. 지속 추진음으로 완료 처리하지 않음|
|10.073/10.527/10.983초|3/2/1|각 Countdown 요청과 재생|전자음이며 실제 숫자 발화 아님|
|11.439초|임무 시작|Launch 전자음 재사용|현재 연결 확인, 무전 발화 아님|
|12.705초|패배 귀환 접근 시작|별도 접근 큐 없음|미완료|
|13.194초|귀환 중|BgmResult 페이드 전환 완료|기존 배경음악 전환|
|16.163초|ReturnProgress 약0.588, 착륙 시작 구간|전용 착륙 큐 없음|미완료|
|17.066초|ReturnProgress 약0.763, 문 닫기 시작 구간|전용 문 닫기 큐 없음|미완료|
|18.118초|ReturnProgress 약0.964, 문 닫힘|전용 종료 소리 없음|미완료|
|18.298초|귀환 완료/결과 공개|Result 큐|결과 공개와 연결됨|

귀환 단계 이름은 BaseFlightCinematic.AnimateShot에 있는 접근0~.58/착륙.58~.76/문닫힘.76~.96의 같은 ReturnProgress 값을 읽어 대조했다. 별도 타이머로 재생을 흉내내지 않았다. CSV의 engine/speed 열은 출격 LaunchSequenceController의 값이므로 귀환 엔진의 실제 크기/속도로 해석하지 않는다.

현재 출격 순서는 문 열기→시동→가속→우주 진입→카운트다운이다. 이 순서를 바꾸라는 명시 요구나 별도 적합 자산 없이 새 연출 순서를 도입하지 않았다. 전투 시작에서 추가 Countdown 전자음도 관찰됐으며 이를 숫자 발화/AI 안내라고 부르지 않는다.

## 기존 자산 적합성
Assets/_Project/Audio/Generated/EngineStart.wav는 FinalAudioSetup에서 시동용 상승 저주파+노이즈로 생성된 기존0.7초원샷이다. EngineStart 단계에 이미 연결되어 있다. Launch/Countdown은 기존 단일음 기반0.28초전자 큐다. 기존27오디오파일/25큐 인벤토리에서 문 개폐, 지속추진, 저속귀환접근, 착륙충격 전용 자산은 확인되지 않았다. GlassHit/HullImpact/BossExplosion 등을 문·착륙으로 임의 전용하지 않았다. 새 파일/음원 생성, Suno 접근 우회 없음.

## 경계 검사
- 출격 EngineStart에서 실제 WindowsVRSimulation.Pause/Resume: 단계/엔진 값 정지 및 재개 통과.
- 귀환에서 Time.timeScale=0와 AudioListener.pause를 제어하여 애니메이션 정지 확인. 이는 귀환 화면에서 사용자 Esc 일시정지 기능이 제공된다는 뜻은 아니다.
- 귀환 완료 전 실제 ChooseFromResult(true) 거부, 착륙/문닫힘 후 결과 표시 통과.
- EngineStart 중 초기화: 출격 장면/실행 상태 종료, 잔류 효과 루프 없음.
- 귀환 중 초기화: pending result와 장면 정리, 잘못된 결과 노출 없음.
- 성공 결과에서는 실패 귀환이 재생되지 않음.
- 결과 이후 실제 재출격 메뉴 행동→Playing→BgmBattle 복원 통과.

검사 경로에서는 새 엇박자/정리 결함이 재현되지 않았다. 독립 타이머, 장식, 신규 음원 또는 잘못된 대체음으로 상세 요구를 완료 처리하지 않는다. 적합한 자산이 없어 신규 연결도 하지 않았다.

## 녹음 및 한계
listener-output.wav: 게임 내부 AudioListener 혼합 출력37.35초/48kHz/2채널,peak0.3314415,RMS0.0161438,|sample|>1샘플0. SHA256d1b6380d96e00304ef67fdd1fd501d477c523662d750b6b07e72b0caff26eefd. 파일은 로컬 QA 폴더에 보존했다. OS·스피커 녹음 또는 청취 완료 증거가 아니다. 문/착륙 부재 판정은 자산 및 큐 연결 감사에 근거한다.

필수 잔여: 문 개폐·지속 추진·귀환 접근·착륙에 적합한 오디오 자산과 연출 단계 이벤트 연결, 실제 카운트다운/무전/AI 발화, 사람의 동시 전투 청취와 HMD 확인. 실제 무전/AI는 없는 상태다. Suno 승인 대기 및 기존BGM ID3단서/배포권리증빙미확인 구분을 유지한다. 고정HUD·사용자ProjectSettings 변경은 보존했다.
