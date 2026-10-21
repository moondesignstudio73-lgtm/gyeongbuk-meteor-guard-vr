# 기본 캠페인 오디오·화면 계약 검증 — 2026-10-22

## 이전 공백 및 재현
이전 audio-lifecycle 검사는 CampaignCompletionHandler를 임시 해제한 단일 미션 승리 경로였다. 이번 검사는 제품 기본 콜백을 처음부터 끝까지 그대로 유지하고(동일 delegate 비교), 보통 난이도10스테이지를 실제 운석/보스 ReceiveDamage로 종료했다. 최초 Playing 진입만 명령하며 후속 스테이지/성공/결과 상태를 직접 지정하지 않는다. Spawner.Tick(3)으로 생성 일정만 가속하고 각 표적의 최대 체력만큼 실제 피해를 적용한다. 보스 경고/등장과 스테이지/결과 타이머는 실제 시간이다. 합성·가속 검사이며 사람의 자연 플레이가 아니다.

수정 전 before-verification.txt: START1은1회, START2~10은각2회, 최종 승리와 결과는각1회. AdvanceStage가 ApplyStage를 호출한 직후 StartMainGame→DifficultyManager.PrepareForPlaying이 같은 ApplyStage를 다시 호출했다. 스테이지 설정·기준점 캡처·StageStarted 이벤트가 중복되는 결함이다. 이 중복이 화면 깜빡임을 유발했다고 주장하지 않는다.

수정 한 건: 실제 flow가 있는 AdvanceStage는 StartMainGame만 요청하여 기존 사전 전환 경로에서 준비를 한 번 수행한다. flow가 없는 독립 컨트롤러 테스트만 직접 ApplyStage를 유지한다. 음악/음원/꾸밈/난이도 데이터는 변경하지 않았다.

## 에디터 전후
CampaignAudioContractTests+StageProgressionTests 7/7통과(after.xml). starts1~10정확한순서/각1회, 중간clear1~9각1회, 최종complete1회/result1회, ClearedStages10/AllStagesClearedtrue,2초관찰중중복result없음, 다시출격실제행동→Playing/Stage1/ClearedStages0/결과숨김. 실제피해210회(일반200+보스10),하단고정HUD표본1170개. 전체 콜백을 보존했다.

800×600에서 단계별Playing/Boss/StageClear/최종승리/결과/재시작을 캡처하고 상태창 글자 포함/경계 검사를 수행했다. 첫 final-result 캡처는 결과 애니메이션 시작 프레임이라 비어 보여, 최종 Windows검사에는2초후 final-result-settled 화면 및 텍스트경계·글자포함 검사를 추가했다. 초반 빈 프레임을 완성 화면 가독성 증거로 쓰지 않는다.

## 오디오 기록과 한계
0.1초 간격 상태/스테이지/위상/BGM대상/AudioSource 재생/volume/RMS CSV와 cue 타임라인. 각 상태의 TargetBgmCue가 GameAudioEventRouter의 대응곡인지 연속 확인하고 MissionComplete/Result/Boot의 새Warning/BossWarning을 감시했다.

QA 전용 Camera AudioListener OnAudioFilterRead에서 기존 혼합 신호를 48kHz/스테레오16bit WAV로 기록했다. 재생하거나 새 음원을 생성하는 코드가 아니며 QA 실행에만 추가되는 컴포넌트다. 에디터 기록75.99초,peak0.2175528,RMS0.0200241,|sample|>1인샘플0. 이 값은 청각적 마스킹/스피커음량/사람의 청취 통과가 아니다. OS·스피커 루프백 녹음이나 HMD 검증도 아니다. 실제 청취는 미수행.

기존무전/AI발화 없음, Suno승인대기 및기존BGM권리증빙미확인 유지. ID3출처단서와권한확인을 구분한다. Drive는 이 작업에서 변경하지 않는다. 사용자 ProjectSettings 변경 보존, 이전고정HUD/격납고/소리설정/음악일시정지수정 유지.

## 최종 Windows 검증 및 배포
소스d7b6b2d3681ab9e0645f2a38689976e74701b774,빌드RC_0.2.0-rc.1_d7b6b2d. 깨끗한작업트리 빌드/사용자ProjectSettings복원검증. ZIP222파일대조,89,509,524bytes,SHA25689d3def23e8ff9daa11dd1307c9be7f63be7ce61c5b266e3f3517a156681fb3f. CA20261022별도압축해제실행2026-10-21T20:48:04Z~20:52:33Z,exit0,passed=true,issues=[].

자연합성입력한판90.80초/초기화102.04초/89.9FPS통과. 이어서기본처리기그대로10스테이지전체검사: 피해210회,고정HUD표본1146회. START1~10각1회,중간CLEAR1~9각1회,최종승리/결과각1회,후속2초관찰중복결과없음,종료상태새경고없음,BGM대상상태별일치. 결과의실제텍스트: 점수112,540/파괴한운석210/명중률0%/S/보통. 피해API가속검사이므로발사명중률0%이며사람의사격성적이아니다. 최종결과800×600렌더를직접확인했고글자포함/경계검사통과. replay실제메뉴행동이초기화된stage1으로이어지고원래완료delegate가끝까지유지됨.

출력녹음: 최종Windows의listener-output.wav를공개릴리스Campaign-output.wav로첨부.48kHz/2채널/77.57초/7,446,528samples,peak0.1855723/RMS0.0201121/|sample|>1샘플0. 시스템스피커최종출력보장/주관적잘들림/청취통과가아님. source-timeline도보존. 추가음량우선순위나경고쿨다운변경없음.

소리설정4채널/25기존큐출력/5해상도및입력검사도최종ZIP에서통과. 별도프로세스48912쓰기→37500읽기저장확인모두true. 기존사용자설정과분리된QA프로필정리. 재배포는검사만추가해서가아니라실제로재현된중복stage-start수정때문이다.

공개릴리스: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-campaign-audio-20261022 . 공개ZIP재다운로드해시결과public-redownload-verification.json. 녹음과완성결과이미지도첨부했다.

남은항목: 실제사람의동시전투청취/스피커·HMD출력및기기성능,실제무전·AI발화,Suno승인및기존BGM권리증빙. 완료로표시하지않는다.
