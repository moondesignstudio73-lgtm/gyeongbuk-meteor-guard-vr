# Turret Player View Clearance QA

## 변경 결과

- Top Turret: 좌측 외곽/상단/후방 `(-3.2, 1.95, -2.15)`로 이동
- Bottom Turret: 우측 외곽/하단/후방 `(3.2, -1.85, -2.15)`로 이동
- Muzzle 거리: `0.74m`에서 `0.48m`로 단축
- 포신과 Rail 길이를 약 35~40% 축소
- `TurretPlayerHidden` Layer는 Player/HMD Camera에서 제외하고 Spectator Camera에서는 포함
- `TurretPlayerVisibilityGuard`가 런타임과 렌더 직전에 Camera Culling Mask와 Renderer Layer를 복구
- Muzzle Light와 Laser Beam은 숨김 Layer 밖에 유지하여 플레이어 공격 피드백 보존
- 기존 Cockpit Exclusion Radius, 상/하 포탑 자동 선택, 최소 회전 우선순위 유지

## 검수

- 포탑 전용 자동 테스트: 7/7 통과
- 6방향 Windows VR Simulation 렌더: Front / Left / Right / Rear / Above / Below 통과
- 전체 회귀 테스트: 359개 중 357 통과, 실패 0, 기존 조건부 제외 2
- Spectator: 8/8 통과
- Windows VR Simulation: 18/18 통과
- Tutorial Safe Zone: 2/2 통과
- Windows Player 빌드 및 15초 시작 스모크 테스트 통과

6방향 결과 이미지는 `After/<Direction>/stability-game-render.png`에 있습니다.
