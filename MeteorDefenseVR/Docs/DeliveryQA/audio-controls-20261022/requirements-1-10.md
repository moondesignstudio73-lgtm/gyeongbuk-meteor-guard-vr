> 후속 구현: [전투 효과음 재생 변형](../combat-variation-20261023/verification.md)은 요구8 중 기존 발사·운석 타격·파괴음에만 pitch0.97~1.03배/gain0.94~1배를 적용한다. 같은 클립의 재생 변형이며 복수 클립 선택·동일 클립 회피 구현이 아니다. 중요 경고·출동·카운트다운·UI·음악·음성·지속 루프를 제외한다. 실제 출력 전후 녹음과 선택값은 해당 기록에 있으며, 청취 품질 통과를 주장하지 않는다.

> 후속 구현: [두 곡 크로스페이드 검증](../bgm-crossfade-20261022/verification.md), 소스1365a7b에서 요구8의 실제 겹침 전환을 구현했다. 최대2소스·최신 요청·경고 감쇠·사용자 설정·정지/초기화와 50개 회귀/별도 실행 검사를 통과했다. 아래 최초 감사의 단일 소스 전환 설명은 당시 상태이며 현재는 이 결과로 대체한다. 루프 이음새·사람 청취·HMD·실제 발화는 여전히 미완료다.

> 후속 구현: [공간 경고 간격 검증](../spatial-cadence-20261022/verification.md)에서 요구2의 접근별 간격·충돌 경로 판단을 추가했고, 48개 회귀와 별도 실행본 검사가 통과했다. 아래 표는 최초 감사 시점(ac95d6a)의 기록이며 이 후속 결과와 함께 읽는다. 요구2 전체(기본 탐지음·새 대상 신호 등)나 실제 청취 완료를 뜻하지 않는다.

# 상세 오디오 요구 1~10 완료표 — 2026-10-22

이 표는 게임 연출 기획의 최초 10항목과 별개다. 검토 채팅 `01a15103-0291-73e2-9cab-052a615bd708`의 상세 오디오 요구 1~10을 2026-10-22 읽기 전용 조회로 확인하고, 기존 자산 감사·실행 증거와 현재 소스를 대조했다. 아래 제목은 원문 요약이다. 이 감사에서 새 실행·청취·생성·빌드·배포는 하지 않았다.

기준 실행본: [windows-bgm-duck-20261022](https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-bgm-duck-20261022), 소스 `ac95d6aa4b919e0fe4bfef14b4f8fbed98a4e94d`, ZIP 89,524,890 bytes, SHA256 `9417ae9ac73c1b148151545a32e5626f91ba62c10b82366d99236d01f86be24d`. 이후 문서 커밋은 실행 코드 변경이 아니다. 최신 공개 재다운로드 일치는 [BGM 감쇠 기록](../bgm-duck-20261022/verification.md)에 있다.

**10개 요구 전체 중 완전 완료로 판정한 항목은 없다.** 부분 구현과 수치 검증을 실제 음성·청취·원본/배포 권한 확인으로 대신하지 않는다.

| 요구 | 실제 구현·재생 자산·이벤트 | 확보된 실행 검증 | 미완료·정확한 범위 |
|---|---|---|---|
| 1. 간헐적 아군 무전, 스퀠치·필터·이벤트/확률/반복 제어 | Radio 사용자 채널만 존재. 실제 발화·무전 클립, 라디오 필터/스퀠치/대사 스케줄러 없음. 기존 Voice 분류 비프는 Effects로 재생 | 네 채널 gain/음소거/저장 경로 검사는 있음. Radio 검사는 기존 음악을 테스트 신호로 사용 | **미구현**: 출격 승인·대상 발견·피격·격추·귀환 무전, 낮은 빈도의 통신 분위기와 대사 반복/확률/간격 정책. 채널 존재는 무전 구현이 아님 |
| 2. 레이더·실제 위협·조준 소리 | `ThreatIndicatorView`가 실제 운석의 도달 예상시간/방향을 골라 `Warning.wav`를 공간 재생. 도달18초 미만 조건,6초 미만일 때 gain 증가. `TargetFocus`/`LockOn` 및 포탑 진행58% charge 음 있음 | [위협 우선순위](../threat-priority-20261021/verification.md), [생명주기](../audio-lifecycle-20261022/verification.md), 최근 공간 경고/동시 상한 출력 검사 | **부분**: 기본 탐지 주기·새 대상 발견 전용 신호·작은 음색 변형 미구현. 경고 간격은 난이도의 `SpatialCueInterval`로 고정되어 접근에 따른 간격 변화는 없음. 실제 적 기체 조준/미사일 시스템·경보 없음: 임의 경보를 만들지 않음 |
| 3. 상태에 맞는 조종석 내부 음층 | `IntroSpaceAmbient.wav`, 포탑 움직임에 따른 기존 런타임 `TurretServo`, 진행 charge, 시동 `EngineStart.wav`, 실제 중손상 공기누출 `CabinAirLeak.wav`, 선체/유리 피격층 | 기존25큐 출력, 조종석/저체력 소스 타임라인, 반복 초기화/정지 후 잔류 루프 검사 | **부분**: 시스템 온·오프, 전력/엔진 지속층, 오류·손상 스파크의 개별 소리, 간헐 통신 잡음 없음. 시각적 스파크는 발음 증거가 아님. 여러 층의 청취 균형 미검증 |
| 4. 실제 한국어 AI 발화 | AI 사용자 채널과 ‘음성 준비 중’ 표시만 있음. 기존 `Countdown`/`Launch`/`Warning`은 전자음 | AI gain 경로만 기존 음악 신호로 검사. 한국어 음성 재생 검증 없음 | **미구현**: 시스템 온라인·엔진 정상·출격 준비·운석 탐지·조준 완료·방어막/선체 손상·긴급 귀환·격납고 접근의 실제 발화. 없는 미사일/적 기체 대사는 제외. 자막으로 완료 처리하지 않음 |
| 5. 출격 단계 동기화 | 실제 `LaunchSequenceController.AudioCueRequested` → router의 EngineStart(0.7초), Countdown/Launch(0.28초 비프), BgmBattle. 실제 순서는 문 열기→시동→가속→우주 진입→카운트다운 | [비행음 감사](../flight-audio-audit-20261022/report.md)의 실제 단계/큐 타임라인, 정지/취소/재진입. 해당 감사는 Editor이고 최신 ZIP에서 재실행했다는 뜻이 아님 | **부분**: 문/격납고 기계음, 승인 발화, 지속 추진 상승음, 통신 잡음 없음. 원문에 나열된 출격 순서와 현재 연출 순서 차이도 남음. 별도 타이머나 부적합 피격음으로 채우지 않음 |
| 6. 전투·근접 통과·실드·저체력·먹먹함·위기 음악 | 실제 발사/피격/파괴/보스 파괴에 기존 Laser/MeteorHit/MeteorExplosion/BossExplosion. 실제 손상에 선체/유리층·중손상 Warning/누기. 경고 시 다른 단기 효과35%, 음악35%로 감쇠; 보스 연출 기존 음악 감쇠와 중첩 안 함 | 생명주기의 실제 피해/실드 상태 검사, 캠페인의 실제 피해API 진행, [우선순위](../audio-priority-20261022/verification.md)·[음악 감쇠](../bgm-duck-20261022/verification.md) 출력 | **부분**: 실제 근접 통과 전용음, 실드 흡수 전용 충격음 연결, 위험 중 무전 끊김, 큰 충격의 청각 먹먹함, 위기 음악의 별도 긴장층 미확인/미구현. 아이템 획득음은 실드 흡수음이 아님. 저체력 경고는 중손상 단계 반복이며 체력에 따른 연속 주기 변화 없음 |
| 7. 패배 귀환 단계·종료 정리 | 패배 시 BgmResult 전환, 귀환 완료 결과 표시 때 Result 비프. 실제 귀환 Progress에 따른 접근/착륙/문닫기 시각 단계 존재. 결과/리셋에서 전투 반복 경고·효과 정리 | 비행음 감사 단계표, 생명주기 패배/성공/초기화, 기본 완료처리기를 유지한10단계 [캠페인 검사](../campaign-audio-20261022/verification.md) | **부분**: 불안정 엔진·무전/긴급 귀환 발화·접근·착륙 장치·착륙·엔진 정지·시스템 종료·통신 종료 소리 없음. 성공은 실패 귀환 장면을 재생하지 않음 |
| 8. 독립 제어·우선순위·반복 억제·곡 전환/루프·감쇠 | 4채널 음량/음소거/저장. 단기 효과8슬롯, 중요 경고 선점/낮은 우선 거절, 큐별 쿨다운, 대기열 없음. 음악 경고 envelope attack.15s/hold.35s/release.8s, 기존 gain과 분리. 보스 감쇠와 min 사용 | 최신29/29 회귀, 추출본 AudioPriority/Controls/Lifecycle, 음악 envelope CSV·DSP·사용자 설정 보존; 이전 별도 프로세스 저장검사 | **부분**: 실제 음성 큐·무전/AI 상호 배제는 없음. 8상한은 단기 효과에만 적용되며 음악/servo/누기 등 별도 지속층을 포함한 총 음수 상한이 아님. 각 큐 단일 Clip이라 연속 같은 클립 회피/변형 풀 없음. 음악은 **한 소스 fade-out→교체→fade-in**이며 두 곡이 겹치는 진정한 crossfade가 아님. loop 설정은 있으나 이음새 청취 미검증 |
| 9. 기존 자산 조사·원본/생성/권리/이벤트 매핑 | [27파일·25큐 인벤토리](asset-inventory.json), [BGM ID3](bgm-metadata.json), 기존 합성 생성 코드 출처 조사. BGM5파일(실질4곡: Boss/Result 동일 해시), Generated18 WAV, 손상4 WAV 및 런타임 포탑/파워업 소리 구분 | 파일/GUID/해시·클립·큐 연결 조사와 기존25큐 실제 출력. 현재 새 자산 없음 | **부분**: ID3 Suno 표시는 단서일 뿐 원본 페이지/계정/생성 당시 플랜/배포 허가 확인 아님. 신규 Suno 접근 승인 대기, 생성 원본 링크/프롬프트/다운로드·권한 증거 미확보. 미사용 구형 GlassHit 원제작 증빙도 미확정 |
| 10. 실제 실행·청취·잔류루프/클리핑·배포 | PC 자동 입력/실제 이벤트 API와 통제된 장면 검사, 기존 음악/효과 DSP, 내부 AudioListener 녹음, 별도 압축 해제 실행, 공개 ZIP 해시 확인 | 정지/출격/전투/위험/피해/보스/실패·성공/재시작 증거는 아래 기록에 분산. 최신29검사·222파일/추출검사 통과. 캠페인77.57초·비행37.35초·최신감쇠13.08초 내부 녹음 | **부분**: 실제 사람 청취·장시간 피로·공간 경고 식별·최종 스피커·HMD·루프 이음새 미검증. 기록된 구간의 above-unity0은 전체 게임 무클리핑 보증이 아님. 합성 입력과 피해API 진행을 사람의 자연 플레이로 부르지 않음 |

## 소스·자산 연결

- [AudioCueLibrary](../../../Assets/_Project/Audio/AudioCueLibrary.asset), [AudioCueEntry](../../../Assets/_Project/Audio/AudioCueEntry.cs): 실제 각 큐의 Clip/loop/volume/cooldown. 기존25큐 출력은 [최신 추출 Controls](../bgm-duck-20261022/extracted-controls-verification.txt).
- [GameAudioEventRouter](../../../Assets/_Project/Audio/GameAudioEventRouter.cs): Focus/Locked/Fire/Hit/Destroyed/Score/Damage/보스단계/출격/결과 실제 이벤트와 BGM 상태 매핑.
- [ThreatIndicatorView](../../../Assets/_Project/PcInput/ThreatIndicatorView.cs): 실제 접근 후보 선택과 공간 경고; [TurretAimingSystem](../../../Assets/_Project/Combat/TurretAimingSystem.cs): 실제 조준 진행·움직임에 따른 charge/servo.
- [CockpitDamagePresentation](../../../Assets/_Project/Player/CockpitDamagePresentation.cs), [PlayerDamageFeedbackView](../../../Assets/_Project/Player/PlayerDamageFeedbackView.cs): 실제 피해·손상등급·누기·피격층. [PowerUpManager](../../../Assets/_Project/PowerUps/PowerUpManager.cs)의 획득음과 흡수 피해 이벤트는 구분.
- [AudioManager](../../../Assets/_Project/Audio/AudioManager.cs), [Voices](../../../Assets/_Project/Audio/AudioManager.Voices.cs), [AudioChannelFilter](../../../Assets/_Project/Audio/AudioChannelFilter.cs), [AudioUserPreferences](../../../Assets/_Project/Audio/AudioUserPreferences.cs): 곡 전환, gain, 단기 효과 상한/우선순위, 네 사용자 채널.
- [FinalAudioSetup](../../../Assets/_Project/Core/Editor/FinalAudioSetup.cs): 기존 Generated 음원의 제작 코드. 새로 실행하거나 재생성하지 않음. [기존 자산 감사](verification.md)의 BGM/손상/런타임 합성 출처를 재사용.
- [비행음 감사](../flight-audio-audit-20261022/report.md)는 문·추진·귀환·착륙의 적합 자산 부재 및 단계별 연결/취소 증거. [캠페인](../campaign-audio-20261022/verification.md)은 기본 캠페인 처리기를 유지한 실제 피해API 가속검사; 단일승리용으로 처리기를 잠시 해제한 [생명주기 검사](../audio-lifecycle-20261022/verification.md)와 구분.
- [최신 감쇠 실행 기록](../bgm-duck-20261022/verification.md), [추출 envelope](../bgm-duck-20261022/extracted-music-envelope.csv), [추출 priority](../bgm-duck-20261022/extracted-audio-verification.txt): 적용 source gain과 실제 DSP를 함께 측정. 사람 청취 자료가 아님.

## 이번 감사의 판정과 다음 작업

새 실행으로 재현된 결함은 없으며 기존 증거와 소스의 누락을 분류했다. 따라서 제품 코드·음원·빌드를 변경하거나 같은 검사를 다시 실행하지 않았다. 새로운 배포로 가장하지 않고 위 실행본을 유지한다. 특히 한 소스의 곡 전환을 ‘크로스페이드 완료’, 고정 간격 공간 경고를 ‘접근별 경고 주기 완료’, 채널 테스트를 ‘음성 구현 완료’로 표현하지 않는다.

차단 사항은 서로 다르다.

1. **제작/접근**: 신규 Suno 접근 승인 대기와 적합한 문·추진·귀환·착륙/실제 발화 자산 부재. 승인 재요청·우회·다른 생성서비스·직접 합성 대체를 하지 않음.
2. **원본/권한**: 기존 BGM ID3 단서는 있으나 계정/원본·생성 조건·배포 권한 증거 미확보. 새 생성 접근과 별도 문제다.
3. **환경/관찰**: 사람의 청취, 최종 스피커와 HMD 관찰 미확보. 수치검사로 이를 통과 처리할 수 없음.
4. **남은 PC 구현**: 접근별 경고 간격, 실제 새 대상 감지 신호 정책, 단일 큐 반복/변형 정책, 진정한 두 곡 crossfade는 별도 미구현 항목이다. 전부 Suno 때문에 막혔다고 표현하지 않음.

다음 후보 중 기존 자산만으로 범위를 작게 잡을 수 있는 것은 **실제 운석 도달 예상시간에 따른 공간 경고 간격**이다. 현재 경고 gain은 변하지만 간격은 고정이다. 착수 시 `SpatialCueInterval`/Warning 쿨다운/중앙8슬롯과의 관계를 먼저 정하고, 안전거리 무경고·접근 시 단조 간격 감소·선회/후퇴/대상 소멸·여러 대상 중 하나·정지/종료/초기화·중요 경고 과다발생 억제를 완료 조건으로 삼아야 한다. 이번 감사에서는 이를 구현·검증했다고 표시하지 않는다. 청취 가능한 환경에서는 기존 녹음의 마스킹/루프 이음새 평가가 별도의 다음 항목이다.

사용자 ProjectSettings 변경과 고정 HUD 유지. Drive 갱신은 이 작업에서 수행하지 않았다.
