# 프로젝트 전체 구조 한눈에

> 식당 경영 시뮬레이션 게임. 손님이 들어와서 줄 서고 → 카운터에서 주문 → 자리 앉음 → 서버가 음식 가져다줌 → 식사 후 퇴장.
> 배달은 전화기에서 ring → 서버가 받음 → Cook이 조리 → Rider가 픽업 후 배달 사이클.
>
> 1년 = 12달, 1달 = 24시간(영업 8~24시). 연말마다 매출+평판으로 랭킹 산출.

---

## 폴더 구조 한눈에

```
Assets/_project/Scripts/
├── Camera/                  카메라 정렬 셋업 (URP용)
├── Counter/                 손님 주문받는 카운터
├── Customer/                손님 본체/매니저/대기열/데이터
├── Debug/                   테스트 디버그 패널
├── Furniture/
│   ├── BasicFurniture/      좌석, PassWindow, Phone, RiderRoom, PlacementZone
│   └── CookingTool/         조리도구 (인덕션/오븐 등 ToolType별)
├── Grid/                    격자 시스템, 배치, 확장, 시각화
├── Marketing/               마케팅 캠페인 (손님 스폰 부스트)
├── MoneyAndSatisfaction/    돈/만족도/평판/매출 추적
├── Pathfinding/             A* 길찾기 + 이동 컴포넌트
├── Product/                 주문/메뉴/음식 데이터
├── Ranking/                 연말 랭킹 산출
├── Staff/                   직원 (Cook/Server/Rider) 본체/매니저/채용
├── Time/                    시간 시스템 + 영업일 사이클
├── UI/                      HUD
└── Util/
```

---

## 핵심 시스템별 정리

### 1. Grid 시스템 (`Grid/`)

**무엇:** 9×12 격자 위에 가구를 배치하고, 셀의 zone(주방/홀/라이더룸)·활성화·벽·점유 여부를 관리.

| 파일 | 역할 |
|---|---|
| `GridManager.cs` | 셀 배열의 단일 진실. 셀 활성화/walkable 판정/가구 등록/카메라 정렬/zone 셀 조회 등. **싱글톤** |
| `GridCell.cs` | 셀 1개의 데이터 (zone, isActive, isOccupied, isReserved, isWall, placedObject) |
| `PlacementSystem.cs` | 가구 배치/이동/삭제 모드. 터치 입력 처리 |
| `PlacedObject.cs` | 배치된 가구 인스턴스. footprint 회전 처리 |
| `PlacedObjectInit.cs` | 시작 시 자동 배치 (initialPlacements 데이터) |
| `InitialPlacementData.cs` | 시작 가구 데이터 (ScriptableObject) |
| `ExpansionManager.cs` | 확장 단계 진행 (스페이스바로 다음 단계 시도) |
| `ExpansionStageData.cs` | 확장 단계 데이터 SO (origin, w, h, newZone, unlockCost, order) |
| `GridVisualizer.cs` | zone/active 색칠 시각화 |

**핵심 개념:**
- `CellZone` = Kitchen / Hall / RiderRoom
- 역할(`PathRole`)별 walkable 제한: Cook=주방, Customer=홀, Server=모두, Rider=홀+라이더룸
- `GetFurnitureApproachPosition(worldPos, role, from)`: 가구 인접한 walkable 셀 중 `from`에서 가장 가까운 것 반환 → 모든 직원이 가구 접근할 때 사용

---

### 2. 길찾기 (`Pathfinding/`)

| 파일 | 역할 |
|---|---|
| `Pathfinder.cs` | A* 알고리즘. 정적 클래스. `FindPath(start, end, role)` |
| `PathMover.cs` | 캐릭터에 붙는 컴포넌트. `SetDestination(world)` + 매 프레임 `Step(speed)`. 도착 판정 거리 상수 `CELL_ARRIVE=0.05`, `FINAL_ARRIVE=0.1` |

특이점: **목적지 셀은 walkable이 아니어도 통과 허용** (의자, 카운터 위 안착 가능). 인접 셀에서 멈추게 하려면 호출자가 직접 `GetFurnitureApproachPosition` 사용.

---

### 3. 시간 / 영업일 (`Time/`)

| 파일 | 역할 |
|---|---|
| `TimeSystem.cs` | 시:분 흐름. `_hourInterval`(영업 중 1시간 = N초), `_nightHourInterval`(영업 외). `OnHourChanged`, `OnCloseHourReached`, `OnDayStarted`, `OnYearEnded` 이벤트 |
| `DayCycleController.cs` | 영업 시작/종료/정산 진행. 종료 시 손님 강제 퇴장 → 모두 빠지면 `OnSettlementReady` 발동 |

**흐름:**
```
[8시 영업 시작] → 손님 스폰 시작
[24시 영업 종료] → 스폰 중지 + 대기 손님 강제 퇴장
[손님 0명] → 정산 (MoneySystem.SettleMonthly)
[다음 날] → BeginDay → 8시까지 nightInterval로 빨리감기 → OnDayStarted
```

---

### 4. 손님 시스템 (`Customer/`)

| 파일 | 역할 |
|---|---|
| `Customer.cs` | 손님 본체. FSM (WAIT_FOR_SEAT → Enter → WALK_TO_COUNTER → WAIT_AT_COUNTER → WALK_TO_SEAT → WAIT_AT_SEAT → EAT → LEAVE) |
| `CustomerManager.cs` | 스폰/등록/퇴장 추적. 영업 종료 시 `ForceLeaveWaitingCustomers` |
| `CustomerData.cs` | 손님 종류 SO (patience, eatSpeed, moveSpeed, spawnWeight, orderCount 등) |
| `CustomerState.cs` | FSM 상태 enum |
| `QueueManager.cs` | 카운터 앞 대기줄 관리 (TryEnqueue/Dequeue/IsFront/GetSlotPosition) |

**한 손님 사이클:**
1. **WAIT_FOR_SEAT**: 자리 빈 거 있나 폴링. patience 초과하면 떠남
2. **Enter**: 빈자리 점유 → 큐에 들어감
3. **WALK_TO_COUNTER**: 카운터 ServicePos로 이동
4. **WAIT_AT_COUNTER**: 서버 응대 대기. Server가 OnOrderTaken 호출 시 결제 처리 + 좌석으로 출발
5. **WALK_TO_SEAT** → **WAIT_AT_SEAT**: 좌석으로 이동 후 식사 대기
6. **EAT**: `eatSpeed` 시간 동안 만족도 +
7. **LEAVE**: 좌석 해제, 만족도 → SatisfactionSystem + ReputationSystem 보고

**만족도 계산:**
- 기본 50에서 시작
- 대기 시간 patience 초과하면 페널티 (`waitPenaltyRate`)
- Server `EffectiveKindness`만큼 +
- 식사 중 초당 `eatGainRate`만큼 +

---

### 5. 직원 시스템 (`Staff/`)

| 파일 | 역할 |
|---|---|
| `Staff.cs` | 추상 베이스. `_data`, `id`, `_hireVariance`, `_growthMultiplier`, `MoveTo/HasArrived`, `TickMonth` |
| `CookStaff.cs` | 요리사 FSM (IDLE_AT_KITCHEN → WALK_TO_TOOL → USING_TOOL → WALK_TO_PASS_WINDOW) |
| `ServerStaff.cs` | 서버 FSM (IDLE_AT_COUNTER → TAKING_ORDER → WALK_TO_PASS_WINDOW → WALK_TO_SEAT → WALK_TO_PHONE → TAKING_DELIVERY_ORDER) |
| `RiderStaff.cs` | 라이더 FSM (IDLE_AT_RIDERPOS → WALK_TO_PASSWINDOW → WALK_TO_EXIT → DELIVER → RETURN_TO_ENTRY) |
| `StaffManager.cs` | 직원 고용/해고/등급 조회. 월별 정산 tick |
| `StaffData.cs` | 직원 SO (role, grade, salary, hireCost, moveSpeed, kindness, speedMultiplier, deliveryTime 등) |
| `StaffType.cs` | grade enum (Junior/Senior/Manager) |
| `StaffState.cs` | 각 직원 FSM state enum 모음 |
| `StaffCandidatePool.cs` | 채용 후보 풀. 만족도로 RecruitmentTicket 구매 → 몇 달 후 후보 등장 |
| `StaffCandidate.cs` | 후보 1명 (이름, baseData, hireVariance) |
| `Recruitment.cs` | 채용 등급 enum + Config + Ticket |
| `RestSpotPicker.cs` | 휴식지 픽 유틸 (후보 중 점유자 아닌 가장 가까운 곳) |

**공통 휴식 패턴:**
- Cook → Kitchen zone 안 빈 셀
- Rider → RiderRoom zone 안 빈 셀
- Server → 빈 Counter.StaffPos
- 모두 `_currentRestTarget` 필드로 sticky 처리 (한 번 정하면 다른 직원이 차지하기 전엔 안 바꿈)

**직원 stats 보정 공식:**
- `EffectiveMoveSpeed` = moveSpeed × (1 + hireVariance)
- `EffectiveKindness` = kindness × (1 + hireVariance) × growthMultiplier
- `EffectiveSpeedMultiplier` = speedMultiplier × (1 + hireVariance) × growthMultiplier (Cook 조리 속도)
- `EffectiveDeliveryDuration` = deliveryTime / divisor − RiderRoomBonus (Rider)

**채용 흐름:**
1. 만족도로 RecruitmentTicket 구매 (월 1회 제한)
2. `ticketDelayMonths` 경과 후 후보 풀에 N명 등장
3. Hire하면 StaffManager.HireXxx 호출

---

### 6. 가구 시스템 (`Furniture/` + `Counter/`)

#### 6-1. 카운터 (`Counter/`)
| 파일 | 역할 |
|---|---|
| `Counter.cs` | 손님 대기 위치 + 서버 응대 위치. `ServicePos`(손님 서는 위치) / `StaffPos`(서버 위치). 클레임 (`TryClaim`/`ReleaseClaim`) |
| `CounterManager.cs` | 카운터 리스트. 빈 카운터 찾기, 손님 있는데 서버 없는 카운터 찾기 |

#### 6-2. 기본 가구 (`Furniture/BasicFurniture/`)
| 파일 | 역할 |
|---|---|
| `Seat.cs` | 손님 좌석. 점유 플래그 |
| `SeatManager.cs` | 좌석 등록/빈자리 찾기 |
| `PassWindow.cs` | 주방-홀 통과 카운터. 주문 큐 + 완성 음식 리스트 |
| `PassWindowManager.cs` | PassWindow 풀. 통합 조회/픽업 |
| `Phone.cs` | 배달 전화기. ring 상태 + claim 시스템 |
| `PhoneManager.cs` | Phone 인스턴스 관리. ring 타이머 + 콜 생성 + 만족도 해금 (`Unlock`) |
| `RiderRoomManager.cs` | 라이더룸 zone 안 빈 셀 조회 + deliveryBonus 합산 |
| `PlacementZone.cs` | zone 영역 정의 (배치 가능 영역) |
| `FurnitureData.cs` | 가구 SO (width, height, anchor, sprite, deliveryBonus 등) |

#### 6-3. 조리도구 (`Furniture/CookingTool/`)
| 파일 | 역할 |
|---|---|
| `CookingToolInstance.cs` | 조리도구 인스턴스 (특정 ToolType) |
| `CookingToolManager.cs` | 도구 등록/검색. 도구 인접 위치 조회 |
| `CookingToolData.cs` | 도구 SO (toolType, usingDuration) |
| `ToolType.cs` | 도구 종류 enum |

---

### 7. 주문 / 메뉴 / 음식 (`Product/`)

| 파일 | 역할 |
|---|---|
| `Order.cs` | `customer`, `menus[]`, `isDelivery` |
| `Food.cs` | `order` 참조. 조리 완료 후 PassWindow에 놓임 |
| `MenuData.cs` | 메뉴 SO (이름, 가격, 원가, 사용 도구, 조리 시간, 스폰 weight) |
| `MenuManager.cs` | 전체 메뉴 리스트. weight 기반 랜덤 선택 |

**플로우:**
```
Customer.OrderedMenus (스폰 시 결정)
    ↓ Server가 받음
Order(customer, menus, isDelivery=false)
    ↓ PassWindow 큐
Cook이 dequeue → 각 메뉴.tool에서 조리 → Food 생성
    ↓ PassWindow.readyFoods
Server가 픽업 → 손님 자리로 전달
```

배달도 동일하나 customer=null, isDelivery=true.

---

### 8. 돈 / 만족도 / 평판 / 매출 (`MoneyAndSatisfaction/`)

| 파일 | 역할 |
|---|---|
| `MoneySystem.cs` | 잔액 관리. `Earn`/`Spend`/`ForceSpend`. `SettleMonthly`(재료비+급여+유지비 차감) |
| `SatisfactionSystem.cs` | 만족도(int). 손님이 떠날 때 누적. Phone 해금, 마케팅, 채용 티켓 구매에 사용 |
| `ReputationSystem.cs` | 연간 평판(long). 손님 만족도 누적. 랭킹 점수 계산에 사용 |
| `SalesTracker.cs` | 월별 메뉴별 판매량 + 연간 매출. 정산 시 재료비 계산용 |

**자원 흐름:**
- 손님 결제 → MoneySystem.Earn (Counter.OnCustomerPaid)
- 손님 만족 → SatisfactionSystem.Earn + ReputationSystem.Report
- 만족도 → Phone 해금/마케팅 구매/채용 티켓에 차감
- 매달 정산 → 재료비(SalesTracker) + 급여(StaffManager) + 유지비(셀 수 × 단가)

---

### 9. 마케팅 (`Marketing/`)

| 파일 | 역할 |
|---|---|
| `MarketingData.cs` | 캠페인 SO (만족도 비용, 기간 개월, spawnBoost) |
| `MarketingManager.cs` | 만족도 차감 → pending에 추가. 다음 영업일(OnDayStarted)에 active로. spawnBoost 합산해서 `CustomerManager`의 스폰 multiplier 설정. multiplier = `1 + log(1 + sumBoost)` |

---

### 10. 랭킹 (`Ranking/`)

| 파일 | 역할 |
|---|---|
| `RankingSystem.cs` | 연말(OnYearEnded)에 score = revenue/divisor + reputation 계산. dummyTop100과 비교해서 순위 산출. 최소 점수 미달이면 순위권 외 |

---

### 11. 디버그 (`Debug/`)

`TestDebugPanel.cs` — 인스펙터에 의존성 연결하고 UI 버튼에서 다음 메서드 호출:

- **시간**: `ApplyFastTime` / `ApplyNormalTime` / `SkipOneMonth` / `Skip3Months` / `Skip12Months`
- **돈**: `AddMoney` (인스펙터에서 금액 설정)
- **만족도**: `AddSatisfaction1000`
- **마케팅**: `ApplyTestMarketing` / `ForceApplyTestMarketing` / `LogSpawnInterval`
- **손님**: `StartCustomerSpawning` / `StopCustomerSpawning` / `SpawnOneCustomer`
- **직원**: `TickAllStaffMonth` / `LogStaffStatus`
- **라이더**: `ForceUnlockPhone` / `HireRider` (사전점검 로그 포함)
- **가구**: `StartPlaceRandomFurniture` / `StartRemoveMode` / `StartMoveMode` / `ConfirmPlacement` / `CancelPlacement`
- **상태**: `LogGameStatus`

---

### 12. 카메라 / UI / Util

| 파일 | 역할 |
|---|---|
| `Camera/TransparencySortSetup.cs` | URP 카메라에 Custom Y-axis 정렬 적용 |
| `UI/HudView.cs` | 시간/돈/만족도 HUD 표시 |
| `Util/MoveUtil.cs` | 좌표 유틸리티 |

---

## 한 사이클: 전체 흐름 따라가기

### 홀 손님 (앉아서 식사)
```
1. CustomerManager.Spawn() — entryPoint에 생성
2. Customer.WAIT_FOR_SEAT — 빈 좌석 폴링
3. 좌석 발견 → 큐에 등록 → Enter → WALK_TO_COUNTER
4. ServerStaff.IdleAtCounterState ②번 — 손님 있는 카운터 발견
   → TryClaim → TAKING_ORDER → 카운터 도착 + takingOrderDuration 대기
5. Order 생성 (isDelivery=false) → PassWindowManager.SubmitOrder
6. Customer.OnOrderTaken — 결제 처리 (MoneySystem.Earn) → WALK_TO_SEAT
7. CookStaff.IdleAtKitchenState — pending order dequeue
   → 각 메뉴.tool에 접근 → 조리 → PassWindow.PlaceFood
8. ServerStaff.IdleAtCounterState ①번 — readyHallFood 발견 → 픽업
   → WALK_TO_PASS_WINDOW → WALK_TO_SEAT (의자 인접 셀까지만)
9. Customer.OnFoodDelivered → EAT → eatSpeed 후 LEAVE
10. 좌석 해제 + 만족도 합산 + 평판 보고 + 매출 기록
```

### 배달 손님 (전화)
```
1. PhoneManager — 8~20초 ring 타이머 → Phone.StartRinging
2. ServerStaff.IdleAtCounterState ③번 — ringing 발견 + claim 성공
   → WALK_TO_PHONE → TAKING_DELIVERY_ORDER
3. PhoneManager.AcceptCall → Order(customer=null, isDelivery=true) → PassWindow 큐
4. Cook 조리 → PassWindow.PlaceFood (isDelivery 표시)
5. RiderStaff.IdleAtRiderPosState — readyDeliveryFood 발견 → 픽업
   → WALK_TO_PASSWINDOW → WALK_TO_EXIT → DELIVER (5~?초 invisible)
   → entry 위치 텔레포트 → RETURN_TO_ENTRY → 라이더룸 복귀
6. PhoneManager.OnDeliveryCompleted — 카운터 감소
```

---

## "X 바꾸려면 어디?" — 자주 묻는 위치

| 바꾸고 싶은 것 | 어디 |
|---|---|
| 시간 흐름 속도 | `TimeSystem.cs`의 `_hourInterval`, `_nightHourInterval` 인스펙터 |
| 영업 시간 (8~24시) | `TimeSystem.cs`의 `_openHour`, `_closeHour` 인스펙터 |
| 시작 자금 | `MoneySystem.cs`의 `startingMoney` 인스펙터 |
| 셀 유지비 단가 | `MoneySystem.cs`의 `PricePerSquareMeter` |
| 그리드 크기 | `GridManager.cs`의 `_gridWidth`, `_gridHeight` 인스펙터 |
| 초기 활성 영역 | `GridManager.cs`의 `_startGridWidth`, `_startGridHeight` |
| 초기 zone 분포 | `GridManager.cs`의 `CreateGrid()` 안 조건 |
| 손님 스폰 간격 | `CustomerManager.cs`의 `_minSpawnInterval`, `_maxSpawnInterval` |
| 손님 종류 | `CustomerData` SO 만들어서 `CustomerManager.pool`에 등록 |
| 메뉴 추가 | `MenuData` SO 만들어서 `MenuManager`에 등록 |
| 가구 종류 추가 | `FurnitureData` SO 만들어서 PlacementSystem 사용 |
| 도구 종류 | `ToolType.cs` enum + `CookingToolData` SO |
| 직원 등급 데이터 | `StaffData` SO. `StaffManager.cookGrades` 등에 등록 |
| 라이더 상한 | `StaffManager.cs`의 `maxRiderCount` |
| 전화 ring 간격 | `Phone.cs`의 `minCallTimer`, `maxCallTimer` 인스펙터 |
| 전화 ring 타임아웃 | `PhoneManager.cs`의 `ringTimeout` |
| 배달 메뉴 수 | `PhoneManager.cs`의 `minOrderCount`, `maxOrderCount` |
| 폰 해금 비용 (만족도) | `PhoneManager.cs`의 `unlockSatisfactionCost` |
| 배달 최소 시간 | `RiderRoomManager.cs`의 `minDeliveryDuration` |
| 도착 판정 거리 | `PathMover.cs`의 `CELL_ARRIVE`, `FINAL_ARRIVE` 상수 |
| 휴식지 충돌 반경 | 각 Staff cs의 `kRestBlockRadius` 상수 (현재 0.5) |
| 확장 단계 추가 | `ExpansionStageData` SO 만들어서 `ExpansionManager.stages`에 등록 |
| 마케팅 캠페인 추가 | `MarketingData` SO 생성 |
| 랭킹 더미 분포 | `RankingSystem.cs` 인스펙터의 `autoDummyTopScore`, `autoDummyBottomScore`, `autoDummyExpCurve` |
| 채용 비용/딜레이 | `StaffCandidatePool.cs`의 `tierConfigs`, `ticketDelayMonths` |

---

## 시스템 간 의존성 다이어그램

```
[TimeSystem] ─── OnHourChanged ──── HUD
     │
     ├── OnCloseHourReached ── DayCycleController ── 영업종료
     │                                │
     │                                └── customerManager.StopSpawning + ForceLeave
     │
     ├── OnDayStarted ── MarketingManager (campaign 진행)
     │                ── StaffCandidatePool (후보 티켓 진행)
     │                ── CustomerManager (스폰 재개)
     │
     └── OnYearEnded ── RankingSystem (점수 산출)

[Customer] ─── 만족도 ──── SatisfactionSystem ──── 마케팅/채용/폰해금 비용
            ── 평판   ──── ReputationSystem ────── 랭킹 점수
            ── 결제   ──── Counter ── MoneySystem.Earn
            ── 주문   ──── SalesTracker.RecordSale

[Cook]    ── PassWindow에서 Order dequeue → 조리 → Food 배치
[Server]  ── PassWindow에서 readyHallFood 픽업 → 손님 좌석
[Rider]   ── PassWindow에서 readyDeliveryFood 픽업 → 배달 사이클

[PhoneManager] ── ring → Server가 받음 → Order(delivery) → 일반 조리 흐름

[GridManager] ─── 모든 walkable 판정 + 인접 셀 계산
[Pathfinder]  ─── A*로 경로 계산 (PathMover가 사용)
```

---

## 자주 헷갈리는 것들

### "왜 직원이 자꾸 자리를 옮기지?"
→ Rest spot이 매 프레임 재계산되던 버그였음. 지금은 `_currentRestTarget` sticky 처리. 다른 직원이 그 자리(0.5m 이내)에 도달하기 전까진 안 바꿈.

### "왜 서버 둘이 같이 전화 받으러 가?"
→ Phone에 Claim 시스템 추가됨. `TryClaim` 성공한 한 명만 출발. 이미 claim된 폰은 `IsClaimedByOther`로 무시.

### "왜 서버가 의자 위로 걸어가?"
→ Pathfinder가 목적지 셀은 walkable 아니어도 허용함. ServerStaff `WalkToSeatState`에서 `GetFurnitureApproachPosition`으로 의자 인접 셀까지만 가도록 처리됨.

### "왜 모든 직원이 가구 옆 같은 방향으로만 가?"
→ 옛 버전. 지금은 `GetFurnitureApproachPosition`에 `from` 파라미터가 있어서 요청자 위치 기준 가장 가까운 인접 셀 선택.

### "라이더 고용이 왜 실패?"
→ `TestDebugPanel.HireRider`가 사전점검 로그 출력. 보통 원인:
1. StaffManager 인스펙터의 `riderStaffPrefab` 미할당
2. Phone 미설치 또는 미해금 (ForceUnlockPhone)
3. RiderRoom zone 없음 (확장 단계 적용 필요)
4. 라이더 상한 도달

### "확장하면 카메라가 안 움직임"
→ 지금은 `ActivateCells` 끝에서 자동 `CenterCameraOnActiveGrid()` 호출됨. 실제 활성 셀 bounding box 중앙으로 + ortho size 자동 조정.

---

## 추가 자료
- `PROJECT_CONTEXT.md` — 게임 디자인 컨텍스트 (있다면)
- `Assets/_project/Datas/` — 모든 ScriptableObject asset (StaffData, MenuData, CustomerData, FurnitureData, ExpansionStageData 등)
