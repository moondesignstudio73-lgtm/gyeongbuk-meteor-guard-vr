# 시작·튜토리얼·결과 화면 개선 — 2026-10-27

제품 소스: `bb59aa18eb3f9361b1b502efb0f27f32b15e7a43`. 비교 기준 제품은 `a0cf60c`(격납고 개선 제품 `f5234e8` 이후 문서 커밋)입니다. ZIP 및 공개 다운로드의 식별 정보는 `release-evidence.json`에 기록합니다.

## 변경 범위

| 화면 | 이번 변경 |
| --- | --- |
| 시작 | 난이도 2×2 카드, 제목/설명 정렬, 선택·잠금 표시 유지, 시작과 교육 진입을 같은 행으로 배치. 조종석에 가리지 않는 메뉴 깊이와 화면 비율별 간격. |
| 튜토리얼 | 별도 소제목과 본문, 조준 영역 위로 이동, 긴 두 줄 한글의 와이드 화면 잘림 수정. 원래 문구·진행·성공 조건은 유지. |
| 결과 | 고정 제목, 큰 점수 집계, 운석 수/명중률/등급 세 열, 성취/다음 목표, 재출격/기지 메뉴의 구조로 변경. 원래 점수·등급 계산과 0.8초 집계 시계는 유지. |
| 메뉴 입력 | 가까운 버튼 밖을 바라봐도 구형 범위 조준 보정이 메뉴를 획득하던 문제 수정. 메뉴는 직접 광선 교차가 필요하며 일반 목표의 조준 보정은 유지. |
| 교육 | 시작 화면의 교육 진입 버튼 위치만 변경. 교육 전체 화면 개편은 아님. |
| 격납고·출동·전투 HUD | 이전 `f5234e8` 개선을 포함. 이번에는 출동 연속 프레임/전체 전투 2회 검사를 반복하지 않음. |
| 보스·일시정지·시점/소리 설정·연습·기지 난이도·기록 | 화면 상태별 회귀 캡처. 이번 묶음에서 새 구조로 개편하지 않음. |
| 로딩·동시 전투 알림 | 이번 새 구조 변경 및 동시 알림 스트레스 검사의 대상이 아님. 기존 구현과 앞선 검증 범위를 유지. |

전체 UI 개편 완료를 뜻하지 않습니다. Suno 음악은 미변경·미완료입니다.

## 검증과 한계

- `accepted.xml`: 2/2 통과. 800×600, 1024×768, 1280×720, 1920×1080, 2560×1080에서 17개 상태, 총 85장. 로딩 완료와 실제 준비 카드 가시성을 확인한 뒤 시작/선택/잠금을 캡처합니다.
- 시작 버튼은 합성 InputSystem 마우스 누름/뗌으로 Intro 진입을 확인합니다. 이후 튜토리얼·연습·전투·보스·결과 등은 명시적으로 상태를 지정하는 화면 검사입니다. 기지 메뉴 및 재출격도 같은 합성 마우스 입력으로 확인하며 재출격은 준비 상태 진입까지 확인합니다.
- 메뉴 경계 단위 검사는 빗나간 광선에 대한 일반 목표의 조준 보정, 같은 위치의 직접 조준 메뉴가 선택되지 않음, 정확히 바라본 메뉴의 유지 시간을 각각 검사합니다.
- 점수/운석 수 `2,147,483,647`, 명중률 100%는 합성 최대값입니다. 결과 가로 넘침과 튜토리얼 문장 잘림을 검사하고 원본 이미지를 시각 검토했습니다.
- `final.xml`의 결과 집계 및 튜토리얼 시점 검사 통과: 점수 0/138,940/최대값, 성공/실패, 0.05/0.4/0.85초의 90장, 집계 중 6회 버튼 호출, 일시정지/설정/새 세션 경계, 여섯 방향의 튜토리얼 캡처. 이 두 검사는 같은 UI 레이아웃에서 메뉴 직접 조준 표식 추가 전에 수행했습니다. 이후 메뉴 입력은 `accepted.xml` 및 새 ZIP의 별도 프로세스 검사로 확인합니다.
- `final.xml`에 포함된 이전 갤러리의 준비 화면은 의도치 않은 메뉴 선택을 충분히 검사하지 않아 승인 근거에서 제외합니다. 승인 갤러리는 `Accepted`와 `ExtractedFinal`입니다.
- 기준 버전의 `baseline.xml`은 2560×1080 튜토리얼 문장 잘림을 검출하며 실패합니다. 그 시점까지 72장의 비교 이미지를 보존했습니다. 기준 버전의 통과 기록으로 사용하지 않습니다.
- `finalscreens.xml`은 메뉴를 바라보지 않았는데 Intro에 들어가는 문제를 재현한 실패 기록입니다. 직접 조준 표식 추가 후 동일 검사가 통과합니다. 초기 `Before`, `After`, `Candidate*`, `Final`, `FinalScreens`는 중간 시도이며 승인본이 아닙니다.
- 자동 입력은 실제 사람이 Windows 입력으로 플레이한 검사가 아닙니다. 실제 HMD, VR 메뉴 거리/편안함, OS 입력, 청취, 이번 빌드의 자연스러운 전체 전투 진행은 확인하지 않았습니다.

## 같은 상태의 전후 이미지

| 상태 | 수정 전 | 수정 후 |
| --- | --- | --- |
| 800×600 잠금·안내 | [전](Baseline/gallery/800x600/locked/stability-game-render.png) | [후](Accepted/gallery/800x600/locked/stability-game-render.png) |
| 1280×720 시작 | [전](Baseline/gallery/1280x720/start/stability-game-render.png) | [후](Accepted/gallery/1280x720/start/stability-game-render.png) |
| 1280×720 튜토리얼 | [전](Baseline/gallery/1280x720/tutorial/stability-game-render.png) | [후](Accepted/gallery/1280x720/tutorial/stability-game-render.png) |
| 1280×720 결과 최대값 | [전](Baseline/gallery/1280x720/result-max-metrics/stability-game-render.png) | [후](Accepted/gallery/1280x720/result-max-metrics/stability-game-render.png) |
| 2560×1080 시작 | [전](Baseline/gallery/2560x1080/start/stability-game-render.png) | [후](Accepted/gallery/2560x1080/start/stability-game-render.png) |
| 2560×1080 튜토리얼 | [전: 잘림 검출](Baseline/gallery/2560x1080/tutorial/stability-game-render.png) | [후](Accepted/gallery/2560x1080/tutorial/stability-game-render.png) |

동일 종류의 화면 상태 비교이며 배경 별/운석의 무작위 위치까지 고정한 비교는 아닙니다. 원본 크기의 선택 이미지와 검증 기록을 저장소에 포함하고, 전체 로컬 증거 파일의 경로/크기/SHA-256은 `evidence-manifest.csv`에 보존합니다. 엔진 로그, 사용자 설정 백업, 레지스트리 값, 개인 기록 백업은 공개 증거에서 제외합니다.

## 사용자 데이터

원래 수정되어 있던 ProjectSettings 파일은 빌드 전 별도로 보존하고 빌드 후 정확한 바이트로 복원했습니다. SHA-256: `A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89`. 해당 사용자 수정은 제품 커밋에 포함하지 않습니다. 실행 검사 전후 설정 및 순위 파일 보존은 `extracted-run.json`에 기록합니다.

## 배포 실행파일

- 릴리스: [windows-ui-screens-20261027](https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-ui-screens-20261027), ID `424458496`.
- ZIP: `MeteorDefenseVR-Windows-UiScreens-20261027.zip`, 89,598,350 bytes, 222개 파일. 빌드 폴더와 압축 내용 및 추출 내용을 파일별로 대조했습니다.
- SHA-256: `820ef6f5e40d4b73dcb20372fcf9e557225a23817245f34156e42d6fb5c1238a`.
- 새 추출 폴더의 정상 Windows 실행파일, 개발 빌드 아님, `--pc-presentation-qa=screens --no-webcam -batchmode`로 별도 프로세스 실행. GPU: NVIDIA GeForce RTX 3070, Direct3D12. 종료 코드 0, 85장, `passed=True`.
- 실행 구간: 2026-10-26 15:55:59.517Z–15:57:25.870Z. 일반 게임 설정/순위 파일 변화 없음. Unity 세션 값만 달라졌습니다.
- 실제로 원본 이미지를 열어 검토한 새 실행파일 샘플: 800×600 잠금·동시 체험 안내, 800×600 최대 결과 숫자, 2560×1080 긴 튜토리얼, 1280×720 기지. 모든 85장을 사람이 순서대로 플레이하거나 눈으로 검사했다는 뜻은 아닙니다.
- 공개 다운로드 재수신의 시각·해시·크기 일치 여부는 `release-evidence.json`의 `publicDownload`에 기록합니다. 재수신 파일은 바이트 일치 검사이며 추가 플레이 검사가 아닙니다.
