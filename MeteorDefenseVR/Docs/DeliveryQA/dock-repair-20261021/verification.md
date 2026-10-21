# 현재 단계: 도킹 수리 드론 최종 ZIP 검증·GitHub 공개·공개 재다운로드 해시 확인 완료. 다음 행동: 검토 채팅에서 배포 기록 반영

## 배포
- 공개: https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-dock-repair-20261021
- Release ID: 419153939. prerelease=true, draft=false.
- 실행본 커밋: fe277d95d462a84843d26206e0928ae8f5a77415 (브랜치 codex/cockpit-visual-restore).
- Unity6000.5.10f1, release build, worktreeClean=true, 빌드 시각2026-10-21T12:16:25+09:00.
- ZIP: MeteorDefenseVR-Windows-DockRepair-20261021.zip, 89,438,939bytes.
- SHA256: e729faee9c7d302a8b4f047f4f8d42239c38304f5e38f9e9a16dcb7f1777a5e8.
- 로컬 ZIP: ../../../Builds/Sharing/DockRepair20261021/MeteorDefenseVR-Windows-DockRepair-20261021.zip
- 압축 안222개 파일 및 압축 해제222개 파일의 원본 해시 일치. 공개 업로드 asset digest/size 및 공개 링크 재다운로드 SHA256 일치.

## 구현 범위
PLAY_REVIEW_20261019.md 항목5 도킹 수리 드론만 진행했다. 두 드론 접근→실제 손상 부위 스캔→실제 HP 회복과 기존 균열 알파 복원→청록색 준비 표시. 손상 시 약3초, 무손상은 약0.6초 점검이다.
수리 중 기존 메뉴를 사용할 수 있다. 조기 출격/다른 메뉴 이동은 수리를 마무리하고 드론·스캔·손상 경고를 정리한다. 메뉴 Back은 수리를 다시 시작하지 않는다. 외부 Reset과 상태 변경도 정리한다.
기존 패배 귀환5.2초→결과 공개→기존 결과 타이머→메뉴 순서와 LastResult 점수는 유지한다. 도킹은 기존 단계 수리 이벤트를 반복 발행하지 않고, 실제 회복률에 연결한 균열 알파를 사용하며 최종 ResetHealth에서 일괄 정리한다.
카메라/XR 포즈를 강제로 움직이지 않는다. 새 음원·광원·파티클을 추가하지 않았다. Suno 승인 대기를 유지했다.

## 최종 검증
- 관련 개별 자동 검사26개는 최신 결과 기준 모두 통과. 최종 dock-original-pixels-20261021.xml의 도킹/메뉴3개도 통과했다. 출동/실패 착륙 검사는 dock-flight-20261021.xml 1/1 통과했다.
- 정확한 최종 ZIP을 FinalVerified에 압축 해제하여 새 플레이어 프로세스로 실행했다. QA/pc-soak.json: passed=true, issues=[], 2026-10-21T04:12:04Z–04:15:38Z.
- 자동 전투1회: 점수4980, 파괴21, 오류0/경고0, 평균89.94FPS. 자동 PC 환경 측정이며 VR 성능 보증은 아니다.
- 1280×720/800×600의 접근·스캔·준비·조기 이탈 직전/직후 PNG를 생성했다. 문자별 투영 영역의 실제 밝은 청록 픽셀 검사113건 모두 통과했다. ForceMeshUpdate나 추가 프레임 대기로 캡처를 바꾸지 않았다.
- 원본 PNG 및 원본 픽셀 영역을 최근접 확대하여 메뉴/수리 문구를 확인했다. 확대는 진단 보기이며 배포 이미지는 수정하지 않은 원본 PNG다. FinalPixelViews에 진단 보기를 보존했다.
- 수리 안내 최소 글자 높이: 23.677px(1280×720), 19.731px(800×600). 텍스트 잘림/누락 글리프 검사 통과.
- 균열 알파0.820→0.560/0.579로 실제 HP에 따라 옅어지고, 완료 시 HP100/균열0/드론0/스캔false/경고와 누출 정리를 확인했다.
- 메뉴 사용, 조기 Replay 후 다음 전투 정리, 난이도 메뉴 이탈 정리, Back 후 정비 재시작 방지, 무손상 짧은 점검 통과.
- 실패 결과 pending/점수 보존/표시 후4.7초 유지/실패 기체 수리/Reset 정리 및 카메라 포즈 유지 통과.
- 4회 반복 귀환: 도킹pool27/공유재질4, 전체objects2041/materials145로 일정했다.
- 1000회 zero-delta scan Tick: GC0bytes, 합계1.46/1.42ms. 실제 HP/UI 이벤트 포함200회 작은 Tick: GC0bytes, 합계2.23/2.02ms. 전체 GPU 부하 측정은 아니다.

## 글자 누락 보고 정정
앞선 “게임 글자 누락 확정” 및 “깊이 가림이 원인” 보고는 철회한다. 축소/전달된 미리보기에서는 글자 일부가 빠진 것처럼 보였지만 기존 후보 a075f4b의 원본 PNG에도 해당 글자 픽셀이 있었다. 예를 들어 접근 화면의 ‘눈’ 영역은 실제 밝은 청록 픽셀59개이며, 원본 영역 확대에서 전체 글자를 확인했다.
재질 검사에서 Mobile SDF가 material의 _ZTestMode 대신 unity_GUIZTestMode를 사용하는 점을 발견했으나 이것만으로 게임 결함의 원인이라고 볼 수 없다. 임시 DockTextOverlay 셰이더와 재질 변경, 강제 갱신/대기 실험은 전부 철회했다. 최종 추가 변경은 QA의 원본 PNG 문자 픽셀 검사이며 실제 게임 표시 결함을 고쳤다고 주장하지 않는다.
원인 조사 이력은 investigation-history.md와 DepthDiagnosis에 남겼다. 최신 결론은 이 문서가 우선한다.

## 보존 및 한계
- 사용자 ProjectSettings.asset의 Standalone analytics define 제거 변경을 커밋에 포함하지 않았다. 빌드 전에 경로 한정 stash와 백업을 사용했고, 완료 후 복원하여 줄바꿈 정규화 비교가 일치함을 확인했다.
- 직접 사람 전체 플레이, HMD 착용, 초심자 인지, 실제 눈추적, 청감 검사는 수행하지 않았다. 자동 입력/명시적 피해·상태 fixture와 실제 플레이어 카메라 PNG 검증이다.
- Suno 음원 승인 대기 유지. Drive/연속개선 상태파일은 수정하지 않았다. 다른 채팅으로 메시지를 전송하지 않았다.