> 이번 묶음 완료: 최초 10개 중 첫 P1(공격 대상 구별/가장 임박한 위협 하나 강조)의 구현·자동 실행 검증·새 공개 배포를 마쳤다. 위험 안내에서 재현한 상태판 겹침도 수정했다. 아래 추적표는 착수 시점, 진행 로그의 미완료는 각 시점 기록이다. 초심자 사용성/HMD·눈 추적/Suno 신규 음원/직접 창 전체 수동 검증은 미완료다. 이 문서를 압축 후 먼저 읽고, 이전 결과 수치 검사나 완료한 이 묶음을 이유 없이 반복하지 않는다.
# 현재 요청 / 인계 — 최초 10개 항목 중 P1 공격 대상 구별
맥락 압축 후 이 문서를 먼저 읽는다. 이전 결과 수치 수정은 windows-result-values-20261021 / 9d2345e로 완료됐으며 이유 없이 반복 검사하지 않는다.

## 이번 범위와 완료 조건
PLAY_REVIEW_20261019.md 최초 항목을 현재 소스와 최신 실행 증거에 대조한 뒤, 가장 높은 미완료 항목 하나만 구현·검증·새 배포한다. 이번 선택은 P1 운석 등장과 위험 방향: 기존 짧은 등장 표시를 재사용하고 등장 후 공격 가능 대상의 구별과 실제 접근 시간에 따른 단일 우선 강조를 보강한다. 기존 시선 잠금/명중 신호를 훼손하지 않아야 한다.

## 최초 10개 추적표 (구현과 검증을 구분)
|항목|현재 구현 근거|최신 실행 검증 범위|남은 부분|
|---|---|---|---|
|P1 운석 등장/위험 방향|LockOnReticleView .35초 등장 표시, MeteorMotionTrail 종류별 꼬리, ThreatRadarMath 거리/접근속도, ThreatIndicatorView 단일 .8초 충돌 예고|최신 승리 전투 렌더 존재. 속도 교체/최우선 가시 신호는 미검증|등장 후 비조준 대상 표시 사라짐. 화면 안 최우선 강조 없음. 이번 범위|
|P1 조준/명중 단계 반응|LockOnReticleView .12초 수렴/잠금 링, LaserWeapon, PremiumCombatVfx, ScorePopupView|최신 자동 InputSystem 전투/21파괴/보스 완료|포신 후퇴·생존 외피 변화와 초심자 구분 관찰은 별도 미완료|
|P1 충돌 예고/방어막|ThreatIndicatorView.Impact 단일 .8초 예고/방향 파문, CockpitDamagePresentation|최신 실패 fixture 귀환·결과. 개별 방어 성공/피해 방향 구분 청감·사용성은 미검증|실제 사용자 원인 이해 관찰 미완료|
|P1 메뉴 선택/전환|SfPresentationDirector/WorldTextMotion, HangarMenuButton 및 난이도 버튼|UiMotion 회귀·최신 재시작/격납고 메뉴 준비 통과|HMD/실제 창 연속 선택 사용성은 미완료|
|P2 도킹/수리 드론|BaseFlightCinematic 손상 귀환/착륙/문 닫힘, 체력 수리 이벤트|최신 추출 실행본 착륙·문닫힘·결과/메뉴 검증|두 수리 드론/스캔 복원 연출은 미구현|
|P2 출격 연속 동작|LaunchSequenceController/손/엔진/기지 출동|BaseFlight 회귀, 추출 실행본 출동/전투 전환 통과|HMD 착용감·반복 준비 단축 선택 확인 미완료|
|P2 보스 외피/핵|BossMeteorView 체력 연동 균열, BossStageMotion 파편, 폭발|최신 보스 처치/승리 흐름 통과|체력 단계별 외피 탈락·핵 노출 형태 변화는 미완료|
|P2 강화 물리 반응|PowerUpManager/Hud, EnergyShield, 회복 균열 이벤트|기존 시스템 테스트 존재. 최신 배포별 강화 조합 시각 검증 없음|포신 띠/보조 발사구/효과 종료 물리 연출 전체 미완료|
|P2 지구 응답/훈장|임무 완료/결과 등급 등장, GameFeel 연속 성공 칭찬|최신 승리·결과, 수치30조합 검증 완료|실제 기록 기반 결과 칭찬/다음 등급 목표/지구 보호 고리 미완료|
|P3 관제 드론/배경 사건|기존 우주 배경|배경 렌더만 확인|관제 드론·수송 사건은 미구현. 이번 범위 아님|

## 발견과 다음 실행 단계
- ThreatProjection.TimeToImpact는 ThreatRadarMath로 연결되어 거리−경계−운석 반경을 실제 방사 접근 속도로 나눈다. 중복 위험 계산기는 만들지 않는다.
- 등장 .35초 표시는 이미 LockOnReticleView에 있으므로 중복 생성하지 않는다. 동일 표시의 낮은 지속 신호와 최우선 한 개의 강조를 연결한다.
- ThreatIndicatorView의 경계 반경4.2 상수를 실제 spawner.ShipCollisionRadius로 연결한다.
- 새 결정적 fixture: 가까운 느린 운석 vs 먼 빠른 운석, 속도 변경으로 우선순위 역전, 파괴/풀 재사용/일시정지/리셋, 배경 암석에는 표시 없음, 화면 안·밖 전환. 관련 잠금 해제/위협 회귀만 실행한다.
- 수정 전후 렌더→실행본 동일 fixture→빌드/새태그/ZIP/공개해시. 현재 구현 전이다.

## 보존 / 미완료
ProjectSettings 사용자 변경(Standalone analytics define 제외) 보존. 새 빌드 때 경로 한정 stash/백업 후 복구, 사용자 변경 커밋 금지. HMD/눈 추적 실기, Suno 승인 대기/신규 음원, 직접 창 전체 조작 미완료 유지. 승인 제한을 우회하지 않는다. 다른 채팅 Drive/상태파일 수정 또는 메시지 전송하지 않는다.

## 테스트 기반 보정
- 최초 baseline 세 번은 게임 화면 검사에 도달하기 전에 NullReference로 실패했다. 준비 객체 문제로 처음 추정했으나 최종 스택의 bool passed 초기화 줄을 확인해, EnterPlayMode 도메인 재로드를 가로지른 캡처 지역변수/콜백 closure가 원인임을 좁혔다.
- 기존 GameFeelTests 패턴대로 EnterPlayMode 뒤 별도 iterator에서 검증을 시작하도록 분리했다. 게임 코드 결함으로 집계하지 않는다. baseline 재실행 중, 실제 위협 표시 수정은 아직 하지 않았다.

## 재현 및 구현
- Before/far-fast에서 가까운 느린 대상TTC27.07초, 먼 빠른 대상4.79초. 계산 선택은 정확했으나 실제 강조된 대상0개. 속도 역전/화면 밖/파괴 후/풀 재사용에서도 표식0개였다. baseline 검증은 수집 모드이며 correct=False를 통과라고 해석하지 않는다.
- 기존 LockOnReticleView의 등장 표식을 재사용해 비조준 공격 가능 대상에 낮은 청록 표시를 유지한다. 새로운 오브젝트/위험 계산기를 추가하지 않는다.
- ThreatIndicatorView의 기존 가장 빠른 도달 대상 하나를 동일 표식의 주황색/두께/펄스로 강조한다. 화면 밖 안내도 같은 대상으로 강조한다. 경계 반경은 스포너 실제값을 사용한다.
- VisualsSuppressed인 교육 상황에서는 새 탐지 표시를 숨긴다. 조준 진행 링, 충돌 판정, 카메라/입력은 유지한다.
- After + ThreatRadarTests + TargetReleaseRegressionTests 실행 중. 결과 가독성30조합은 이번에 반복하지 않는다.

## 우선 처리한 UI 결함
- After/urgent-behind 첫 렌더에서 방향 화살표와39m가 ShipStatusHologram 글자와 겹쳤다. 위험 안내의 실제 UI 결함으로 기록하고 장식 추가보다 먼저 수정한다.
- 경고 위치는 실제 좌우 상태판 RectTransform의 화면 투영 사각형을 피한다. 위치 보간 중에도 겹치지 않도록 같은 검사를 적용하고, 여러 표시는 위/아래 빈 공간으로 분산한다.
- 배치 회귀는 표시 중인 모든 위협 라벨 사각형과 상태판 사각형이 겹치지 않는지 확인한다. 1280×720 및800×600 뒤쪽 경고 렌더를 추가한다.
- 앞선 코드의 위협 계산14개, 잠금 해제3개 통과. 단일 우선 강조/속도 역전/숨김/조준/파괴/재사용/리셋은 갱신 대기를 명시한 재검사에서 모두 통과했다. 현재 새 배치 변경 검사 중.

## 편집기 완료 / 다음 단계
- threat-priority-placement-20261021.xml 1/1 통과. 관련 ThreatRadar 14개 및 TargetRelease 3개도 이전 동일 변경 회귀에서 통과했으며, 표시 색 보강 뒤 TargetRelease3개를 다시 통과했다.
- After/verification.txt: 실제 속도 기반 선택/강조1개, 배경 무표시, 교육 안내 숨김, 조준 우선 강조, 화면 밖 전환, 파괴 후 전환, 풀 재사용, 초기화 모두true. 새 UI 상태판 겹침 검사 및800×600 검사true.
- After/far-fast-800x600 및 urgent-behind-800x600을 직접 확인했다. 뒤쪽 표시가 상태판 위로 이동하여 글자 겹침이 사라졌다.
- 다음: 이 범위 코드 커밋, 사용자 설정 보존 후 새 빌드/포장/추출 실행검증, 공개 릴리스 및 해시 확인. 아직 배포 미완료.

## 코드 확정 / 빌드 진행
- 코드 커밋853c832f0101ea97e4794c5c3d09ac7f98f12ee1. 최초10개 중 첫 번째 P1과 그 위험 안내에서 실제 재현한 상태판 겹침만 수정했다.
- 커밋 전 남아 있던0바이트 Git 잠금은 약590초 동안 변경 없고 Git 프로세스0개, 독점 파일 접근 가능함을 확인한 뒤 해당 파일만 정리했다. 사용자 작업 파일 삭제 없음.
- ProjectSettings 사용자 변경은 별도 백업 및 경로 한정 stash로 보존 중. 빌드 완료 직후 재적용하고 내용 일치를 확인해야 한다.
- 빌드 로그 Logs/threat-priority-build.log, 예상출력 Builds/Windows/RC_0.2.0-rc.1_853c832.
- 다음: 사용자 설정 복구→ThreatPriority20261021/package.ps1→FinalExtract 새폴더에서 --pc-soak --pc-soak-rounds=1 --pc-delivery-qa --pc-threat-qa --no-webcam -batchmode 실행. --pc-threat-qa는 완료된 결과 수치30조합 검사를 생략하고 이번 위협 fixture를 검사한다. 공개 배포는 아직 미완료.

## 빌드 및 포장 완료
- 코드853c832f0101ea97e4794c5c3d09ac7f98f12ee1, RC_0.2.0-rc.1_853c832, clean=true.
- MeteorDefenseVR-Windows-ThreatPriority-20261021.zip, 89,414,644bytes, SHA256 ee6e892b9def706acd7f2351b237c97612280da9946619c370918701f6f3309d. 내부222파일 해시/크기 일치.
- 사용자 ProjectSettings를 stash에서 재적용하고 원본 백업과 내용 동일함을 확인했다. 작업 트리의 해당 변경을 커밋하지 않는다.
- FinalExtract/MeteorDefenseVR-Windows-ThreatPriority-20261021에서 새 실행 중. 실행/PID/해시는 extracted-run.json, 결과는 QA/pc-soak.json 및 QA/ThreatPriority/verification.txt. 아직 공개 배포 미완료.

## 추출 실행본 완료
- 종료 코드0. QA/pc-soak.json passed=true, issues=[], 점수4980/파괴21, 오류0/경고0, 결과90.85초/초기화102.10초. 자동 InputSystem 전투1회다.
- 실패/귀환 fixture의 문닫힘 전 점수 보존·결과 공개·4.7초 가시성·격납고 메뉴 준비 모두true.
- QA/ThreatPriority/verification.txt 전 항목true. 가까운 느린 대상27.01초 vs 먼 빠른 대상4.79초에서 먼 대상을 강조, 속도 변경 뒤13.51초 vs47.95초에서 가까운 대상으로 전환한다. 배경 암석 무표시, 교육 숨김, 조준 중 유지, 뒤쪽 전환, 파괴/재사용/초기화와 실제 상태판 사각형 겹침 검사 통과.
- 실행본 far-fast와 urgent-behind-800x600 렌더 직접 확인. 첫 장면은 기존 스테이지 안내가 사라지는 시점에 촬영되어 편집기 비교 이미지와 구분한다. 26개 촬영 이미지에 검은 화면0, 완료된 결과 수치30조합 재실행 없음.
- 다음: GitHub 새 릴리스 공개 및 공개 ZIP 다운로드 해시 확인. 최초 P1의 구현/자동검증을 완료했으나 초심자 사용성/HMD 평가까지 완료라고 주장하지 않는다.

## 공개 배포 및 현재 상태
- https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-threat-priority-20261021
- release ID418848474, draft=false, target853c832f0101ea97e4794c5c3d09ac7f98f12ee1.
- ZIP/SHA256SUMS와 전후 비교 PNG2개, 뒤쪽 경고800×600 PNG1개를 공개했다. 각 자산 GitHub digest/크기 검증 완료.
- 공개 ZIP을 다시 내려받아 SHA256 ee6e892b9def706acd7f2351b237c97612280da9946619c370918701f6f3309d 일치를 확인했다.
- README 최신 다운로드와 플레이 안내를 갱신했다. 사용자 ProjectSettings 변경은 원래 내용으로 남겨 뒀으며 커밋하지 않았다. 다른 채팅 Drive/상태파일은 수정하지 않았다.
- 최초 추적표1번: 등장 후 표식/실제 속도에 따른 단일 강조 구현 완료, 관련 편집기 및 추출 실행 검증 완료. 초심자 시선 이동 사용성은 실제 사용자 평가가 남아 있다.
- 2~10번의 미완료 항목은 이번에 추가 구현하지 않았다. 후속 지시 시 첫 P1 다음 순위의 실제 미완료부터 확인하되 새로 재현된 UI 결함을 먼저 처리한다.
