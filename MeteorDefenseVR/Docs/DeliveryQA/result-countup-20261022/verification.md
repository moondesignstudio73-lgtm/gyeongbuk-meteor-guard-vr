# 결과 점수 집계 연출 — 2026-10-22

## 목적과 변경
기존 결과 화면을 유지하고 제목에서 점수를 약 0.8초 동안 집계한다. 본문의 실제 최종 점수·등급·명중률·파괴 수는 첫 화면부터 즉시 표시하며 점수/등급 데이터는 변경하지 않는다. 기존 등급 글자 정착 효과만 1.12→1로 완화해 재사용한다. 전체 결과 패널 이동/확대/순차 줄 공개를 제거했다. 새 카메라 흔들림·섬광·음원 없음.
WorldTextPresentation/WorldTextMotion, 정적 한글 폰트 수정. ResultActionsView의 명시적 클릭만 기존 0.4초 시선 대기를 건너뛰도록 HangarMenuButton에 opt-in을 추가했다. 다른 메뉴와 시선 선택 대기는 유지한다. 숨김 즉시 집계 active=false, 설정을 열면 집계 완료, 일시정지 시 정지, 재시작 시 새 snapshot으로 초기화한다.

## 검증 완료
- final.xml: ResultCountupTests 1/1 통과. 5해상도 × 0/138940/int.MaxValue × 성공/실패 × 초기/중간/완료 = 90개 실제 렌더/글리프/경계/행 간격/불변 점수·등급 검사. 최대 점수·S등급·실패 원본 화면 시각 확인.
- 집계 중 약0.1초의 명시적 UI button event 6회 모두 수락. 일반 OS 마우스 클릭이나 사람 조작을 주장하지 않음.
- scaled-time pause, 설정 열기→완료→닫기(재시작 없음), 새 0점 결과 및 중복 callback 검사 통과.
- fourth.xml: 기존 ResultFeedbackTests와 UiMotionTests 2개 통과. 당시 전용 집계 검사는 실패했으나 final.xml에서 해결.
- 초기 후보에서는 결과 클릭에 시선 gate가 적용되어 막힘. 이후 숨겨진 Editor의 프레임/WaitForSeconds 스케줄링 때문에 샘플 시점이 부정확했다. Time.time 루프와 렌더 probe로 검증 시점을 명시했다. 마지막으로 숨김 즉시 집계 상태가 false가 되도록 보완했다.
- 전/후 캡처는 서로 다른 점수 fixture임. before-settled.png는 이전 배포본 완료 결과, after-counting/after-settled는 최대 정수 fixture. 사람/HMD 검증 아님.

## 복구 체크포인트 / 다음 작업
에디터 검증 완료. 아직 공개 campaign-audio에는 이번 연출이 없다.
다음: 변경 파일과 이 기록/검증 자료만 커밋(사용자 ProjectSettings 제외). Builds/Sharing/ResultCountup20261022/build.ps1 실행 → package.ps1 -BuildDirectory <clean build> → run-extracted.ps1. passed=true/issues=[] 및 배포 렌더 확인 후 publish.ps1 → verify-public.ps1. 공개 릴리스 확인 뒤 README/기록 갱신. 문맥 압축 후 이 파일을 읽고 이어갈 것. 완료된 격납고 작업으로 되돌아가지 말 것.

## Windows 빌드 완료 / 현재 검사
소스 0897e1bc447d3f6aeed794a51a7bed4bbba64940, clean/non-development Windows 빌드 성공. BUILD_IDENTITY worktreeClean=true. 빌드 후 stale index.lock(활성 git 없음, 30초 이상 경과)을 확인해 제거하고 줄바꿈 인덱스 갱신 후 사용자 ProjectSettings를 stash에서 복원, 백업과 원문 비교 일치.
ZIP 89,513,803 bytes, 222파일 해시 일치, SHA256 3f874cab6bfb40ee9c5ba27b64a63b49ade200d678cd6350923f633ffe5889e1.
현재 run-extracted.ps1 실행 중(session 71057). 추출본 Builds/Verify/RCU20261022/MeteorDefenseVR-Windows-ResultCountup-20261022. --pc-soak --pc-soak-rounds=1 --pc-delivery-qa --pc-result-countup-qa --pc-result-feedback-qa. final-run.json의 실행경로/PID 및 QA/pc-soak.json 확인. 끝나면 실제 배포본 ResultCountup PNG 확인하고 release assets Result-Counting.png/Result-Settled.png 복사. git push 후 publish.ps1, verify-public.ps1. 아직 새 공개 릴리스 없음.

## 첫 Windows 후보 검사
전투 라운드·기본 배포 회귀·기존 결과 가독성 및 결과 90개 렌더/조기 버튼6회 통과. 집계 pause assertion만 실패. Update에서 timeScale=0으로 바꾸는 프레임에 LateUpdate가 현재 Time.time 값을 한 번 반영하므로, pause 직전 프레임의 표시값과 비교한 fixture가 부정확했다. 정지 후 한 프레임을 완료한 값을 기준으로 비교하도록 QA만 수정. 제품 시간 계산은 변경하지 않음. 첫 ZIP 미공개. 다음 소스 커밋 빌드/새 ZIP을 별도 RCU20261022b에 추출해 전체 재검사.
