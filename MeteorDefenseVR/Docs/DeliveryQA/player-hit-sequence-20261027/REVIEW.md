# 플레이어 시점 조준·타격 연속 표본

제품 소스 **372a75f438e40f5832ffc05bf14cc6f6943ca7f3**의 조준 → 자동 발사 → 생존 타격 → 파괴를 Unity 편집기 D3D11에서 확인했다. 이 고정 표적 표본에서는 해당 효과의 누락이나 표적을 가리는 현상이 재현되지 않았다. **게임 실행 코드는 수정하지 않았으며 새 실행파일이나 릴리스를 만들지 않았다.** 현재 배포본은 [ReplayMenu](https://github.com/moondesignstudio73-lgtm/gyeongbuk-meteor-guard-vr/releases/tag/windows-replay-menu-20261027)다.

## 검사 조건과 한계

[검사 코드](../../../Assets/_Project/Core/Tests/PlayerHitSequenceReviewTests.cs)는 Editor 전용 테스트 어셈블리에 있다. 실제 장면, 플레이어 카메라 1280×720, 보통 난이도, 기본 공격력 1을 사용했다. 장면의 스포너가 참조하는 웨이브에서 유형별 첫 설정을 읽었다. 일반은 체력 1·크기 1.2, 대형은 체력 2·크기 1.6이며 체력 배율은 1이다. 일반 운석에 생존 타격을 만들려고 체력을 올리지 않았다.

표적은 카메라 앞 12m, 화면 좌표 (0.5, 0.58)에 고정했다. 이동·회전만 정지하고 주입한 시선 레이로 **실제 Raycast, 잠금, 포탑 조준, 자동 발사, 피해 및 효과 이벤트**를 실행했다. 테스트에서 직접 잠금 완료·발사·피해·효과를 호출하지 않는다. 게임 시간은 프레임당 1/60초이며 실제 실행 속도를 측정한 자료가 아니다. 카메라 위치·회전 변화는 0이고 포탑의 플레이어 카메라 제외 정책을 유지했다. 관전 포신 반동을 플레이어 피드백으로 계산하지 않았다.

이것은 공개 ZIP 실행본 검사, OS 입력 플레이, HMD, 음향 청취 또는 초심자 이해도 검사가 아니다. 격납고와 전체 화면 UI 품질 승인으로 확대하지 않는다. 스포너 시나리오의 누적 개수와 이전 표본의 잔여 입자는 자연 웨이브 진행의 증거가 아니다. 대형의 첫 잠금 시점에는 이전 입자가 0으로 정리됐다. 파괴 직후 비활성 풀 객체의 체력이 초기값으로 돌아오는 CSV 행은 부활이 아니다. 생존 판단에는 active와 피해·파괴 이벤트를 함께 사용한다.

## 관찰 결과

[통과 XML](sequence.xml)은 테스트 1/1 통과이고 [요약](Editor/summary.txt)에 이벤트 수, 카메라 고정 및 포탑 제외 검사가 있다. 242개 렌더 프레임을 기록하고 PNG 90개를 저장했다. 아래 시각 판정은 원본 프레임을 직접 확인한 범위다.

| 조건 | 잠금 프레임 / 게임 시각 | 발사·피해 프레임 / 게임 시각 | 결과 |
| --- | --- | --- | --- |
| 일반 | 652 / 7.818840s | 653 / 7.835507s | 체력 1→0, 발사·명중·파괴 각 1회 |
| 대형 첫 타격 | 754 / 9.518840s | 755 / 9.535507s | 체력 2→1, 활성 유지, 손상 비율 0.5 |
| 대형 다음 타격 | 793 / 10.168840s | 794 / 10.185510s | 체력 1→0, 누적 발사·명중 2회, 파괴 1회 |

- 일반: [조준 진행 637](Editor/Normal/frame-000637/stability-game-render.png), [초록 잠금 652](Editor/Normal/frame-000652/stability-game-render.png), [발사 653](Editor/Normal/frame-000653/stability-game-render.png), [파괴 확산 659](Editor/Normal/frame-000659/stability-game-render.png), [폭발 679](Editor/Normal/frame-000679/stability-game-render.png). 발사 후 표적과 조준 링이 사라지고 레이저·점수·폭발이 보인다.
- 대형: [잠금 754](Editor/Large/frame-000754/stability-game-render.png), [첫 명중 755](Editor/Large/frame-000755/stability-game-render.png), [명중 후 761](Editor/Large/frame-000761/stability-game-render.png), [균열 유지·재조준 780](Editor/Large/frame-000780/stability-game-render.png), [마지막 명중 794](Editor/Large/frame-000794/stability-game-render.png), [파괴 확산 800](Editor/Large/frame-000800/stability-game-render.png), [폭발 816](Editor/Large/frame-000816/stability-game-render.png), [후속 화면 852](Editor/Large/frame-000852/stability-game-render.png). 생존 시 외형과 주황 균열이 유지되며 파괴 시 외형이 사라진다.
- 잠금 완료는 각 발사 전 **한 렌더 프레임**이다. 게임 시간으로 약 16.7ms이므로 정지 화면의 존재를 사람이 단계를 확실히 구별한다는 근거로 삼지 않는다. 명중과 발사는 동일 프레임에 발생한다. 자동 발사 지연이나 추가 흔들림·번쩍임은 넣지 않았다.
- 레이저 재생은 각 발사부터 10개 렌더 프레임(일반 653–662, 대형 755–764 및 794–803)에 기록됐다. 상단 안내판은 레이저의 중간 일부를 가리지만 이 위치의 표적·명중점·균열은 가리지 않는다. 임의 화면 위치나 다중 표적의 가독성까지 통과로 판정하지 않는다.

[일반 CSV](Editor/Normal/events-and-frames.csv), [대형 CSV](Editor/Large/events-and-frames.csv)에 매 프레임 체력·활성·잠금·손상·레이저·입자와 실제 이벤트를 기록했다. [일반 이미지 순서](Editor/Normal/images.txt), [대형 이미지 순서](Editor/Large/images.txt)의 프레임 번호로 CSV와 대응한다. 특히 645–663, 747–765, 786–804는 프레임 누락 없는 짧은 연속 구간이다. 그 외 구간은 6프레임 간격 표본이므로 전체를 60fps 영상으로 취급하지 않는다.

## 실패한 검사와 보존

앞선 두 실행은 일반 운석에 손상 표면 컴포넌트가 있다고 가정한 검사 코드의 NullReference로 실패했다. 두 번째 실행은 오류 위치 확인용이었다. 체력 1 일반 운석은 그 컴포넌트가 없는 정상 조건이므로 기록을 0으로 처리하도록 검사만 고쳤다. 실패 폴더와 원시 로그는 공개 통과 증거에서 제외했다.

새 파일은 검사·증거뿐이다. 원본 작업 폴더의 ProjectSettings SHA256은 `A8C1FEDDDF182D07C33D8F5162907A0E99DD81F06D1D1995ABDB82EFE963FE89`로 유지됐으며 별도 작업 폴더의 기존 설정 변경은 커밋하지 않는다. 외부 앱 창, Drive, Suno는 변경하지 않았다. 현재 OS 화면 가림 조건에서 추가 조작도 시도하지 않았다.
