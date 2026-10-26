# 위협 방향·거리 안내 수정 — 2026-10-27

기존 951ce7d 실행본의 상단 방향 안내 가림을 수정합니다. 글자 크기21과 방향·거리 문구는 그대로 유지합니다. 상단3개 정보판, 하단 상태판, 보상/보스 메시지, 생존 안내, 중앙 조준 영역을 실제 화면 투영 좌표로 피합니다. 보상 메시지 영역은 비표시 중에도 예약해 표시/숨김에 따라 안내가 튀지 않게 했습니다. 가려지는 안내는 좌우 전투 영역의 빈 자리를 확보하며, 대상이 사라질 때까지 유지합니다. 위협 순위가 바뀌어도 대상별 위치를 보존합니다. 신규/재사용 대상은 첫 표시부터 방향·거리를 갱신합니다. 원래 위협 선택, 실제 방향/거리 계산, 운석 이동, 무기/피격, 점수와 흐름은 변경하지 않았습니다.

## 전후 비교와 검증 범위

Before: 제품951ce7d + 새 관찰 전용 검사 코드. Editor 명시적 시나리오/뷰포트 이동이며, 기존 공개 ZIP을 그대로 실행한 새 검사는 아닙니다. 5해상도×5상황×24렌더 프레임=600개 관찰에서 조건 위반2411건(한 프레임에 여러 조건 위반 가능)을 기록했습니다. 모든5개 상단 비교 원본을 검토했고 상단 정보판이 방향을 가리는 현상을 확인했습니다. baselineCaptured=True는 검사 자료 수집 완료이며 제품 통과 의미가 아닙니다.

초기 주변부 후보는 기하 검사600프레임을 통과했지만, 와이드에서 안내가 하단 계기판 쪽으로 내려가고800 다중위협이 계기판 위까지 확장되어 제외했습니다. RejectedPerimeter는 로컬에만 보관합니다. 최종 좌우 배치는 Editor600프레임에서 조건 위반0, 모든 단조 이동 구간의 수직 방향반전0입니다. 대상 소멸/풀 재사용/일시정지 위치 유지/재개/초기화도 검사했습니다. 매 프레임 방향·거리 존재, 고정 글자크기, 말줄임 없음, 전체 사각형의 화면 안 유지, UI 및 조준영역/다른위협과 비겹침을 검사합니다. 실제 대상 방향·거리와 우선순위는 별도의 기존 회귀 검사로 확인합니다.

범위: 800x600,1024x768,1280x720,1920x1080,2560x1080. 상단 이동, 좌/우 모서리,4개 동시 위협+보상,4개 위협+실제 BeginClimax 경고. 보상은 직접 HUD 콜백, 대상은 실제 풀 생성 후 위치만 명시적으로 움직였습니다. PNG 파일 저장이 시간에 영향을 주므로 자연 속도의 게임 플레이나 고정 FPS 영상이 아닙니다. 각 구간24개는 연속 렌더콜백 프레임이며 CSV에 frame 번호와 전체 라벨 사각형/문구를 남깁니다. 사진600장을 사람이 전부 본 것으로 주장하지 않습니다. 주요 전후와 모서리/다중위협 원본만 별도로 시각 검토합니다.

## 남은 범위

전체 UI 최종완료, 직접 OS 조작, HMD 착용/편안함, 청취 및 Suno 제작은 미완료입니다. 임의 DPI/창크기, 모든 방향/수/난이도 조합은 보장하지 않습니다. 이미 공개된951ce7d 통합 전투2회는 이번 배치 변경의 신규 검증으로 재사용하지 않으며 다시 실행하지 않습니다. 수정된 정상 비개발 ZIP의 별도 추출 프로세스에서 같은 위협 전용 검사를 수행합니다. 기존 격납고·생존경고 변경은 유지합니다.

제품 소스00b1fd6. Editor 최종 배치검사1/1 및 기존 생존경고/위협우선순위 회귀2/2 통과. CSV를 별도로 검사해 각24프레임 구간의 frame 번호가 연속(+1)임을 확인했습니다. 신규 대상/풀 재사용 첫 문구 갱신도 포함합니다. 넓은 화면의 상단 이동과800 보스+4개위협 최종 원본을 시각 검토했고, 다음 정상 Windows 실행 검증에서5개 크기를 다시 확인합니다.

## 공개 실행본 검증

Source 00b1fd6e94efdd8ef3da04b53a2bf6c116217e61, clean non-development build. ZIP 89613795bytes / 222files, SHA256 19776735694688f462d815c562cde3bb83b4e75ad621189c812533efcae5556f. 별도 추출 native exit0, 10/26/2026 22:18:14–10/26/2026 22:19:26. RTX3070/Direct3D12,600 PNG,600 관찰 프레임 조건위반0, 각24프레임 구간 frame번호연속 확인. 공개 재다운로드 10/26/2026 22:21:53 해시 일치.

[Release](https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-threat-safe-area-20261027). QA archive ThreatSafeArea-QA-20261027.zip, 333523161bytes / 607files / 600images, SHA256 f744a7e0af3d0278aea979f634385f5cb395c7f67240819b206dea97101e4022. 전체 원본은 릴리스 QA ZIP에 있고 native-frame-manifest.json에서 개별 해시를 확인할 수 있습니다. 저장소에는 각 구간00/12 대표 원본과 CSV를 포함합니다.

최종 native 원본 시각 검토:5해상도 모두 top/12;800 left-corner/12,right-corner/12,boss-multiple/12;2560 reward-multiple/00와12,boss-multiple/12. 나머지 연속프레임은 자동 기하/문구 검사 자료이며 전부 육안 검토했다는 의미가 아닙니다. 대상이 반대쪽으로 이동해도 확보한 안내 슬롯은 유지하고 실제 방향은 화살표와 문구로 갱신합니다. 이 배치 방식의 사람/HMD 직관 평가는 남아 있습니다.

사용자 ProjectSettings SHA256 A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89 복원 확인. 실행 wrapper에서 개인 기록/비세션 설정 보존을 확인했습니다. 백업/엔진 로그/인증정보는 공개하지 않습니다.

## 다섯 크기의 상단 안내 원본

| Size | Before (Editor) | Final (native) |
|---|---|---|
| 800x600 | [원본](Before/800x600/top/12/stability-game-render.png) | [원본](FinalExtracted/threat-safe/800x600/top/12/stability-game-render.png) |
| 1024x768 | [원본](Before/1024x768/top/12/stability-game-render.png) | [원본](FinalExtracted/threat-safe/1024x768/top/12/stability-game-render.png) |
| 1280x720 | [원본](Before/1280x720/top/12/stability-game-render.png) | [원본](FinalExtracted/threat-safe/1280x720/top/12/stability-game-render.png) |
| 1920x1080 | [원본](Before/1920x1080/top/12/stability-game-render.png) | [원본](FinalExtracted/threat-safe/1920x1080/top/12/stability-game-render.png) |
| 2560x1080 | [원본](Before/2560x1080/top/12/stability-game-render.png) | [원본](FinalExtracted/threat-safe/2560x1080/top/12/stability-game-render.png) |
