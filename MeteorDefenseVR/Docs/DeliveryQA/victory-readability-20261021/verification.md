# REVIEW-20261021-07 성공 안내 시인성
Before: 공개 source6201459 / SHA256 abde1084fe88eab7c1bebbc65f8956b5c92d64c130eb306faa35496523749eef. 공개 PNG800/1280을 Before에 보존.
수정 범위: 최종 성공 안내 전용 글자/패널 크기. 공통 안내 기본값은 복원하여 유지. 자동 렌더 검증이며 사람/HMD/웹캠 평가는 별도. 사용자 ProjectSettings 및 Suno 승인대기 보존.

첫 확대 검증: after.xml은 PASS였지만 After/800x600 원본의 두 번째 줄이 잘려 불채택. 최종 검증에 두 줄 렌더/잘림 없음/preferred 크기/한글 글리프 quad20px 이상 검사를 추가하고 실제 PNG 잉크 높이를 별도로 측정한다.
Before 실PNG: 800×600 한글 잉크 높이11px/줄 사이6px, 1280×720 높이15px/줄 사이9px. 녹색 실제 픽셀 추출. 검은 패널 대비 내부 글자 픽셀 평균 대비 약9:1이며 안티앨리어싱 가장자리 최저 대비를 의미하지 않는다.
두 번째 후보는 두 줄을 모두 표시했지만 일부 한글 글리프의 개별 quad 높이가17.14px로 새20px 기준에 미달하여 불채택(final-editor.xml/FinalEditor). 글자60으로 추가 확대하고 패널720×232/텍스트672×200 안에서 동일 문구를 유지한다.

## 최종 에디터 검증
approved-editor.xml 17/17 PASS (VictoryEarthResponseTests, GameplayHudOverlayTests, ResultFeedbackTests, FinalPolishTests). 5해상도 모두 성공 두 줄/비잘림/전체 문구/크기 검증 PASS, 카운트다운 복귀 시 원래 글자25/패널620×108 복원 확인. 성공 스타일만 분기하므로 기존 튜토리얼/경고/단계/능력 안내 크기 계산은 유지.

| 해상도 | 실제 한글 잉크 높이(두 줄) | 줄 사이 빈 픽셀 | 내부 글자 평균 대비/검정 |
|---|---|---|---|
|800×600|25/25px|19px|10.72:1|
|1280×720|35/35px|26px|11.22:1|
|1920×1080|52/52px|40px|11.50:1|
|1024×768|32/32px|25px|11.09:1|
|2560×1080|59/60px|46px|11.64:1|

TMP 최소 한글 quad 높이는800에서21.43px이며 픽셀 투영 전체 줄 높이와는 다른 지표다. 실제 PNG 픽셀 분석은 measure.ps1/pixel-metrics.json, 렌더 상태 검증은 ApprovedEditor/verification.txt. 800/1280/2560 원본 시각 확인에서 지구 외곽/중앙 조준 영역과 안내창 분리, 결과 때 숨김. 테스트는 실제 보스 피해 이벤트를 사용하는 자동 장면 구성으로 사람의10스테이지 완주가 아니다.

## 빌드 및 패키지
소스3492913027611a358985923cd82831bb6586cc11, Unity6000.5.10f1, 비개발 빌드, worktreeClean=true. ZIP89,483,061bytes / SHA25618964b47803bf3672362e9ce57629ff2b055a561586d04871e93571ff92eed29. 압축222항목 원본 해시 일치. 사용자 ProjectSettings는 별도백업/stash 후 원문(줄바꿈 정규화) 비교로 복원 확인. 해당 설정은 커밋하지 않았다.
원본 대비 검토 범위는 성공 고리·안내 묶음이며 항목9 전체 완료가 아니다. Drive 변경은 하지 않는다.

## 최종 ZIP 실행
별도 Builds/Verify/VR20261021에 추출하여 실행: 2026-10-21T14:42:42Z~14:46:58Z, exit0, pc-soak passed=true/issues=[]. 실제 InputSystem 합성 마우스 자연 라운드 결과90.82초/재시작102.06초/평균89.9FPS (RTX3070/D3D12/QA 렌더 환경). VictoryEarth/ResultFeedback/BuffReadability/ViewSettings 모두 PASS.
5해상도 실제 배포 렌더 픽셀 계측(final-pixel-metrics.json)이 위 에디터 표와 일치. 배포800/1280 원본에서 두 줄/안쪽 여백/지구 외곽과 분리 확인. 배포의 CPU/GPU 마커0표본은 측정불가이며 비용0이 아니다. 실제 사람/HMD/웹캠 검증 미수행, Suno 승인대기 및 사용자 설정 보존.

## 공개 완료
새 tag windows-victory-readability-20261021 / release419679177 / source3492913.
https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-victory-readability-20261021
2026-10-21T14:48:05.6526531Z 공개ZIP 재다운로드 SHA256/89,483,061bytes 일치. 검토용800/1280 실제 배포 PNG도 같은 릴리스에 첨부했다. GitHub 최신 안내 링크만 갱신하며 Drive의 검토 채택은 담당 채팅에 남긴다.
