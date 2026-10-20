# 완료: 상태판 가독성 windows-telemetry-readable-20261021 공개·다운로드 검증 완료
맥락 압축 후 이 문서를 먼저 읽는다. 이전 결과 수치/위협 우선순위 묶음은 공개 완료했으므로 반복하지 않는다. 이번은 새 연출보다 먼저 재현된 상태판 UI 가독성 결함만 수정·검증·배포한다.

## 완료 조건
- 기존 5해상도(1920×1080,1280×720,1024×768,800×600,2560×1080), 정상/저체력/다중 위협/앞뒤 전환의 전후 실제 렌더와 최소 글리프 높이 측정.
- 필수 방어막/위협/방향/거리 정보를 생략·축소·말줄임 없이 즉시 읽게 한다.
- 상태판과 경고/조준 영역/실제 운석 및 조준 링이 겹치지 않는지 확인한다. 확대한 패널이 타깃을 가리지 않아야 한다.
- 실제 모델 표면 텍스처/레이더 장식 전체를 무제한 수정하지 않는다. 새 커밋/빌드/ZIP 추출검증/새 릴리스/공개해시까지 완료한다.

## 실제 사용 경로 조사
CockpitTelemetryPresentation.Refresh/RefreshSecondaryDisplays가 실제 UI의 LiveShipStatus/LiveThreatAnalysis/LiveWeaponStatus/LiveSystemsStatus를 갱신한다. CommercialVisualPolishSetup의1920×1080 기반 글자17px와 CanvasGroup .44~.46 밝기 때문에800×600에서 매우 작고 흐리다.
- SHIELD는 실제 PlayerHealth 값: 필수, 큰 %로 유지.
- THREAT/CONTACTS/VECTOR/RANGE는 실제 운석 위험·수·방향·거리: 필수, 단일 최우선 위협과 일치하는 큰 요약으로 유지.
- HULL은 별도 선체 체력이 아닌 min(현재HP, 시각 손상 단계) 파생값. ENGINE/POWER도 별도 엔진/전원 게임 상태가 아닌 손상 단계의 장식 설명. 중복 파생 백분율 대신 기체 상태를 명확히 표시한다.
- SCORE는 상단 점수 HUD, GAZE/TARGET/포탑 준비는 기존 실제 조준 링/조준 중·포탑 조준 중 표시에서 즉시 확인 가능. RADAR CONTACTS는 위협 수와 중복. 이 중복/장식 정보를 상시 상태판에서 정리하며 실제 값 자체를 변경하지 않는다.

## 설계/다음 실행
1. 기존 상태판을 재사용하여 방어막%/기체 상태, 위협 수·위험/방향·거리의 핵심 두 줄씩을 큰 고정 크기로 표시. 자동 축소/잘림 금지. 새 정보창을 무리하게 추가하지 않는다.
2. 실제 운석과 링 영역이 겹치면 좌/우 빈 높이로 위치 이동. 중앙 조준 영역 및 상단 주요 HUD를 비운다. ThreatIndicatorView는 실제 상태판 사각형 회피를 이미 사용하므로 새 위치와 연동.
3. 결정적 실제 스포너/상태 fixture로 Before 확보→After와 글리프/영역 검증→필요 회귀→추출 실행본→배포.
4. 구현·검증·공개 완료. 아래 최종 배포 결과가 현재 상태이며, 중간 실패 기록은 재현/보정 이력이다.

## 보존/미완료
사용자 ProjectSettings(Standalone analytics define 제외)를 백업 및 경로 한정 stash로 보존하고 빌드 후 복구한다. 기존 사용자 변경 커밋 금지. HMD/눈 추적 실기, Suno 승인 대기/신규 음원, 직접 창 전체 조작, 초심자 사용성은 미완료 유지. 다른 채팅 Drive/상태 파일 변경·메시지 전송 금지. 이번 상태판 묶음 이후 최초10개 다음P1로 돌아간다.


## 첫 After 결과 및 보정
- 기존 5해상도 25상태 모두 글리프/자동축소/잘림/표적·경고·중앙 영역 검사는 통과. 800×600 최소 숫자23.34px, 최소 문자17.76px. 기존800×600 숫자는 약5.2px.
- 방향 예상값만 실패: 검사용 viewport(.7,.5)는 초광폭을 제외하면 실제 방향 분류상 앞쪽이었다. 해상도와 무관한 명확한 오른쪽 local(25,2,35)로 검사 대상 위치를 고정한다. 실제 값은 수정하지 않는다.
- 실제800×600 화면에서 오른쪽 상태판 하단이 조종석 손 모델에 가리는 결함을 발견했다. 기존 Canvas의 planeDistance를1.25→.25로 옮겨 기체 모델보다 앞에 표시한다. 새 계층 생성/모델 텍스처 변경 없음.
- 다음: 보정된 After 재검사 및 직접 렌더 확인, 관련 ThreatRadar/TargetRelease 회귀.


## 편집기 통과 / 코드 확정 준비
- telemetry-readable-verified-20261021.xml 18/18 통과: 상태판25상태 + ThreatRadar14 + TargetRelease3.
- After800×600 직접 렌더에서 조종석 손에 가리던 텍스트가 완전히 표시됨을 확인. 숫자 최소23.34px, 문자 최소17.76px, 글리프 누락/잘림/자동축소/운석·경고·중앙 영역 겹침 없음.
- 추출본 QA에는 오른쪽 패널에 운석을 직접 옮기는 추가 상태도 포함한다(5해상도×6상태=30). 양쪽 패널이 실제로 이동했는지도 검증한다. 이 추가 QA 상태는 편집기25상태와 구분한다.
- 사용자 ProjectSettings 원본을 현재 QA 폴더에 백업했다. 다음: 코드 커밋→해당 설정만 stash→빌드→설정 복원/대조→포장/추출실행.


## 빌드 진행
- 코드 커밋31e65cf, 빌드 로그 Logs/telemetry-readable-build.log. 현재 Unity 빌드 중 Assets 수정 금지.
- 사용 중인 Git 프로세스가 없고0바이트 잠금이231초 동안 변경되지 않았으며 독점 접근 가능함을 확인한 후 해당 index.lock만 정리했다.
- 사용자 설정은 user-settings-stash.txt의 SHA와 ProjectSettings.user-backup.asset으로 보존 중이다. 빌드 후 apply하고 정규화한 내용 동일함을 확인해야 한다.
- 포장 스크립트 Builds/Sharing/TelemetryReadable20261021/package.ps1 및 publish.ps1 준비됨. 새 이름 MeteorDefenseVR-Windows-TelemetryReadable-20261021.zip, 태그 windows-telemetry-readable-20261021. 공개 배포는 아직 미완료.


## 빌드/포장 완료
- 빌드31e65cf6f56ba415e1d5c1ce17e1d1f4b57afcd1, clean=true, Build exit0.
- 사용자 ProjectSettings를 보존 stash에서 복원하고 원본 백업과 정규화 내용 동일함을 확인. 사용자 변경은 커밋하지 않았다.
- ZIP 89,421,004bytes,222파일 해시/크기 일치. SHA256 996d49335c375d3f124273fe9ffa9507aea89f60882a4adf5f66211b91373d24.
- 실행 스크립트 Builds/Sharing/TelemetryReadable20261021/run-extracted.ps1. --pc-soak --pc-soak-rounds=1 --pc-delivery-qa --pc-telemetry-qa --no-webcam -batchmode. 실행 정보 extracted-run.json, 최종 QA는 FinalExtract/.../QA/pc-soak.json 및 TelemetryReadable/measurements.txt.
- 새 릴리스 아직 미공개. 이 단계에서는 이전 릴리스를 새 완료 결과로 소개하지 않는다.


## 첫 추출본에서 발견한 결함 — 공개 보류
- 실제 자동 전투 중 LiveThreatAnalysis에서 U+C67C(왼) 글리프가 폰트에 없어 대체 기호로 나타난다는 경고를 발견했다. 편집기25상태는 앞/뒤/오른쪽만 포함해 이 문자를 검사하지 못했다.
- 이 첫 ZIP/실행본은 공개하지 않는다. 종료 후 FirstAttempt로 보존하고, 왼쪽 방향 및 중간체력(손상) 상태를 추가해35상태로 확대한다.
- PcTestSetup.BakePresentationFont로 소스에서 사용하는 한글을 정적 폰트에 포함→필요 폰트 변경 커밋→사용자 설정 보존 후 재빌드→새 FinalExtract 실행.
- 첫 추출본의 추가 오른쪽 교차 상태에서5해상도 모두 겹침 실패. 기존4개 선호 높이 사이의 빈 공간까지 찾도록 세로 위치 검색을 보완했다. 폰트 크기는 줄이지 않는다.
- 첫 QA의 최상위 passed가true였으나 issues에 상태판 실패가 있었다. 이 신규 fixture 실패를 최종 passed에 반영하도록 누락된 조건을 추가하고, 배포 스크립트도 issues가0개여야 진행하도록 강화했다. 첫 결과는 통과로 판정하지 않는다.
- 첫 추출본은 FirstAttemptExtract, 첫 ZIP은 Sharing/TelemetryReadable20261021/FirstAttempt에 보존. 현재 폰트 베이크 중. 다음: 편집기35상태 재검사 후 새 빌드.

- 폰트 베이크 종료0, PcTestKorean.asset에 필요한 글리프가 추가됐다. 현재 telemetry-readable-complete-20261021.xml로35상태를 검사 중이다.
- 폰트 베이크 과정에서 Unity가 ProjectSettings를 다시 기록할 수 있으므로 다음 코드 확정 전에 ProjectSettings.user-backup.asset과 반드시 대조하고 사용자 버전으로 복원한다. Git status만으로 보존 여부를 판단하지 않는다.


## 밀집 표적 회피 보완
- 폰트 보완 후 왼쪽/손상 글리프 및 실제값 검사는 통과했다.
- 상세 bounds에서 오른쪽 운석들이 y .06~.29, .43~.66, .61~.76을 차지하여 높이.22 패널을 측면에만 둘 빈 공간이 없음을 확인했다. 세로 세분 검색만으로는 해결할 수 없다.
- 양옆 빈 공간을 우선 유지하되 측면이 막히면 하단 중앙(필요하면 상단 중앙) 빈 영역을 사용한다. 다른 패널 및 중앙 조준 영역과의 교차도 점수에 포함한다. 글자/정보 크기를 줄이지 않는다.


## 35상태 통과
- telemetry-readable-final-20261021.xml 1/1 통과, After/measurements.txt35상태×2패널 전 항목true. 최소 글리프17.76px/숫자23.34px(800×600), 새 왼/손상 글리프도 존재 확인.
- 양쪽 패널의 직접 표적 교차 시 이동 및 운석·경고·조준 영역 비겹침 통과. 밀집 측면 상황에는 중앙 하단 빈 공간을 사용할 수 있다. 다른 패널과도 겹치지 않게 검사한다.
- 기존 ClassicCockpitIntegrationTests의 런타임 예상값을 새 한글 요약/주 HUD 점수/중복 계기 숨김에 맞췄다. 기존 모델·계층 검사 포함6개 실행 중(telemetry-readable-cockpit-20261021.xml).
- 기존 조종석6개 중4개 통과,2개는 기존 테스트 전제 때문에 보정했다. 숨긴 옛 WEAPON 블록의2px 하단 여백 판정은 실제 런타임35상태 검증으로 대체하고 연결 참조 보존만 확인한다. 점수 검사는 메뉴에서 비활성 전투 HUD를 찾던 상태였으므로 실제 전투 시작 후 값을 설정한다. 제품 동작 우회나 필수 글자 축소가 아니다.
- 현재 telemetry-readable-cockpit-final-20261021.xml로 재검사 중. 아직 최종 코드/ZIP 배포 미완료.


## 최종 코드/재빌드
- 조종석6/6 통과(telemetry-readable-cockpit-final-20261021.xml). 상태판35상태 통과 및 위협 계산14/조준 해제3 통과 기록과 함께 확인했다.
- 최종 코드 d51f3063b277eea97a900c356eb99d4a4e06967b, 보완된 정적 폰트 포함. 사용자 설정 차이는 analytics define 제외1개뿐임을 확인하고 백업과 대조했다.
- 사용자 설정을 다시 경로 한정 stash로 보존했으며 SHA는 user-settings-stash.txt에 갱신했다. 현재 빌드 로그 Logs/telemetry-readable-final-build.log. 출력 예상 Builds/Windows/RC_0.2.0-rc.1_d51f306.


## 최종 ZIP
- 코드 d51f3063b277eea97a900c356eb99d4a4e06967b, clean=true, 빌드 종료0.
- 새 ZIP 89,422,772bytes,222파일 일치. SHA256 2a478ed9beeb086886b6bf972cb5bf0450794cf5d6f03b2b8213e8f85bcb6d4b.
- 사용자 설정을 stash에서 다시 적용하여 최초 백업과 내용 동일함을 확인했다.
- FinalExtract는 새 ZIP에서 다시 생성한다. FirstAttemptExtract 및 Sharing/.../FirstAttempt는 실패한 첫 시도 보존이며 배포 대상이 아니다.
- 현재 run-extracted.ps1 실행. 실행 추적은 extracted-run.json, 결과 QA/pc-soak.json 및 QA/TelemetryReadable/measurements.txt. 둘 다 통과하고 issues=[]여야 공개할 수 있다.


## 최종 추출본 검증 완료
- 실행 종료0, QA/pc-soak.json passed=true, issues=[]. QA/TelemetryReadable/measurements.txt35상태×2패널 모두true.
- 실패 귀환 fixture: 결과 보류/문닫힘 전 점수 보존/138940 공개/4.7초 표시/격납고 메뉴 준비 모두true.
-53개 실제 게임 렌더 이미지 표본 검사에서 검은 화면0개.1920×1080 밀집 오른쪽 교차,800×600 왼쪽·손상,1024×768 교차,1280×720 정상,2560×1080 다중 위협을 직접 확인했다. 글리프 누락/조종석 모델 가림/패널의 공격 대상 가림 없음(검증한 상황 범위).
- 공개할 비교 이미지: Telemetry-Before.png(기존800×600 다중위협), Telemetry-After.png(최종 추출본 동일 상태), Telemetry-Targets-800x600.png.
- 다음: 준비된 publish.ps1 실행, 공개 ZIP 재다운로드 해시 검증 후 README와 이 문서를 최종 링크로 갱신한다. HMD·실제 눈 추적·직접 창 전체 조작·초심자 사용성·Suno 신규 음원은 아직 미완료다.


## 최종 공개 결과 — 현재 상태
- 공개 링크: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-telemetry-readable-20261021
- release ID418950543, draft=false, target d51f3063b277eea97a900c356eb99d4a4e06967b.
- 파일 MeteorDefenseVR-Windows-TelemetryReadable-20261021.zip,89,422,772bytes. SHA256 **2a478ed9beeb086886b6bf972cb5bf0450794cf5d6f03b2b8213e8f85bcb6d4b**.
- ZIP/SHA256SUMS와 전후 비교·표적 회피PNG3개 공개. 업로드 자산 digest/크기 일치 및 공개 ZIP 재다운로드 해시 일치 확인(public-download-verified.json).
- 최종 추출본 자동 마우스 전투1회: 점수4980/파괴21/오류0/경고0, 결과90.83초/초기화102.07초. 실제 사람의 플레이로 주장하지 않는다.
- 5해상도×7상태(정상,저체력,다중위협,뒤→앞,왼쪽 패널 교차,오른쪽 패널 교차,왼쪽 위협·중간체력) ×2패널: 실제값/방향/거리/글리프/최소 높이/잘림/자동축소/표적·경고·중앙 조준 영역 비겹침 통과. 편집기 및 최종 추출본 둘 다 확인.
-800×600 최소 숫자5.24→23.34px, 최소 문자3.99→17.76px. 전체53개 실제 렌더 표본에 검은 화면0, 주요5해상도 직접 이미지 확인 완료.
- 관련 회귀: ThreatRadar14,TargetRelease3,ClassicCockpit6 통과. 기존 숨김 영문 블록의 배치 검사는 실제 큰 한글35상태 검사로 대체했다.
- 사용자 ProjectSettings의 analytics define 제외 변경을 원본 백업과 동일하게 복원했다. 해당 변경을 커밋하지 않았다. 다른 채팅 Drive 문서/개선 상태 파일은 수정하지 않았다.

## 다음 재개 위치 / 남은 검증
이번 좌우 상태판 UI 묶음은 완료했다. 최초10개 개선안으로 돌아가 다음 P1인 **2번 조준과 명중의 단계별 반응**의 실제 미완료 항목부터 확인한다. 이미 구현된0.12초 수렴/진행 링/명중 효과를 다시 만들거나 과거 출동·귀환 소개로 되돌아가지 않는다. 새 범위를 이번 릴리스에 추가하지 않았다.
HMD·실제 눈 추적·직접 창 전체 조작·초심자 사용성은 별도 검증이 남아 있다. Suno 승인 대기/신규 음원 미완료 상태도 유지한다. 이번 작업은 실제 조종석 모델 표면 텍스처를 변경하지 않았다.
