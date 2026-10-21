# REVIEW-06 완료: 일시정지 중첩 수정본 새 공개 ZIP 및 재다운로드 일치 확인

REVIEW-05의 공개본 c07cd11은 설정창 자체 개선만 인정하며, 전체 가독성 통과 결론은 아래 REVIEW-06 반례로 철회합니다. 아래 기록은 버전별 이력입니다.

## 재현/범위/완료조건
- 기준: windows-result-feedback-20261021,소스17960da/문서3a7c2a2.
- 실제 공개ZIP 800x600최대점수성공/1280x720실패 결과의 오른쪽아래 시점설정 글자 작음.
- 생성경로 PcTestOverlay.BuildLookSettings: public 플레이어설정이며 디버그전용아님. 시작/일시정지/결과에표시,전투중에는기존부터숨김.
- 기존 진입150x38/font18,패널620x330/font16~27. Canvas1920x1080 Expand로800x600에서글자와버튼모두매우작아짐.
- 설정4개: 마우스감도/위아래반전/조준점표시/흔들림. 실제현재값과조작설명유지,진입임의삭제금지.
- 5해상도 시작/전투/정지/결과,열기/닫기/뒤로가기,상태선택/비활성/긴문구/현재값을실제렌더전후비교. 픽셀크기/대비/선택영역/안전영역/다른HUD·표적·결과버튼가림검사.
- 실제UI이벤트·재시작초기화·입력충돌검사,結果/강화회귀 유지. 최종ZIP별도실행→새GitHub릴리스→공개재다운로드해시.
- 사용자ProjectSettings변경보존,Suno승인대기유지,직접조작/HMD미검증구분. Drive는검토채팅담당.

## 구현/검증 진행
- 변경전70렌더 저장. 진입버튼은1920에서도150x38로선택높이44미달,800에서는글자약7px/높이약16px.
- 진입320x110/font44,안전여백48.설정패널1180x900,본문40/설명38/제목52.명칭과현재값2행으로전체문구보존.
- 감도최소/최대비활성,반전/조준점/흔들림선택상태반영.선택색에만의존하지않고현재값문자유지.
- 설정모달전체배경으로뒤UI클릭차단,router.UiModalOpen즉시반영,결과자동타이머일시보류/뒤재출격차단.
- Esc는설정닫기우선,일시정지유지.기존WindowsVRSimulation순서(-150)보다앞(-175)에서입력처리.재시작BeforeReset에즉시닫힘연결.
- 화면좌표기반시선머무름0.8초선택(원시추적선).전투시선은막힌채설정선택만가능,같은항목에머무르면연속토글안됨.합성시선검사이며실제웹캠/HMD검증아님.
- 관련일시정지복귀버튼/설명도같은축소결함이어서44/38글자및110높이로확대.
- 첫컴파일의중복OnDisable발견후기존정리처리에병합.기능검사는view-settings-verified에서진행중.

- view-settings-verified:35검사중33PASS,ViewSettings/UnifiedLook입력통합2건실패. 글꼴에버/튼누락,800조준점표시11.94px/닫기영문11.09px,복귀·안내일부12px미달.
- 정적 PcTestKorean 글꼴을 기존 BakePresentationFont 경로로 갱신하여새문구글자포함. 본문44/설명44/설정버튼50으로확대. 자동축소없음.
- 두번Esc 통합검사에키뗌0.12초를명시,새fixture에도첫Esc설정닫기/두번째Esc복귀를별도로추가. 원인확정전합격주장안함.
- 글꼴갱신중Unity가사용자설정파일을기본값으로되돌려,변경없음확인후기보관사용자설정백업복원. 사용자선호는커밋제외.
## 2026-10-21 추가 확인
- 시점 설정 진입을 아래 중앙으로 옮겨 정지 중 오른쪽 위험 표시와 겹침 해소. 320×110/font50, 최소 화면에서 약133×46 선택영역.
- 안내문은 font50/높이160, 아래360 위치로 상태 표시 위에 배치. 본문 44/128, 설명44/128, 버튼50/110. 800×600 원본에서 전체 문구 확인.
- Esc 진단: batch=true로 포커스 자동정지가 원인이 아님. 기존 2프레임 입력은 일부 누름이 관측되지 않음. 키 유지0.12초와 해제0.12초로 실제 입력 이벤트를 검증하며 UnifiedLook 통합검사 PASS.
- 새 해상도 검사에서는 포커스 설정 전에 가상 키보드를 생성한 순서 문제 확인. IgnoreFocus 적용 뒤 가상 장치 생성으로 수정하고 별도 재검증 중. 실제 플레이 코드에 진단로그는 남기지 않음.
- 로딩 중 설정 진입은 숨기고 로딩 완료 후 표시. 합성 시선 검사는 로딩 완료를 기다린 뒤 수행.
- 원본 확인: 800×600 설정 전체/시작/일시정지/결과. 상단 HUD는 유지되지만 설정 배경이 전투 영역을 덮으며 설정 버튼은 결과 동작과 겹치지 않음.
- view-settings-final-20261021.xml: 1/1 PASS. 5해상도80원본, 글자12px이상/선택44px이상/12px안전영역/잘림없음. Esc닫기+복귀 전해상도 PASS, 시선열기/변경/유지/닫기 및 결과타이머/재시작 PASS.
- view-settings-keyhold-20261021.xml 중 UnifiedLook 실제마우스·Esc통합 PASS. 앞선35개 검사중33개기존검사 PASS와 이번2개 수정검사 PASS를 합쳐35개 고유검사 확인(한번의35개재실행으로 표현하지 않음).
- 최종 1920/1280/1024/2560 설정 원본을 추가 확인. 글자 전체 표시 및 선택 상태 확인.
- 소스 커밋 c07cd110bcf9b882e79ab88342fb088c82d5f5df, 깨끗한 작업트리에서 Release 빌드 성공(2026-10-21 20:11:13 KST, developmentBuild=false).
- 사용자 설정은 빌드 후 백업과 해시 동일하게 복원. Unity 저장 뒤 줄바꿈 변경으로 stash 적용이 거절되어, 의미상 diff없음을 확인하고 보관한 원본 복사. stash도 보존.
- 패키지 MeteorDefenseVR-Windows-ViewSettings-20261021.zip 89,467,303 bytes, 222파일 내용별 SHA256 대조 통과.
- ZIP SHA256 e6b28a80548e55bc3e96b5fa1736317116eb6cb0800cb45ab4c004d2224447f4.
- 별도 FinalVerified 폴더 추출, 기본 Direct3D12 플레이어 실행. 실제 출격 및 손상기체 착륙 원본 확인. 종합 보고서 생성 대기.

## 최종 결과
- 별도 ZIP 실행 종합 PASS, issues=[]; 4980점/21파괴/오류0/경고0/중복구독0, 평균89.923 FPS(자동 입력 환경).
- 결과 표시 회귀: 30회 즉시 결과 동작과 재시작 초기화 PASS. 강화 표시 및 시점 설정 5해상도 PASS. 직접 사용자 조작·실제 웹캠·HMD는 미검증.
- 공개 릴리스: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-view-settings-readability-20261021
- release ID 419465694, source c07cd110bcf9b882e79ab88342fb088c82d5f5df, draft=false, prerelease=true.
- 공개 ZIP 재다운로드: 2026-10-21T11:17:06.8823763Z, 크기와 SHA256 모두 로컬 검증본과 일치.
- final-pc-soak.json / final-settings-verification.txt / public-redownload-verification.json 보관. 원본은 Before70 + After80 + 최종설정80, 최종 결과/강화 및 전환 원본은 FinalVerified의 QA에 보관.
- 신규 Suno 음원 승인대기 상태 유지. 사용자 ProjectSettings 변경 보존. Drive/지속검토 상태 변경하지 않음.


## REVIEW-20261021-06 — c07cd11 공개 일시정지 화면 검증 실패
- 기존 REVIEW-05 설정창 확대 자체는 유지하되 **전체 가독성 통과 결론을 철회**. 공개 Settings-Pause.png(1280×720)에서 스테이지 안내·휴식 안내·복귀 버튼 배경이 서로 겹침.
- 공개 원본을 ../pause-priority-20261021/Before/Published-Settings-Pause-1280x720.png에 다운로드하여 보존. 기존 ZIP·릴리스 자산은 덮어쓰지 않음.
- 원인1: Time.timeScale=0으로 안내 애니메이션은 멈췄지만 표시가 남음. 원인2: 휴식 안내는 아래 기준, 복귀 버튼은 중앙 기준이라 16:9에서 서로 겹침.
- 수정: 일시정지 표시 계층을 별도로 제어하여 세계 UI의 텍스트뿐 아니라 패널·Canvas 전체를 일시적으로 숨김. GameObject/컨트롤러/활성 상태를 껐다 켜지 않으며 복귀 시 렌더 상태만 복원.
- 휴식 문구·복귀 버튼을 공통 중앙 기준으로 배치. HUD 안내 남은 시간과 문자 등장 애니메이션도 게임 일시정지 시간에 맞춰 보존.
- 새 증거 경로 ../pause-priority-20261021. 스테이지 안내 시작/중간/끝 직전, 튜토리얼, 연습, 일반전투, 보스 ×5해상도, 설정→Esc→Esc·지원되지 않는 결과Pause 차단 검사 추가. 버튼 배경 전체 영역 간 겹침도 검사.
- 새 테스트 첫 컴파일은 QA 코드의 GazeRaycaster namespace 오기 발견 후 수정. 기능검사 실행 중이며 아직 새 배포 승인 상태 아님.
- tests-focused: 새 일시정지 우선순위 검사 PASS. 대상 조준 고리가 튜토리얼 휴식 문구 뒤에 남는 것을 원본에서 추가 발견해 대상 렌더러도 일시정지 중 숨기고 복귀 시 복원.
- tests-regression.xml: 기존 설정·마우스입력·결과·강화 35/35 PASS.
- tests-final.xml: 조준대상까지 포함한 최종 일시정지 검사 + UI모션·MissionHud 회귀 PASS. 5해상도×7상황×5단계=175 PNG, 상태유지/남은시간/전체버튼사각형겹침/세계Canvas·텍스트·대상숨김/2단계Esc/결과Pause불가 확인.
- 원본 검수: 1920 스테이지중간 정지·복귀, 1280 종료직전 정지,1024 연습정지,800 튜토리얼정지,2560 보스정지. 글자축소 없이 패널/문구 분리. 실제 사람·HMD·웹캠 검증과 구분.
- 최종 소스 9cb85fd63fba38e3ad99e7ca2563cd3326019cc7, 깨끗한 작업트리에서 2026-10-21T20:52:09+09:00 Release 빌드 성공. 사용자 설정과 검토 문서를 stash에서 복원, 사용자 설정은 백업과 내용 동일 확인.
- 새 ZIP MeteorDefenseVR-Windows-PausePriority-20261021.zip: 89,471,877 bytes, 222개 파일별 해시 일치. SHA256 218cb03e454183bd918fc92ebe0a45ff5acea6e9cd7020608024c41c94200dff.
- FinalVerified 별도 추출 실행 중. 이전 ZIP/릴리스 자산 보존. 기본 Direct3D12, 자동 입력·UI이벤트 검사이며 직접 사용자/HMD 검증 아님.

### REVIEW-06 최종 공개 검증
- 새 릴리스 https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-pause-priority-20261021 (ID419502882, draft=false, prerelease=true), 소스9cb85fd63fba38e3ad99e7ca2563cd3326019cc7.
- ZIP 별도 실행 최종 passed=true/issues=[]; 4980점/21파괴/오류0/경고0/평균89.934 FPS, NVIDIA RTX3070/기본Direct3D12. 강제GC 없음, 자동입력 환경 수치.
- PausePriority175원본 + ViewSettings80원본, 결과·강화 회귀 PASS. 일시정지에서 설정→Esc닫기→Esc복귀, 결과에서 설정열기/닫기/타이머/재출격차단 및 결과Pause미지원 경로 확인.
- 최종 실행본 원본 추가 검수: 1920스테이지시작/1280중간/1024연습/800튜토리얼/2560보스의 일시정지 화면. 안내·버튼 배경·설정 진입 간 겹침없음. 스테이지중간 복귀 및 튜토리얼 복귀 원본에서 안내 재표시 확인.
- 공개 재다운로드 검증 2026-10-21T11:58:36.6970649Z. 89,471,877bytes 및 SHA256 218cb03e454183bd918fc92ebe0a45ff5acea6e9cd7020608024c41c94200dff 일치.
- 증거: ../pause-priority-20261021/final-pc-soak.json, final-pause-verification.txt, final-settings-verification.txt, public-redownload-verification.json 및 Before/source.json. 최종 원본은 그 폴더의 FinalVerified/…/QA에 보관.
- REVIEW-05 공개 ZIP/릴리스와 그 FinalVerified 원본은 그대로 보존. 공통 ViewSettings 편집기 회귀는 기존 After 경로를 재생성하므로 REVIEW-05의 불변 버전 증거로는 기존 FinalVerified와 공개 자산을 사용해야 함.
- 사용자 ProjectSettings 선호 보존, 신규 Suno 음원 추가 없음/승인대기 유지. 실제 사람 직접조작·실제 웹캠·HMD 실기기는 미검증. Drive 및 지속검토 상태는 변경하지 않음.

