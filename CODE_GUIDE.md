# 식당 경영 시뮬레이션 — 코드 가이드

> 식당 경영 시뮬 게임. **홀 손님 / 배달(전화) / 드라이브 쓰루(DT)** 3가지 매출 채널.
>
> 1년 = 12달, 1달 = 24시간(영업 8시~24시). 연말마다 매출+평판으로 랭킹.
>
> 이 문서는 모든 코드의 역할을 한 곳에 모은 레퍼런스. 시스템적으로 중요한 부분은 자세히 설명.

---

## 목차

1. 폴더 구조
2. Grid 시스템
3. 길찾기 (Pathfinding)
4. 시간 / 영업일 (Time)
5. 손님 시스템 (Customer)
6. 드라이브 쓰루 시스템 (DT) — **NEW**
7. 직원 시스템 (Staff)
8. 가구 시스템 (Furniture / Counter)
9. 주문 / 메뉴 / 음식 (Product)
10. 돈 / 만족도 / 평판 / 매출
11. 마케팅
12. 랭킹
13. 디버그 / UI / 기타
14. 전체 흐름 따라가기
15. "X 바꾸려면 어디?"
16. 시스템 간 의존성
17. 자주 헷갈리는 것들

---

## 1. 폴더 구조

```
Assets/_project/Scripts/
├── Camera/                  카메라 정렬 셋업 (URP용)
├── Counter/                 손님 주문받는 카운터
├── Customer/                손님 / DT 차량 / 매니저 / 대기열 / 데이터
├── Debug/                   테스트 디버그 패널
├── Furniture/
│   ├── BasicFurniture/      좌석, PassWindow, Phone, RiderRoom, DT창구, PlacementZone
│   └── CookingTool/         조리도구 (인덕션/오븐 등)
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

## 2. Grid 시스템 (`Grid/`)

**무엇:** N×M 격자 위에 가구 배치, 셀의 zone(주방/홀/라이더룸)·활성화·벽·점유 여부 관리.

| 파일 | 역할 |
|---|---|
| `GridManager.cs` | **싱글톤.** 셀 배열의 단일 진실. 활성화/walkable 판정/가구 등록/카메라 정렬/zone 셀 조회 |
| `GridCell.cs` | 셀 1개의 데이터 (zone, isActive, isOccupied, isReserved, isWall, placedObject) |
| `PlacementSystem.cs` | 가구 배치/이동/삭제 모드. 터치 입력 처리 |
| `PlacedObject.cs` | 배치된 가구 인스턴스. footprint 회전 처리 |
| `PlacedObjectInit.cs` | 시작 시 자동 배치 (initialPlacements 데이터) |
| `InitialPlacementData.cs` | 시작 가구 데이터 SO |
| `ExpansionManager.cs` | 확장 단계 진행 (셀 해금) |
| `ExpansionStageData.cs` | 확장 단계 SO (origin, w, h, newZone, unlockCost, order) |
| `GridVisualizer.cs` | zone/active 색칠 시각화 |

### 시스템적 핵심
- `CellZone` = **Kitchen / Hall / RiderRoom**
- **`PathRole`별 walkable 규칙:**
  - Cook → 주방
  - Customer → 홀
  - Server → 모두 (홀/주방/라이더룸 다 가능)
  - Rider → 홀 + 라이더룸
- **`GetFurnitureApproachPosition(worldPos, role, from)`** — 가구 인접 walkable 셀 중 `from`에서 가장 가까운 셀 반환. **모든 직원이 가구로 접근할 때 반드시 사용.** 이게 없으면 의자 위로 걸어가거나 같은 셀에 몰린다.
- `CenterCameraOnActiveGrid()` — 활성 셀 bbox + DTLane bbox 기준 카메라 자동 조정.

---

## 3. 길찾기 (`Pathfinding/`)

| 파일 | 역할 |
|---|---|
| `Pathfinder.cs` | A* 알고리즘. 정적 클래스. `FindPath(start, end, role)` |
| `PathMover.cs` | 캐릭터 컴포넌트. `SetDestination(world)` + 매 프레임 `Step(speed)` |

**상수:**
- `CELL_ARRIVE = 0.05` — 중간 셀 도착 판정 거리
- `FINAL_ARRIVE = 0.1` — 최종 도착 판정 거리

**특이점:** 목적지 셀은 walkable이 아니어도 통과 허용 (의자/카운터 위에 멈출 수 있음). 인접 셀에서 멈추게 하려면 호출자가 직접 `GetFurnitureApproachPosition` 사용해야 함.

---

## 4. 시간 / 영업일 (`Time/`)

| 파일 | 역할 |
|---|---|
| `TimeSystem.cs` | 시:분 흐름. `_hourInterval`(영업 중 1시간 = N초), `_nightHourInterval`(영업 외). 이벤트: `OnHourChanged`, `OnCloseHourReached`, `OnDayStarted`, `OnYearEnded` |
| `DayCycleController.cs` | 영업 시작/종료/정산. 종료 시 손님 강제 퇴장 → 모두 빠지면 `OnSettlementReady` 발동 |

**흐름:**
```
[8시 영업 시작] → 손님 스폰 시작
[22시] → DT 신규 스폰 차단 (newSpawnCutoffHour)
[24시 영업 종료] → 모든 스폰 중지 + 대기 손님 강제 퇴장
[손님 0명] → 정산 (MoneySystem.SettleMonthly)
[다음 날] → BeginDay → 8시까지 nightInterval로 빨리감기 → OnDayStarted
```

---

## 5. 손님 시스템 (`Customer/`)

| 파일 | 역할 |
|---|---|
| `Customer.cs` | 손님 본체. FSM (WAIT_FOR_SEAT → Enter → WALK_TO_COUNTER → WAIT_AT_COUNTER → WALK_TO_SEAT → WAIT_AT_SEAT → EAT → LEAVE) |
| `CustomerManager.cs` | 스폰/등록/퇴장 추적. 영업 종료 시 `ForceLeaveWaitingCustomers` |
| `CustomerData.cs` | 손님 종류 SO (patience, eatSpeed, moveSpeed, spawnWeight, orderCount 등) |
| `CustomerState.cs` | FSM 상태 enum |
| `QueueManager.cs` | 카운터 앞 대기줄 (TryEnqueue/Dequeue/IsFront/GetSlotPosition) |

### 한 손님 사이클
1. **WAIT_FOR_SEAT** — 자리 빈 거 폴링. patience 초과 시 떠남
2. **Enter** — 빈자리 점유 → 큐 등록
3. **WALK_TO_COUNTER** — 카운터 ServicePos로 이동
4. **WAIT_AT_COUNTER** — 서버 응대 대기. `OnOrderTaken` 시 결제 처리 + 출발
5. **WALK_TO_SEAT → WAIT_AT_SEAT** — 좌석으로 이동 후 식사 대기
6. **EAT** — `eatSpeed` 동안 만족도 +
7. **LEAVE** — 좌석 해제, 만족도 → SatisfactionSystem + ReputationSystem 보고

### 만족도 계산
- 기본 50에서 시작
- 대기 시간이 patience 초과하면 페널티 (`waitPenaltyRate`)
- Server `EffectiveKindness`만큼 +
- 식사 중 초당 `eatGainRate`만큼 +

---

## 6. 드라이브 쓰루 시스템 (DT) — `Customer/`, `Furniture/BasicFurniture/`

**무엇:** 차량이 차로(레인)를 따라 진입 → 주문창구 정차 → 픽업창구 정차 → 음식 받고 퇴장. 홀과 별도의 매출 채널.

### 6-1. 파일 구성

| 파일 | 역할 |
|---|---|
| `DTSystem.cs` | **싱글톤.** DT 매니저. 해금(만족도 800) + 영업 중 차량 스폰 + 한도 체크 |
| `DTLane.cs` | **싱글톤.** 차로. waypoint 시퀀스 + 활성 차 큐. `IsOrderStop/IsPickupStop/IsExit` 판정 |
| `DTCustomer.cs` | 차량 본체. FSM (DRIVE → WAIT_AT_ORDER → DRIVE → WAIT_AT_PICKUP → DRIVE → Despawn) |
| `DTCustomerData.cs` | 차량 종류 SO (carSprite, moveSpeed, patience, orderCount, baseSatisfaction, spawnWeight) |
| `DTOrderWindow.cs` | 주문창구. 차 1대 슬롯 + 서버 응대 위치 + `TryClaim`/`ReleaseClaim` |
| `DTPickupWindow.cs` | 픽업창구. **차별 음식 슬롯(Dictionary)** + 서버가 음식 놓는 위치 |
| `DTWindowManager.cs` | DT 창구 등록 매니저 |

### 6-2. 시스템적 핵심

**차로(DTLane) waypoint 배열**
- 인스펙터에서 자식 Transform들을 `waypoints[]` 배열에 순서대로 드래그
- `orderStopIndex` / `pickupStopIndex` 로 정거장 지정
- 마지막 waypoint = Exit
- `IsWaypointOccupiedByOther` — 차들이 서로 같은 waypoint 점유 못 함 (충돌 방지)

**차량 진행 로직 (`DTCustomer.DriveState`)**
```
1. 현재 waypoint로 MoveTowards
2. 도착 시 waypoint 타입 분기:
   - OrderStop → EnterOrderStop → WAIT_AT_ORDER
   - PickupStop → EnterPickupStop → WAIT_AT_PICKUP
   - Exit → Despawn
   - 일반 → TryAdvance (다음 waypoint 비어있으면 진행)
```

**Animator 연동**
- `Direction` int 파라미터 (0=오른쪽, 1=위, 2=왼쪽, 3=아래)
- `Turn` trigger — 방향 바뀔 때 발동 (스폰 시는 X)
- `ComputeDirection(v)` — 이동 벡터로부터 방향 계산

**주문 응대 흐름**
```
1. DT 차량이 OrderWindow에 도착 → OnCarArrived(this)
2. ServerStaff.IdleAtCounterState ④ — GetOrderWindowWithUnservedCar 발견
   → TryClaim 성공 → WALK_TO_DT_ORDER → TAKING_DT_ORDER
3. takingOrderDuration 후:
   - 결제(매출 기록 + MoneySystem.Earn)
   - PassWindowManager.SubmitOrder (type = DT, dtCustomer = car)
   - car.OnOrderTaken → 차는 DRIVE 재개
   - ReleaseClaim
```

**픽업 흐름 — 음식 매칭이 중요**
```
1. Cook이 DT 음식 조리 → PassWindow.PlaceFood (food.order.dtCustomer 보존)
2. ServerStaff.IdleAtCounterState ② — PassWindow에서 DT 음식 클레임
   → WALK_TO_PASS_WINDOW → 음식 픽업 → WALK_TO_DT_PICKUP
3. DTPickupWindow.PlaceFood(food) — food.order.dtCustomer를 키로 슬롯에 저장
4. DTCustomer.PickupCheck — _pickupWindow.HasReadyFoodFor(this)
   → TakeFoodFor(this) — 자기 음식만 가져감
```

> ⚠️ **중요한 버그 해결**: 픽업창구가 단일 슬롯이면, 차 2대 대기 중 음식이 2개 먼저 도착하면 첫 음식이 덮어쓰여져 사라지고 1번 차가 2번 음식을 가져가버림. 그래서 `Dictionary<DTCustomer, Food>` 기반으로 차별 슬롯을 운영. 차가 destroy될 때는 `ClearFor(this)`로 누수 방지.

**해금**
- `DTSystem.Unlock()` — 만족도 800 차감 (`unlockSatisfactionCost`). PhoneManager 패턴 동일.
- 해금 + 영업 중 + 22시 미만일 때 스폰. 영업 종료 시 스폰만 멈춤(차는 정상 흐름 유지).

### 6-3. 차량 상태 enum

```csharp
public enum DTState {
    DRIVE,                  // waypoint로 이동 중
    WAIT_AT_ORDER,          // OrderStop에서 직원 응대 대기
    WAIT_AT_PICKUP,         // PickupStop에서 음식 대기
}
```

DT 차량 patience는 사실상 무한 — 디자인상 응대/음식 받을 때까지 무조건 대기 (ForceLeave는 방어용으로만 존재).

---

## 7. 직원 시스템 (`Staff/`)

| 파일 | 역할 |
|---|---|
| `Staff.cs` | 추상 베이스. `_data`, `id`, `_hireVariance`, `_growthMultiplier`, `MoveTo/HasArrived`, `TickMonth` |
| `CookStaff.cs` | 요리사. (IDLE_AT_KITCHEN → WALK_TO_TOOL → USING_TOOL → WALK_TO_PASS_WINDOW) |
| `ServerStaff.cs` | 서버. (IDLE_AT_COUNTER → TAKING_ORDER / WALK_TO_PASS_WINDOW / WALK_TO_PHONE / WALK_TO_DT_ORDER / WALK_TO_DT_PICKUP …) |
| `RiderStaff.cs` | 라이더. (IDLE_AT_RIDERPOS → WALK_TO_PASSWINDOW → WALK_TO_EXIT → DELIVER → RETURN_TO_ENTRY) |
| `StaffManager.cs` | 직원 고용/해고/등급 조회. 월별 정산 tick |
| `StaffData.cs` | 직원 SO (role, grade, salary, hireCost, moveSpeed, kindness, speedMultiplier, deliveryTime 등) |
| `StaffType.cs` | grade enum (Junior/Senior/Manager) |
| `StaffState.cs` | 각 직원 FSM state enum 모음 |
| `StaffCandidatePool.cs` | 채용 후보 풀. 만족도로 RecruitmentTicket 구매 → 몇 달 후 후보 등장 |
| `StaffCandidate.cs` | 후보 1명 |
| `Recruitment.cs` | 채용 등급 enum + Config + Ticket |
| `RestSpotPicker.cs` | 휴식지 픽 유틸 |

### 시스템적 핵심

**ServerStaff IDLE 우선순위 (중요!)**
1. 홀 음식 클레임 (PassWindow에 Hall 음식 있음)
2. **DT 음식 클레임** (PassWindow에 DT 음식 있음)
3. 카운터 응대 (손님 있는데 서버 없음)
4. **DT 주문 응대** (차 있는데 서버 없음)
5. 전화 응대 (ring 중인 폰)
6. 그 외엔 빈 카운터 StaffPos로 대기

각 단계마다 `IsClosestIdleServerTo(target)` 호출해서 **가장 가까운 idle 서버 한 명만** 해당 작업을 가져감. 동률이면 더 작은 Id가 우선 (안정적 분배).

**공통 휴식 패턴**
- Cook → Kitchen zone 안 빈 셀
- Rider → RiderRoom zone 안 빈 셀
- Server → 빈 Counter.StaffPos
- 모두 `_currentRestTarget` sticky 처리 (한 번 정하면 다른 직원이 차지하기 전엔 안 바꿈, 흔들림 방지)

**보정 공식**
- `EffectiveMoveSpeed = moveSpeed × (1 + hireVariance)`
- `EffectiveKindness = kindness × (1 + hireVariance) × growthMultiplier`
- `EffectiveSpeedMultiplier = speedMultiplier × (1 + hireVariance) × growthMultiplier` (Cook 조리 속도)
- `EffectiveDeliveryDuration = deliveryTime / divisor − RiderRoomBonus` (Rider)

**채용 흐름**
1. 만족도로 RecruitmentTicket 구매 (월 1회 제한)
2. `ticketDelayMonths` 경과 후 후보 풀에 N명 등장
3. Hire하면 `StaffManager.HireCook/Server/Rider` 호출

---

## 8. 가구 시스템 (`Furniture/` + `Counter/`)

### 8-1. 카운터 (`Counter/`)
| 파일 | 역할 |
|---|---|
| `Counter.cs` | 손님 대기 위치(ServicePos) + 서버 응대 위치(StaffPos). 클레임 시스템 |
| `CounterManager.cs` | 카운터 리스트. 빈 카운터 / 손님 있는 카운터 조회 |

### 8-2. 기본 가구 (`Furniture/BasicFurniture/`)
| 파일 | 역할 |
|---|---|
| `Seat.cs` | 손님 좌석. 점유 플래그 |
| `SeatManager.cs` | 좌석 등록/빈자리 찾기 |
| `PassWindow.cs` | 주방-홀 통과 카운터. **`pendingOrders` Queue + `readyFoods` List**. 음식 슬롯 시각화 |
| `PassWindowManager.cs` | PassWindow 풀. 통합 조회/픽업. `HasReadyFood(OrderType)` / `ClaimFood(OrderType, claimer)` |
| `Phone.cs` | 배달 전화기. ring 상태 + claim 시스템 |
| `PhoneManager.cs` | Phone 인스턴스 관리. ring 타이머 + 콜 생성 + 만족도 해금 |
| `RiderRoomManager.cs` | 라이더룸 zone 안 빈 셀 + deliveryBonus 합산 |
| `DTOrderWindow.cs` | DT 주문창구 (위 6장 참조) |
| `DTPickupWindow.cs` | DT 픽업창구 (위 6장 참조) |
| `DTWindowManager.cs` | DT 창구 등록 매니저 |
| `PlacementZone.cs` | zone 영역 정의 |
| `FurnitureData.cs` | 가구 SO (width, height, anchor, sprite, deliveryBonus 등) |

### 8-3. PassWindow의 음식 클레임 시스템 (중요)

`Food.claimedBy` 필드로 "누가 가져갈 거다" 예약 시스템 운영.

```
1. Server.IDLE — PassWindowManager.ClaimFood(OrderType.Hall, this) 호출
   → food.claimedBy = server (음식은 여전히 PassWindow에 있음)
2. Server.WALK_TO_PASS_WINDOW — 픽업창구 도착 시
   → PassWindowManager.TakeFood(_claimedFood) 호출
   → readyFoods에서 제거 + claimedBy 정리 + 음식 들기
```

이거 때문에 음식 1개를 서버 2명이 동시에 가져가는 일이 없음.

### 8-4. 조리도구 (`Furniture/CookingTool/`)
| 파일 | 역할 |
|---|---|
| `CookingToolInstance.cs` | 조리도구 인스턴스 |
| `CookingToolManager.cs` | 도구 등록/검색. 도구 인접 위치 조회 |
| `CookingToolData.cs` | 도구 SO (toolType, usingDuration) |
| `ToolType.cs` | 도구 종류 enum |

---

## 9. 주문 / 메뉴 / 음식 (`Product/`)

| 파일 | 역할 |
|---|---|
| `Order.cs` | `customer`(홀) / `dtCustomer`(DT) / `menus[]` / `type` (OrderType) |
| `OrderType.cs` | enum: Hall / Delivery / DT |
| `Food.cs` | `order` 참조. 조리 완료 후 PassWindow에 놓임. `claimedBy` 필드 |
| `MenuData.cs` | 메뉴 SO (이름, 가격, 원가, 사용 도구, 조리 시간, 스폰 weight) |
| `MenuManager.cs` | 전체 메뉴 리스트. weight 기반 랜덤 선택 |

**Order 라우팅:**

| OrderType | 누가 받는가 | 어디로 전달 |
|---|---|---|
| Hall | Server가 카운터에서 | 손님 좌석 |
| Delivery | Server가 전화에서 | Rider가 배달 (외부) |
| DT | Server가 DT창구에서 | DT 픽업창구 → 차 |

`Order.customer` 또는 `Order.dtCustomer` 둘 중 하나만 채워짐. type으로 분기.

---

## 10. 돈 / 만족도 / 평판 / 매출 (`MoneyAndSatisfaction/`)

| 파일 | 역할 |
|---|---|
| `MoneySystem.cs` | 잔액. `Earn`/`Spend`/`ForceSpend`. `SettleMonthly` (재료비 + 급여 + 유지비) |
| `SatisfactionSystem.cs` | 만족도(int). 손님이 떠날 때 누적. Phone 해금, DT 해금, 마케팅, 채용에 사용 |
| `ReputationSystem.cs` | 연간 평판(long). 손님 만족도 누적. 랭킹 점수 계산 |
| `SalesTracker.cs` | 월별 메뉴별 판매량 + 연간 매출. 정산 시 재료비 계산 |

**자원 흐름:**
- 손님 결제 → `MoneySystem.Earn`
- 손님 만족 → `SatisfactionSystem.Earn` + `ReputationSystem.Report`
- 만족도 → Phone/DT 해금, 마케팅 구매, 채용 티켓에 차감
- 매달 정산 → 재료비(SalesTracker) + 급여(StaffManager) + 유지비(셀 수 × 단가)

---

## 11. 마케팅 (`Marketing/`)

| 파일 | 역할 |
|---|---|
| `MarketingData.cs` | 캠페인 SO (만족도 비용, 기간 개월, spawnBoost) |
| `MarketingManager.cs` | 만족도 차감 → pending → 다음 영업일에 active. spawnBoost 합산 → `CustomerManager` 스폰 multiplier |

**multiplier 공식:** `1 + log(1 + sumBoost)` (디미니싱 리턴)

---

## 12. 랭킹 (`Ranking/`)

| 파일 | 역할 |
|---|---|
| `RankingSystem.cs` | 연말 `OnYearEnded`에 `score = revenue/divisor + reputation`. dummyTop100과 비교해서 순위. 최소 점수 미달이면 순위권 외 |

---

## 13. 디버그 / UI / 기타

### `Debug/TestDebugPanel.cs`
인스펙터에서 의존성 연결 후 UI 버튼에서 호출:
- **시간**: ApplyFastTime / ApplyNormalTime / SkipOneMonth / Skip3Months / Skip12Months
- **돈**: AddMoney
- **만족도**: AddSatisfaction1000
- **마케팅**: ApplyTestMarketing / ForceApplyTestMarketing / LogSpawnInterval
- **손님**: StartCustomerSpawning / StopCustomerSpawning / SpawnOneCustomer
- **직원**: TickAllStaffMonth / LogStaffStatus
- **라이더**: ForceUnlockPhone / HireRider
- **DT**: ForceUnlockDT / DebugForceSpawnOne
- **가구**: StartPlaceRandomFurniture / StartRemoveMode / StartMoveMode / ConfirmPlacement / CancelPlacement
- **상태**: LogGameStatus

### 카메라 / UI / Util
| 파일 | 역할 |
|---|---|
| `Camera/TransparencySortSetup.cs` | URP 카메라에 Custom Y-axis 정렬 적용 |
| `UI/HudView.cs` | 시간/돈/만족도 HUD 표시 |
| `Util/MoveUtil.cs` | 좌표 유틸리티 |

---

## 14. 전체 흐름 따라가기

### 14-1. 홀 손님 (앉아서 식사)
```
1. CustomerManager.Spawn() — entryPoint에 생성
2. Customer.WAIT_FOR_SEAT — 빈 좌석 폴링
3. 좌석 발견 → 큐 등록 → Enter → WALK_TO_COUNTER
4. ServerStaff.IDLE③ — 손님 있는 카운터 발견 → TryClaim → TAKING_ORDER
5. takingOrderDuration 후 Order(Hall) 생성 → PassWindowManager.SubmitOrder
6. Customer.OnOrderTaken — 결제 (MoneySystem.Earn) → WALK_TO_SEAT
7. CookStaff.IDLE — pending order dequeue → 각 메뉴.tool 조리 → PassWindow.PlaceFood
8. ServerStaff.IDLE① — Hall 음식 클레임 → WALK_TO_PASS_WINDOW → WALK_TO_SEAT
9. Customer.OnFoodDelivered → EAT → eatSpeed 후 LEAVE
10. 좌석 해제 + 만족도 합산 + 평판 보고 + 매출 기록
```

### 14-2. 배달 손님 (전화)
```
1. PhoneManager — 8~20초 ring 타이머 → Phone.StartRinging
2. ServerStaff.IDLE⑤ — ringing 발견 + claim 성공 → WALK_TO_PHONE → TAKING_DELIVERY_ORDER
3. PhoneManager.AcceptCall → Order(Delivery) → PassWindow 큐
4. Cook 조리 → PassWindow.PlaceFood
5. RiderStaff.IDLE — Delivery 음식 발견 → 픽업 → WALK_TO_EXIT → DELIVER (invisible)
   → entry 위치 텔레포트 → RETURN_TO_ENTRY → 라이더룸 복귀
6. PhoneManager.OnDeliveryCompleted — 카운터 감소
```

### 14-3. 드라이브 쓰루 (NEW)
```
1. DTSystem — 영업 중 + 해금 + 22시 전 → 차로 가능 → Spawn
2. DTCustomer.Init — 메뉴 미리 결정 + DTLane.RegisterCar + Entry waypoint
3. DTCustomer.DriveState — waypoint 따라 이동 (다음 칸 점유 시 대기)
4. OrderStop 도착 → WAIT_AT_ORDER → DTOrderWindow.OnCarArrived
5. ServerStaff.IDLE④ — unserved 차 발견 → TryClaim → WALK_TO_DT_ORDER → TAKING_DT_ORDER
6. 매출 기록 + MoneySystem.Earn + PassWindow.SubmitOrder(DT, dtCustomer=car)
   → car.OnOrderTaken → 차는 DRIVE 재개 → 픽업창구로 이동
7. Cook이 DT 음식 조리 → PassWindow.PlaceFood
8. ServerStaff.IDLE② — DT 음식 클레임 → WALK_TO_PASS_WINDOW → WALK_TO_DT_PICKUP
   → DTPickupWindow.PlaceFood(food) — food.order.dtCustomer 키로 슬롯 저장
9. DTCustomer.PickupCheck — HasReadyFoodFor(this) 확인 → TakeFoodFor(this)
   → 음식 부착 → DRIVE 재개 → Exit → Despawn
10. 만족도 합산 + 평판 보고
```

---

## 15. "X 바꾸려면 어디?"

| 바꾸고 싶은 것 | 어디 |
|---|---|
| 시간 흐름 속도 | `TimeSystem.cs` 인스펙터 `_hourInterval`, `_nightHourInterval` |
| 영업 시간 | `TimeSystem.cs` 인스펙터 `_openHour`, `_closeHour` |
| 시작 자금 | `MoneySystem.cs` 인스펙터 `startingMoney` |
| 셀 유지비 단가 | `MoneySystem.cs` `PricePerSquareMeter` |
| 그리드 크기 | `GridManager.cs` 인스펙터 `_gridWidth`, `_gridHeight` |
| 초기 활성 영역 | `GridManager.cs` `_startGridWidth`, `_startGridHeight` |
| 초기 zone 분포 | `GridManager.CreateGrid()` 조건문 |
| 손님 스폰 간격 | `CustomerManager.cs` `_minSpawnInterval`, `_maxSpawnInterval` |
| 손님/DT 차량 종류 | `CustomerData` / `DTCustomerData` SO 만들어서 매니저 pool에 등록 |
| 메뉴 추가 | `MenuData` SO 만들어서 `MenuManager`에 등록 |
| 가구 종류 추가 | `FurnitureData` SO 만들어서 PlacementSystem 사용 |
| 도구 종류 | `ToolType.cs` enum + `CookingToolData` SO |
| 직원 등급 데이터 | `StaffData` SO. `StaffManager.cookGrades` 등에 등록 |
| 라이더 상한 | `StaffManager.cs` `maxRiderCount` |
| 전화 ring 간격 | `Phone.cs` 인스펙터 `minCallTimer`, `maxCallTimer` |
| 전화 ring 타임아웃 | `PhoneManager.cs` `ringTimeout` |
| 배달 메뉴 수 | `PhoneManager.cs` `minOrderCount`, `maxOrderCount` |
| 폰 해금 비용 | `PhoneManager.cs` `unlockSatisfactionCost` |
| DT 해금 비용 | `DTSystem.cs` `unlockSatisfactionCost` (기본 800) |
| DT 스폰 간격 | `DTSystem.cs` `minSpawnInterval`, `maxSpawnInterval` |
| DT 스폰 차단 시각 | `DTSystem.cs` `newSpawnCutoffHour` (기본 22시) |
| DT 차로 최대 차 수 | `DTLane.cs` `maxCars` |
| DT 차로 waypoint 배치 | DTLane GameObject의 자식 Transform들 + 인스펙터 `waypoints`, `orderStopIndex`, `pickupStopIndex` |
| 배달 최소 시간 | `RiderRoomManager.cs` `minDeliveryDuration` |
| 도착 판정 거리 | `PathMover.cs` `CELL_ARRIVE`, `FINAL_ARRIVE` |
| 휴식지 충돌 반경 | 각 Staff cs의 `kRestBlockRadius` (현재 0.5) |
| 확장 단계 추가 | `ExpansionStageData` SO 만들어서 `ExpansionManager.stages`에 등록 |
| 마케팅 캠페인 추가 | `MarketingData` SO 생성 |
| 랭킹 더미 분포 | `RankingSystem.cs` 인스펙터 `autoDummyTopScore`, `autoDummyBottomScore`, `autoDummyExpCurve` |
| 채용 비용/딜레이 | `StaffCandidatePool.cs` `tierConfigs`, `ticketDelayMonths` |

---

## 16. 시스템 간 의존성 다이어그램

```
[TimeSystem] ─── OnHourChanged ──── HUD
     │
     ├── OnCloseHourReached ── DayCycleController ── 영업종료
     │                                │
     │                                ├── CustomerManager.StopSpawning + ForceLeave
     │                                └── DTSystem.StopSpawning (차는 정상 흐름)
     │
     ├── OnDayStarted ── MarketingManager (campaign 진행)
     │                ── StaffCandidatePool (티켓 진행)
     │                ── CustomerManager.StartSpawning
     │                ── DTSystem.StartSpawning
     │
     └── OnYearEnded ── RankingSystem (점수 산출)

[Customer]   ── 만족도 ── SatisfactionSystem ── 마케팅/채용/Phone해금/DT해금 비용
             ── 평판   ── ReputationSystem  ── 랭킹 점수
             ── 결제   ── Counter ── MoneySystem.Earn
             ── 주문   ── SalesTracker.RecordSale

[DTCustomer] ── 만족도 ── SatisfactionSystem
             ── 평판   ── ReputationSystem
             ── 주문   ── DTOrderWindow → ServerStaff → 매출 기록 + MoneySystem.Earn
                                                  └── PassWindow.SubmitOrder(DT)

[Cook]    ── PassWindow에서 Order dequeue → 조리 → Food 배치 (type 보존)
[Server]  ── PassWindow에서 Hall/DT 음식 클레임 → 좌석 or DT픽업창구로 운반
[Rider]   ── PassWindow에서 Delivery 음식 클레임 → 배달 사이클

[PhoneManager] ── ring → Server가 받음 → Order(Delivery) → 일반 조리 흐름
[DTSystem]     ── Spawn → DTCustomer → OrderWindow → PickupWindow → Exit

[GridManager] ── 모든 walkable 판정 + 인접 셀 계산 + zone 관리
[Pathfinder]  ── A*로 경로 계산 (PathMover가 사용)
```

---

## 17. 자주 헷갈리는 것들

### Q. 왜 직원이 자꾸 자리를 옮기지?
Rest spot이 매 프레임 재계산되던 버그. 지금은 `_currentRestTarget` sticky 처리. 다른 직원이 그 자리(0.5m 이내)에 도달하기 전까진 안 바꿈.

### Q. 왜 서버 둘이 같이 전화 받으러 가?
Phone에 Claim 시스템 추가됨. `TryClaim` 성공한 한 명만 출발. 이미 claim된 폰은 `IsClaimedByOther`로 무시.

### Q. 왜 서버가 의자 위로 걸어가?
Pathfinder가 목적지 셀은 walkable 아니어도 허용함. 호출자가 `GetFurnitureApproachPosition`을 거쳐 의자 인접 셀까지만 가도록 해야 함.

### Q. 왜 모든 직원이 가구 옆 같은 방향으로만 가?
`GetFurnitureApproachPosition`에 `from` 파라미터가 있어서 요청자 위치 기준 가장 가까운 인접 셀 선택.

### Q. 라이더 고용이 왜 실패?
`TestDebugPanel.HireRider`가 사전점검 로그 출력. 보통 원인:
1. StaffManager 인스펙터의 `riderStaffPrefab` 미할당
2. Phone 미설치 또는 미해금 (ForceUnlockPhone)
3. RiderRoom zone 없음 (확장 단계 적용 필요)
4. 라이더 상한 도달

### Q. 확장하면 카메라가 안 움직임
지금은 `ActivateCells` 끝에서 자동 `CenterCameraOnActiveGrid()` 호출됨. 활성 셀 + DTLane bbox 중앙 + ortho size 자동 조정.

### Q. DT 차 2대 도착 전에 음식이 2개 나오면 고장?
**옛 버그.** DTPickupWindow가 단일 슬롯이어서 두 번째 PlaceFood가 첫 음식을 덮어썼음. 지금은 `Dictionary<DTCustomer, Food>`로 차별 슬롯. 각 차는 `HasReadyFoodFor(this)` / `TakeFoodFor(this)`로 **자기 음식만** 가져감.

### Q. DT 차가 픽업창구 도착했는데 음식이 안 보임?
- DT 음식이 PassWindow에 도착했는지 확인 (Cook이 조리 완료)
- 서버가 DT 음식 클레임할 수 있는 상태(IDLE_AT_COUNTER)인지 확인
- `DTPickupWindow.StaffPos` 인접 셀이 walkable인지 (서버가 접근 가능해야 PlaceFood 가능)

### Q. DT 차가 OrderStop에서 안 떠남?
- 서버가 IDLE이어야 응대 가능 — 다른 작업(홀, 전화, DT 음식 운반) 우선순위가 위에 있어서 서버가 모자라면 적체 발생
- ServerStaff IDLE 우선순위(7장 참조) 다시 확인

---

## 18. 추가 자료
- `PROJECT_OVERVIEW.md` — 기존 개요 (DT 추가 전)
- `PROJECT_CONTEXT.md` — 게임 디자인 컨텍스트
- `Assets/_project/Datas/` — 모든 ScriptableObject asset (StaffData, MenuData, CustomerData, DTCustomerData, FurnitureData, ExpansionStageData 등)
