# 표적 가림과 수치 경계 검사 — 2026-10-21

## 범위와 입력
- 이전 공개 빌드 `5d6a0dc` / `windows-hud-safe-20261021`에서 시작했다.
- 데이터 제한 확인: 점수 누적은 `RuntimeValueGuard.Add`로 int.MaxValue(2,147,483,647)에 포화한다. 저장 기록은 `LocalRankingStore.Valid`에서 플레이 시간 86,400초(24시간)까지 허용한다. 현재 마지막 캠페인 단계는 10이다.
- 현재 결과의 실제 TMP 포맷은 플레이 시간을 표시하지 않는다. 따라서 결과에는 최대 점수·138,940점·실제 긴 한국어 실패 안내를, 최근 기록 화면에는 최대 점수·24시간을 표시했다. 단계 안내에는 마지막 단계와 현재 가장 긴 실제 부제 `새로운 운석 무리에 대비하세요`를 사용했다. 임의로 긴 플레이어 이름이나 지원하지 않는 단계 문자열을 넣은 검사는 아니다.
- 화면: 1920×1080, 1280×720, 1024×768, 800×600, 2560×1080. 모두 게임 카메라 RenderTexture 증거이며 네이티브 창 조작·HMD 실기 검사가 아니다.
- `ReadabilityBoundaryTests`는 실제 연습 운석을 이동시키지 않고 PC 시점 제어 경로로 화면 중앙/상단 안내 중심에 투영했다. 조준링은 실제 GazeLockOnTarget 포커스 메서드를 호출한 표시 fixture다. 사용자 자연 조준 또는 자동 사격 완료라고 주장하지 않는다.
- `Before`와 `After` 각각 40개 렌더, `measurements.txt`에 실제 표시 문자열·텍스트 경계·화면 투영 좌표를 남겼다.

## 확인된 결함과 수정
- `Before/1920x1080/practice-panel-crossing`과 같은 위치의 `*-without-backing`을 비교하면 검정 안내 배경이 실제 운석 몸체를 가린다. 단순히 운석과 화면 사각형이 가까워 보인다는 추정이 아니다.
- 안내 배경을 문구 폭+40/높이+16 여백에 맞추고, 실제 운석과 조준링 여유 반경이 상단 영역에 들어오면 두 안내 줄을 하단(높이 24%, 13%)에 배치한다. 경계에 넓은 복귀 조건을 두어 위치가 반복해서 튀지 않게 한다.
- 글자 32/40, 기존 글꼴과 배경 대비는 유지한다. 게임 상태/타깃/추적 카메라 위치·회전은 바꾸지 않는다. 매 프레임 숨길 이전 렌더러 목록은 한 번만 찾는다.
- 수정 후에는 실제 운석·활성 조준링 Renderer 경계의 투영 사각형과 안내 패널이 겹치지 않는지도 검사한다. `After/*/practice-panel-crossing`에서 몸체와 조준링이 완전히 노출된다.

## 잘림과 가독성의 구분
- 결과 138,940점/2,147,483,647점, 마지막 단계와 긴 부제, 기록의 24:00:00은 검토한 5개 크기에서 잘리거나 옆 열과 겹치지 않았다. 이번 묶음에서는 해당 레이아웃을 바꾸지 않았다.
- 최대 점수의 숫자 높이는 1920×1080/2560×1080에서 약17.13px, 1280×720에서11.42px, 1024×768에서10.15px, 800×600에서7.93px이다. 기존 점수 축소 포맷으로 생긴 저해상도 가독성 한계이며, ‘잘림 없음’을 ‘모든 상황에서 읽기 충분함’으로 간주하지 않는다. 수치 포맷 변경은 이번의 재현된 가림/잘림 수정 범위에서 제외했다.
- `minGlyphPixels`는 쉼표·가운뎃점처럼 작은 문장부호까지 포함한다. 숫자 크기 근거에는 `minDigitPixels`를 사용한다.
- HMD/눈 추적, Suno 신규 음원, 실제 창 전 구간 조작은 미완료 유지. 외부 오류 창 도구 접근 제한은 우회하지 않았다. 다른 채팅의 Drive 문서/상태 파일은 수정하지 않았다.
## 새 배포본
- 코드 커밋: `4dc6a150bdb34eeae3fe2fdec2430126cdfb7b93`; Windows 빌드 ID `RC_0.2.0-rc.1_4dc6a15`, worktreeClean=true.
- 공개 릴리스: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-readability-20261021 (release ID 418671005).
- ZIP: `MeteorDefenseVR-Windows-Readability-20261021.zip`, 89,408,023 bytes; SHA256 `efa872722dd0eb3aed33bb7c78f972951ab2b46522637db418fe9132558bebfd`. 내부 222개 파일과 원본 빌드 해시/크기 일치. GitHub 자산 digest 일치.
- 릴리스에 `Training-Target-Before.png`, `Training-Target-After.png`를 함께 올렸다. 같은 시점의 실제 연습 운석/표시용 조준링 비교 렌더다.
- 관련 회귀: `../readability-after-20261021.xml` 5/5 통과. 경계 렌더 외 기존 35개 해상도/방향 안전영역 검사와 튜토리얼·단계 전환 회귀를 포함한다.
- 별도 추출 경로: `FinalExtract/MeteorDefenseVR-Windows-Readability-20261021/MeteorDefenseVR.exe`. 실행 파일 해시·PID·입력 구분은 `extracted-run.json`, 로그는 `extracted-delivery.log`.
- 동일 추출본의 `--pc-soak --pc-soak-rounds=1 --pc-delivery-qa --no-webcam -batchmode` 검사: passed=true, issues=[], 종료 코드0. 자동 InputSystem 마우스로 시작→연습→전투→보스→승리 결과→초기화 1회, 점수4980/파괴21, 오류0/경고0. 결과90.74초/초기화101.97초. 렌더 캡처 비용이 들어간 검증이므로 성능 비교로 사용하지 않는다.
- 별도 실패 fixture는 점수138940/피해100을 지정해 실행했다. 문 닫힘 전 점수 보존, 착륙 후 결과 공개, 공개4.7초 후 가시성, 격납고 메뉴 복귀 모두 true. 자연 입력으로 패배한 검사라고 간주하지 않는다.
- 실행본 증거는 `FinalExtract/MeteorDefenseVR-Windows-Readability-20261021/QA/Delivery/` 아래 승리 상태별·실패 접근/착륙/문닫힘/결과/메뉴 이미지와 `failure-fixture.txt`, `QA/pc-soak.json`에 있다.
- 실제 표적 가림 결함이 확인되어 이 수정 묶음만 배포했다. 최초 10개 항목 중 새로운 기능은 이번 묶음에 추가하지 않았다. HMD·Suno·네이티브 창 전 구간 조작의 미완료 표시는 그대로 유지한다.