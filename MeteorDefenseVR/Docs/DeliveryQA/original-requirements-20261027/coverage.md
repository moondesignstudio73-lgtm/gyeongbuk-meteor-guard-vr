# 최초 연출 요구와 현재 배포본의 구현 및 검증 대조

기준 제품은 **372a75f438e40f5832ffc05bf14cc6f6943ca7f3**, 공개 릴리스는 **425289603 / windows-replay-menu-20261027**이다. [패키지 정보](../replay-menu-20261027/package.json)와 [공개 재다운로드 검증](../replay-menu-20261027/public-download.json)이 같은 ZIP을 가리킨다. 원문은 [10월 19일 최초 10항목](../../GameDesign/PLAY_REVIEW_20261019.md)이며, 추가 요구는 로딩·스테이지 전환, 플레이어용 문구, 전체 화면 가독성, 격납고 그래픽이다.

핵심 연출은 상당 부분 구현돼 있지만 **최초 10항목의 완료 기준을 모두 충족한 상태는 아니다.** 현재 실행본의 직접 증거는 재출격 메뉴 수명과 제한된 설정·기지 동선 검사다. 이전 실행본의 넓은 검사 범위는 과거 증거로 유지한다. 전체 화면 가독성과 초심자의 이해를 현재 버전에서 승인한 것으로 해석하지 않는다.

## 상태 판독 기준

- **구현됨**: 현재 제품 소스에 해당 동작이 있다. 실행·시각 품질·이해도 통과와 별개다.
- **현재 실행본 검증됨**: 정확한 372a75f 압축 해제 실행본에서 확인한 항목만 해당한다. [Native 기록](../replay-menu-20261027/Native/notes.txt), [통과 결과](../replay-menu-20261027/Native/runtime.txt), [검사 방법](../replay-menu-20261027/REVIEW.md)을 따른다. 명시적 상태 변경과 버튼 콜백을 쓰는 제어 검사다.
- **과거 증거뿐**: 이전 제품의 실행·렌더·OS 관찰 또는 편집기 증거가 있다. 소스 유지나 최근 빌드 성공만으로 현재 실행본 통과로 승격하지 않는다.
- **미완료**: 원문의 제안 세부가 구현과 다르거나, 사용자 인지·현재 버전 검증이 남아 있다. 제안의 미구현을 곧바로 제품 결함으로 판정하지 않는다.

현재 문서 HEAD와 제품 커밋 사이의 `Assets` 변경은 없다. 연결한 소스와 증거의 존재·추적 여부 및 제품 Git blob은 [파일 대조 목록](evidence-manifest.json)에 기록한다. 이번 대조에서는 게임 실행·검사·빌드·소스 수정을 하지 않았다.

## 최초 10항목

| 항목 | 현재 구현 근거 | 실행 증거 구분 | 미완료 또는 판단 한계 |
| --- | --- | --- | --- |
| 1. 운석 등장과 위험 방향 | [조준 테두리의 등장·발견 표시](../../../Assets/_Project/Combat/LockOnReticleView.cs), [유형별 꼬리](../../../Assets/_Project/Visual/MeteorMotionTrail.cs), [위협 우선순위·문구](../../../Assets/_Project/PcInput/ThreatIndicatorView.cs) 구현됨 | 과거: [방향 전환 527a889](../threat-transition-20261027/REVIEW.md), [같은 제품 OS 자연 위협](../threat-transition-20261027/Ordinary/REVIEW.md). 372a75f에서 이 경계 검사를 다시 한 것은 아님 | 처음 보는 사람이 배경 암석과 공격 표적을 구분하고 안내를 따라 고개를 돌리는 완료 기준은 미검증 |
| 2. 조준과 명중의 단계별 반응 | [0.12초 테두리 수렴·잠금 링](../../../Assets/_Project/Combat/LockOnReticleView.cs), [포신 반동](../../../Assets/_Project/Combat/TurretAimingSystem.cs), [일반 운석 손상](../../../Assets/_Project/Meteor/MeteorDamageSurface.cs), 파괴 효과 구현됨 | 과거: [실제 무기 반동 5442fae](../turret-recoil-20261021/verification.md), [운석 손상](../meteor-damage-20261021/verification.md), 527a889 OS 연습 격파 | 포탑은 플레이어 시야에서 숨기는 기존 정책이 있으므로 관전 반동 렌더를 플레이어가 보는 피드백으로 오인하면 안 됨. [점수 팝업](../../../Assets/_Project/Combat/ScorePopupView.cs)은 위로 이동하며 기본 0.8초 표시; 원문의 ‘점수판으로 이동·약 0.4초 정리’와 다름. 조준/발사/생존 타격/파괴의 설명 없는 구별 미검증 |
| 3. 충돌 예고와 방어막 반응 | [0.8초 충돌 예고·방향 파문·차단 색상](../../../Assets/_Project/PcInput/ThreatIndicatorView.Impact.cs), 기존 유리 손상 연결 구현됨 | 과거: [통합 저체력 경고 및 실제 충돌 검사](../integrated-ui-20261027/REPORT.md), [00b1fd6 OS 자연 피해·귀환](../natural-return-20261027/verification.md) | 372a75f의 실제 방어막 차단 순간, 여러 충돌의 방향 대응, 플레이어가 피해 원인을 말할 수 있는지는 미검증 |
| 4. 메뉴 선택과 전환 | 기존 응시 재진입·진입 지연과 [전환 단계 표시/입력 차단](../../../Assets/_Project/PcInput/HangarLobbyController.cs), [설정 처리](../../../Assets/_Project/PcInput/PcTestOverlay.cs) 구현됨 | **현재 실행본 검증됨, 제한 범위**: 제목/기지 설정 열기·닫기, 재출격 준비에서 보조 메뉴 숨김·늦은 콜백 차단, 전투 진입 잔류 없음, 결과→기지 설정 복원. [준비 원본](../replay-menu-20261027/Native/replay-preparing/stability-game-render.png), [기지 복귀 원본](../replay-menu-20261027/Native/base-return/stability-game-render.png). 과거 응시 검사는 [별도 자료](../menu-transition-gaze-20261023/verification.md) | 현재 OS 결과→재출격·설정 조작은 [외부 창으로 차단](../replay-menu-20261027/Ordinary/REVIEW.md). 모든 난이도/뒤로 가기/입력 기기 조합을 현재 통과로 표시하지 않음 |
| 5. 도킹과 수리 드론 | [두 드론·실제 HP와 균열 복원·짧은 무손상 점검](../../../Assets/_Project/PcInput/DockRepairController.cs) 구현됨 | 과거: [fe277d9 최종 실행 및 조기 이탈/반복 수리](../dock-repair-20261021/verification.md). 현재 기지 복귀 그림은 수리 전체 동작의 증거가 아님 | 현재 제품의 손상 귀환→도킹→수리→재출격 연속 관찰 미완료. 메뉴 사용 가능성과 HP/시각 복원의 일치는 과거 제어 검사 범위 |
| 6. 출격 준비의 연속 동작 | [손·스로틀](../../../Assets/_Project/Visual/CockpitHandVisualController.cs), [출격 환경](../../../Assets/_Project/Launch/LaunchEnvironmentView.cs), [외부 기체 출동·패배 귀환](../../../Assets/_Project/PcInput/BaseFlightCinematic.cs) 구현됨 | 과거: [출격 인접 전환 ea926a9](../intro-transition-review-20261021/verification.md), [통합 출격 흐름](../integrated-ui-20261027/REPORT.md), [자연 귀환 표본 00b1fd6](../natural-return-20261027/verification.md). 현재 검사에서는 재출격 후 Playing 도달만 확인 | 전원 좌→우 점등·스로틀 홈 빛·외부 빛의 인과 순서 전체와 반복 출격 준비 단축 선택은 이번 소스/증거 대조로 완료 확인 못함. 현재 귀환 모션 연속성 미검증 |
| 7. 보스 외피 붕괴와 핵 노출 | [체력 비율에 연결된 외피 조각·핵](../../../Assets/_Project/Meteor/BossShellPresentation.cs) 구현됨 | 과거: [014539e 실제 무기 3→2→1→0, 큰 피해 건너뜀, 파괴 1회](../boss-shell-20261021/verification.md). [0bf9fc9 실제 보스 경고/진입 렌더](../loading-boss-20261027/REPORT.md)는 외피 모든 단계 검사와 다름 | 현재 제품의 모든 체력 단계·시야각·강화 공격 동시 가독성 및 ‘무너뜨리는 진전’ 인지 미검증 |
| 8. 강화 장치의 물리적 반응 | [속사 충전·추가 발사·잔여 시간](../../../Assets/_Project/PcInput/PowerUpWeaponFeedback.cs), [실제 능력 상태](../../../Assets/_Project/PowerUps/PowerUpManager.cs)와 기존 회복/차단/EMP 연결 구현됨 | 과거: [속사·다중 실발사 및 만료](../powerup-feedback-20261021/verification.md), [버프 가독성](../buff-readability-20261021/verification.md), [강화 획득·폭발 겹침](../pickup-explosion-readability-20261024/verification.md) | 현재 실행본의 전체 능력 획득→유지→소모/만료 및 최대 3능력+실제 경고 조합은 미검증. 과거 작업의 완료는 속사·다중 중심이며 원문 모든 장치의 직관성 승인과 다름 |
| 9. 지구의 응답과 훈장 수여 | [최종 보스 성공에 연결된 지구 고리·유도등](../../../Assets/_Project/PcInput/VictoryEarthResponse.cs), [0.8초 점수 집계](../../../Assets/_Project/PcInput/WorldTextPresentation.cs), [실제 성과·다음 목표](../../../Assets/_Project/GameFlow/ResultFeedback.cs) 구현됨 | 과거: [지구 응답 6201459](../victory-earth-response-20261021/verification.md), [집계 7ac4b4e](../result-countup-20261022/verification.md), [결과 대비 ae2cd40](../result-contrast-20261027/REVIEW.md). 현재 검사에서 명시적 결과→기지 동작은 확인 | 현재 자연 최종 보스 성공→고리→집계→성과/목표 전체 미검증. 등급 표시가 원문의 별도 ‘훈장 수여’ 연출까지 충족하는지 확인 안 됨 |
| 10. 관제 드론과 배경 사건 | [첫 출격 인사·연습 성공 반응·실패 기체 스캔](../../../Assets/_Project/PcInput/ControlDroneController.cs) 구현됨 | 과거: [04937cd 인사/끄덕임](../control-drone-20261022/verification.md), [e342f55 실패 귀환 스캔](../control-return-scan-20261022/verification.md), 이후 00b1fd6 OS 표본에 착륙/스캔 관찰 | 성공 경로에 기체가 없으므로 기체 스캔 제외가 명시된 범위. 전투 행동 음성/자막·수송선·구조 신호는 해당 구현 범위에 포함되지 않음. 선택적 P3 확장이며 핵심 가독성보다 우선하지 않음 |

## 추가 요구와 전체 화면 가독성

로딩은 [실제 준비 상태 연결](../../../Assets/_Project/PcInput/SfPresentationDirector.Transitions.cs)과 [준비 화면](../../../Assets/_Project/PcInput/FlightLoadingVisual.cs)이 있고, 중앙 스테이지 카드는 같은 전환 코드에서 실제 스테이지 번호를 표시한다. 격납고는 [표면 보강](../../../Assets/_Project/PcInput/HangarSurfaceUpgrade.cs)과 [기체·촬영 장면 미술](../../../Assets/_Project/PcInput/BaseFlightCinematic.Art.cs)이 있다. [격납고 그래픽 작업](../hangar-graphics-20261022/verification.md)은 기존 Titanium 색/노멀 맵, 형상 세부, 전용 카메라와 FXAA를 기록한다. 이번에 새 그래픽을 만든 것은 아니다.

| 화면 또는 요구 | 현재 실행본 범위 | 유지할 과거 근거와 남은 공백 |
| --- | --- | --- |
| 시작·난이도·플레이어 문구 | 제목 설정 콜백 검사만; OS 메뉴는 가려짐 | [준비 가독성](../ready-readability-20261027/REPORT.md), [문구 변경 이력](../../GAMEPLAY_UPDATE_20261019.md). 현재 모든 선택/잠금/긴 문구와 초심자 이해 미검증 |
| 로딩·준비 애니메이션 | 현재 전용 로딩 검사 없음 | [0bf9fc9 5해상도 0/40/100% 렌더](../loading-boss-20261027/REPORT.md). 진행률 고정 레이아웃 검사이며 실제 초기 로딩 시간·실패·재시도·모든 중간 프레임 증거 아님 |
| 소개·브리핑·보정·튜토리얼·연습 | 현재 전용 검사 없음 | [인접 전환](../intro-transition-review-20261021/verification.md), [통합 자동 진행](../integrated-ui-20261027/REPORT.md), 527a889 OS 연습. 실제 눈 추적/HMD 보정 미검증 |
| 출격·귀환·격납고 그래픽 | 준비/기지 고정 그림 및 전투 진입 확인 | [과거 미술 개선](../hangar-graphics-20261022/verification.md), [OS 자연 귀환 표본](../natural-return-20261027/verification.md). 372a75f 연속 동작·다섯 화면 비율의 미술 품질 승인 아님 |
| 전투 HUD·방향·저체력 경고 | 재출격 후 잔류 메뉴 없음만 | [951ce7d 통합 UI](../integrated-ui-20261027/coverage.md), [527a889 방향 전환](../threat-transition-20261027/REVIEW.md). 기존 수치/방향 통과 검사를 현재 제품 검사로 재표기하지 않음 |
| 보스 경고·본문·외피 | 현재 전용 검사 없음 | [5해상도 실제 경고/나레이션/활성 보스](../loading-boss-20261027/REPORT.md). 현재 모든 외피 단계와 다른 효과의 동시 표시 공백 |
| 스테이지 전환·보상 | 현재 전용 검사 없음 | [9→10 도달·버프 동시 표시](../stage-banner-overlap-20261023/verification.md), [통합 단계 근거](../integrated-ui-20261027/coverage.md). 앞 단계를 생략한 fixture이며 현재 자연 10스테이지 완주 증거 아님 |
| 강화 아이템·버프·경고 동시 표시 | 현재 전용 검사 없음 | [가독성 전체 지도](../readability-coverage-20261024/coverage.md)와 항목 8의 능력별 자료. 모든 가능한 동시 배치가 승인된 것은 아님 |
| 일시정지·시점/소리 설정 | 제목/기지 열기·닫기와 전환 차단 검증 | [5해상도 설정·정지](../comfort-ui-20261027/REPORT.md). 현재 OS Esc/포커스 변화, 설정 값 경계·저장 재시작 전체는 미검증 |
| 결과·성과·다시 출격 | 명시적 결과→기지, 기지 재출격, 전투 도달 검증 | [결과 선택 유지](../result-choice-20261027/verification.md), [대비](../result-contrast-20261027/REVIEW.md), 527a889 OS 결과 유지·직접 재출격. 현재 자연 결과 클릭은 외부 창 차단 |
| 기록·랭킹 | 이번 현재 fixture는 기록 보존용 빈 자료 사용 | [공개 기록 검사](../public-records-20261027/verification.md). 현재 실사용 기록으로 가독성을 검사한 것이 아님 |

과거 ‘5해상도’는 보통 800×600, 1024×768, 1280×720, 1920×1080, 2560×1080의 제어 렌더다. 실제 OS 창 크기 변경·DPI·모든 중간 비율·HMD 시야를 포함하지 않는다. [기존 전체 지도](../integrated-ui-20261027/coverage.md)는 당시 제품의 스냅샷이며 이 표의 현재 제품 구분이 우선한다.

## 다음 작업 후보

1. **조준부터 생존 타격과 파괴까지 플레이어가 실제로 보는 반응 확인** — 원래 P1 항목 2. 포탑 반동이 관전 시점에서만 보이는 정책과 점수 팝업의 원문 차이가 구체적인 근거다. 기존 손상·반동 자료를 먼저 재사용하고, 플레이어 시점의 일반/대형 운석에서 단계 구분에 필요한 장면만 대조한다. 점수 이동 추가나 포탑 숨김 해제는 자동 결정하지 않는다. 같은 화면에서 생존 타격과 파괴를 구분하기 어렵다는 근거가 생길 때만 최소 수정한다.
2. **강화 능력의 실제 반응과 경고 우선순위 연결** — 항목 8과 공통 가독성 기준. 기존 속사·다중 증거와 회복/차단/EMP 증거를 능력별로 이어 보고, 비어 있는 획득→유지→만료 및 합법적 동시 조합만 좁혀 확인한다. 특히 보스/저체력 경고와 최대 3개 능력이 겹칠 때 중앙 안내·표적·남은 시간의 식별을 본다. 이미 통과한 전체 해상도/256·600프레임 검사는 반복하지 않는다.
3. **현재 제품의 출격·패배 귀환·수리 연결과 격납고 품질 확인** — 사용자 그래픽 요청 및 항목 5·6·10. 과거 00b1fd6의 자연 귀환 표본을 기준으로 현재 차이를 먼저 추적한다. 외부 창이 정리되고 관찰 가능한 조건에서만 부족한 짧은 동선을 확인한다. 현재 기지 고정 그림이나 과거 통과만으로 연속 동작을 승인하지 않는다. 가림·단절·수리 상태 불일치가 실제 확인될 때 다음 수정 묶음으로 넘긴다.

미구현 제안 세부인 점수판행 점수 조각, 별도 훈장 수여, 반복 준비 단축 선택, 배경 수송선/구조 신호는 **추가 확인 또는 선택이 필요한 범위**다. 이번 대조로 전부 구현하도록 범위를 확대하지 않는다. 원문 각 항목의 ‘처음 보는 플레이어가 구별·설명할 수 있음’은 별도 사용자 평가가 필요하다.

## 독립 차단과 보존

- **외부 오류 창**: 현재 OS 전환/설정 관찰을 차단한다. 같은 검사를 재시도하지 않았고 외부 앱을 수정하지 않았다. 사용자 안내는 검토 채팅이 담당한다.
- **HMD와 실제 눈 추적**: 장비 착용감·시야 가독성·눈 추적 정확도는 PC 렌더나 에이전트 마우스 입력으로 대체할 수 없다.
- **Suno와 청취**: 접근 승인 대기와 신규 음원 미완료를 유지한다. 기존 오디오 이벤트 검사도 실제 청취 승인이 아니다.
- 사용자 기록·설정과 원본 checkout의 index lock은 그대로 유지했다. 게임 코드·새 빌드·릴리스·Drive 변경 없이 문서와 근거 목록만 갱신한다.
