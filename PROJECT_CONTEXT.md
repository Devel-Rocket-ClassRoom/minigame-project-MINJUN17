# 프로젝트 컨텍스트 - 레스토랑 경영 시뮬레이션

> **이 문서의 목적**: AI 어시스턴트(Claude, ChatGPT, Copilot 등)에게 프로젝트 컨텍스트를 한 번에 전달.
>
> **표기 규칙**: `TBD`는 미결정 항목. AI는 임의 가정 금지, 사용자에게 질문할 것.

---

## 1. 프로젝트 메타데이터

| 항목 | 값 |
|------|-----|
| 장르 | 2D 레스토랑 경영 시뮬레이션 |
| 참고 게임 | 카이로소프트류 |
| 엔진 | Unity 2022 LTS (2D URP) |
| 플랫폼 | 안드로이드 (모바일, 세로/Portrait) |
| 언어 | C# |
| 개발자 | 솔로, 유니티 초급 |
| 기간 | 3주 (21일), 풀타임 |
| 목적 | 포트폴리오 |
| 비주얼 | 픽셀아트, 탑다운 |
| 참고 에셋 | https://limezu.itch.io/moderninteriors |

---

## 2. 게임 개요

### 2.1 게임플레이 요약
플레이어는 레스토랑 사장. 가구/조리도구 배치, **직원 종류별 고용/업그레이드**, 메뉴 개발, 마케팅, 매장 확장을 통해 매출과 평판을 올린다.
중요: 모바일(안드로이드 세로 화면) 빌드. 코드/UI/카메라 등 모바일 세로 화면에 맞춰 조정.

**장기 목표**: 매년 발표되는 대한민국 맛집 Top 100에서 1등 달성. 1등 이후에도 지속 플레이 가능.

### 2.2 구현 범위 (사용자 원문 + 확장 반영)
- 가구(의자, 책상) 및 조리도구 직접 배치
- 영업시간이 시작되면 손님 랜덤 입장 (빈도는 가게 평판/점수에 영향받음)
- 손님은 카운터 주문/결제 → 자리 이동 → 자리에서 음식 대기 → 식사(만족도 증가) → 퇴장
- 만족도는 카운터 대기시간, 음식 대기시간, 주변 인테리어(조경) 등을 반영해 랜덤 증가
- 돈과 만족도로 더 좋은 가구, 직원 업그레이드, 다양한 메뉴 개발 등을 구매해서 매출을 올림
- 한 달(인게임)마다 재료비(원가 × 팔린 개수), 운영비(가게 크기, 직원 수 비례) 차감
- 일 년(인게임, 실시간 약 1시간)마다 대한민국 맛집 Top 100 발표 (매출, 가게 평판 기준)
- **직원은 종류별로 분리**: 요리사(주방 상주, 조리 전담) / 홀(카운터·서빙·설거지) / 라이더(배달, 확장 시 추가)
- **공간 구역 분리**: 주방 구역(상단) + 홀 구역(하단), 픽업대(PassWindow)로 연결
- **확장 단계별 구역 추가**: 홀 1단 → 주방 1단 → 홀 2단 (자세한 건 Section 6)
- **DT 시스템**: 최종 확장 보상으로 해금 (자세한 건 Section 6)

---

## 3. 시간 시스템 (확정)

### 3.1 시간 단위
- **인게임 시간 최소 단위 = 한 달**
- **영업 1사이클 (8시 ~ 24시, 16시간) = 한 달 = 실시간 4분**
- 인게임 1년 = 12개월 = 실시간 48분 (약 1시간)
- "인게임 1일" 개념은 없음

### 3.2 영업 사이클
```
[월 시작: 영업 시작 (08:00)]
        ↓
   영업 진행 (8:00 ~ 24:00, 실시간 4분)
        ↓
[월 종료: 영업 종료 (24:00)]
        ↓
   월말 정산 (재료비 차감, 운영비 차감)
        ↓
   12개월마다 → 연말 Top 100 순위 발표
        ↓
   다음 달 시작 → 반복
```

### 3.3 인게임 시간 진행 속도
- 영업 16시간 = 실시간 4분 = **240초**
- 인게임 1시간 = 실시간 15초
- 시간 배속 옵션: TBD (있을지 여부, 있다면 0x/1x/2x/4x 등)

---

## 4. 손님 흐름 (확정, 신구조 P)

### 4.1 상태 흐름
```
WAIT_FOR_SEAT        (자리 대기. 자리 비면 진입, patience 초과 시 LEAVE)
ENTER                (자리 확보 후 카운터 줄로 이동)
WALK_TO_COUNTER      (빈 카운터로 이동)
WAIT_AT_COUNTER      (카운터 점유, 홀 직원 응대 대기 → 주문/결제)
WALK_TO_SEAT         (자리로 이동, 카운터 점유 해제)
WAIT_AT_SEAT         (자리 점유, 홀이 음식 가져올 때까지 대기)
EAT                  (식사, 만족도 증가)
LEAVE                (퇴장)
```

### 4.2 상세 규칙
- **WAIT_FOR_SEAT** *(이슈 #51에서 변경)*: 스폰 즉시 진입. `CustomerManager.waitingSlots`의 대기 위치로 이동. 매 프레임 `SeatManager.GetFirstAvailableSeat()` 폴링.
  - 자리 확보 → 좌석/카운터 큐 등록, `_waitStartTime` 리셋 → `ENTER`
  - `Time.time - _spawnTime > _data.patience` → 만족도 0으로 LEAVE (평판만 떨어짐, 만족도 풀에는 0 가산이라 영향 없음)
  - 대기 슬롯 수보다 대기 인원이 많으면 마지막 슬롯에 겹쳐서 대기
- **ENTER**: 자리 + 카운터 줄(`QueueManager`) 확보 완료. 카운터 줄의 자기 슬롯으로 이동. 줄 맨 앞이고 카운터가 비어있으면 → `WALK_TO_COUNTER`
- **WALK_TO_COUNTER**: 빈 카운터로 이동(카운터는 `Reserve()` 호출되어 다른 손님 차단). 도착 → `WAIT_AT_COUNTER`
- **WAIT_AT_COUNTER**: 손님이 카운터 점유. 홀 직원에게 주문. 결제(돈 증가). 대기 시간이 길수록 만족도 페널티(카운터 대기 페널티). 결제 완료 → `WALK_TO_SEAT`
- **WALK_TO_SEAT**: 자리로 이동. 카운터 점유 해제. 도착 → `WAIT_AT_SEAT`
- **WAIT_AT_SEAT**: 자리 점유. 홀이 픽업대에서 음식 받아 자리로 가져올 때까지 대기. 음식 받음 → `EAT`
- **EAT**: 식사. 만족도 증가. → `LEAVE`
- **LEAVE**: 자리 해제. 출구로 이동. 도착 시 파괴. 만족도 → `SatisfactionSystem.Earn`, 평판 → `ReputationSystem.Report`

### 4.3 구현 노트
- 카운터 점유 상태는 카운터가 관리 (`bool _isOccupied`)
- 자리 점유 상태는 자리가 관리 (`bool isOccupied`)
- 손님 길찾기: 주방 영역 진입 금지 (영역 zone 필터링, 2주차)
- 카운터 대기시간 측정: `_waitStartTime`(자리 확보 시점 리셋) ~ `EAT` 진입. 즉 자리 대기는 patience 페널티에서 제외 — 별도 타임아웃으로 LEAVE 처리
- 만족도 계산식: 기본값 + 직원 친절도 보너스 + 식사 중 가산 - 대기 페널티. EAT 진입 시 카운터 대기 페널티 정산
- `_satisfaction = 0`은 평판에 0 가산이라 평판 평균을 내림 (페널티 효과)

---

## 5. 직원 흐름 (확정, 신구조)

### 5.1 직원 종류
- **요리사 (Cook)**: 주방 영역 상주. 카운터 배정 X. 픽업대에서 주문 확인 → 조리도구 순회 → 픽업대에 음식 놓기.
- **홀 (Server)**: **풀로 운영(카운터 귀속 X)**. 손님이 도착한 카운터로 가장 가까운 idle 서버가 가서 응대. 픽업대 음식 픽업 → 자리에 전달.
- **라이더 (Rider)**: 배달 손님 전담. 미구현. 라이더 시스템 도입 시 `StaffCandidatePool.isDeliveryUnlocked` 토글로 후보 풀에 포함.

### 5.2 카운터-직원 배정 *(이슈 #51에서 변경)*
- **카운터-직원 귀속 폐기**. 1:1 비율은 채용 한도(`MaxServerCount = CounterManager.CounterCount`)로만 유지
- 요리사 채용 한도도 카운터 수와 동일 (`MaxCookCount = CounterManager.CounterCount`)
- ServerStaff IDLE 우선순위:
  1. PassWindow에 준비된 음식 있으면 픽업
  2. `CounterManager.GetCounterWithUnservedCustomer()`로 미점유 대기 손님이 있는 카운터를 찾아 `TryClaim` → 응대
  3. 없으면 가장 가까운 빈 카운터의 `staffPos`로 이동 (다른 서버가 idle 타겟으로 잡지 않은 슬롯 중 거리 최단)
- 카운터는 `_servicingServer` 필드 + `TryClaim/ReleaseClaim`으로 같은 손님에 서버 2명 가는 것 차단

### 5.3 직원 등급 (Cook/Server/Rider 공통)
- **Junior / Senior / Manager** 3등급
- 등급별 ScriptableObject 자산: `Cook_Junior/Senior/Manager`, `Server_Junior/Senior/Manager`, `Rider_Junior/Senior/Manager` (총 9개, 인스펙터에서 `StaffManager.cookGrades/serverGrades/riderGrades` 리스트에 등록)
- 능력치: `moveSpeed`, `kindness`, `speedMultiplier`, `hireCost`, `salary`
- **등급 데이터 튜닝 규칙**: Junior 12개월 성장 누적치(베이스 × 1.36)보다 Senior 베이스가 높도록 설정. 마찬가지로 Senior 36% 성장치보다 Manager 베이스가 높도록.

### 5.4 요리사(Cook) 상태 흐름
```
IDLE_AT_KITCHEN     (주방 영역에서 대기)
CHECK_PASS_WINDOW   (픽업대에 새 주문 있는지 확인)
WALK_TO_TOOL        (필요한 조리도구로 이동)
USING_TOOL          (조리도구 사용, 시간 소요)
[필요한 도구가 여러 개면 WALK_TO_TOOL → USING_TOOL 반복]
WALK_TO_PASS_WINDOW (픽업대로 복귀, 완성품 들고)
PLACE_FOOD          (픽업대에 음식 놓음)
→ IDLE_AT_KITCHEN
```

### 5.5 홀(Server) 상태 흐름
```
IDLE_AT_COUNTER       (카운터 뒤에서 대기)
TAKING_ORDER          (손님 주문 받고 결제 처리)
SUBMIT_ORDER          (픽업대에 주문 전달)
RETURN_TO_COUNTER     (카운터로 복귀, 다음 손님 응대 가능)
[음식 준비 완료 알림 시]
WALK_TO_PASS_WINDOW   (픽업대로 이동, 음식 픽업)
WALK_TO_SEAT          (음식 들고 손님 자리로)
DELIVER_TO_CUSTOMER   (손님에게 음식 전달)
→ IDLE_AT_COUNTER
```

### 5.6 상세 규칙
- 픽업대(PassWindow)는 주문 큐 + 음식 큐를 가짐
  - `Queue<Order> pendingOrders` (홀 → 요리사)
  - `Queue<Food> readyFoods` (요리사 → 홀)
- 요리사는 `pendingOrders`에서 주문 꺼내서 조리 → 완성품을 `readyFoods`에 추가
- 홀은 `readyFoods`에 자기 주문 음식 있으면 픽업하러 이동
- 1주차에는 픽업대 1개 고정 위치, 단순 큐로 시작. 2주차에 픽업대 배치 가능하게 확장 검토.

### 5.7 직원 활동 영역 제한
- 요리사: 주방 영역 + 픽업대 셀까지만 이동 가능 (홀 진입 X)
- 홀: 홀 영역 + 픽업대 셀 + 카운터까지만 이동 가능 (주방 진입 X)
- 길찾기 노드에 영역 정보 반영, 직원 역할에 따라 가능 노드 필터링
- 1주차 끝물에는 enum/필드만 잡아두고, 실제 필터링은 2주차

### 5.8 시작 인원 (튜토리얼 완료 상태) *(변경됨)*
- 시작 카운터 1개
- 시작 요리사 1명 (Junior)
- 시작 홀 1명 (Junior)
- 시작 테이블 세트 (현재 구현 기준 유지)
- ※ 튜토리얼 진입 상태는 3주차 UI 작업 시 구현

### 5.9 직원 근속/성장 *(이슈 #51 신규)*
- 채용 이후 누적 개월(`_tenureMonths`)이 매월(`OnDayStarted`) +1
- 매월 능력치 +3%, 12개월까지 누적(최대 +36%, 캡 `_growthBumps == 12`)
- role별 성장 적용 스탯:
  - Cook → `speedMultiplier` (조리속도, `CookStaff.EffectiveSpeedMultiplier`)
  - Server → `kindness` (친절도, `ServerStaff.EffectiveKindness`)
  - Rider → `speedMultiplier` (배달속도, 구현 시)
- `EffectiveMoveSpeed`는 성장 미적용, 변동치만 반영
- 업그레이드해도 `_tenureMonths`/`_growthBumps`/`_growthMultiplier` 보존(누적치 안 사라짐)
- 성장 매월 틱은 `StaffManager.MonthTick`이 `TimeSystem.OnDayStarted` 구독해서 전체 직원에 일괄 호출

### 5.10 직원 업그레이드(승급) *(이슈 #51 신규)*
- 자격 조건 (`Staff.CanUpgrade`):
  - 다음 등급 = Senior: 누적 6개월 이상
  - 다음 등급 = Manager: 누적 12개월 이상
  - 등급 무관 같은 누적 기준. 채용 시 Junior든 Senior든 채용 시점부터 카운트
  - **Junior → Manager 직행 불가**: 항상 Senior 거쳐서. 누적 12개월차 Junior는 `UpgradeCook/Server` 두 번 연속 호출로 한 번에 Senior→Manager 가능
- 비용: **`next.hireCost / 2`** (만족도 비용 없음)
- 호출: `StaffManager.UpgradeCook(staff)` / `UpgradeServer(staff)` — 다음 등급 자동 조회(`GetNextGrade`)
- `SetData(nextGrade)` 시 데이터만 갈아끼움, 성장 누적치 유지

### 5.11 직원 모집 시스템 *(이슈 #51 신규)*
- 후보는 자동 생성 X, **모집권을 사야 생김**
- 모집권 티어: **Normal / High / Rare**
  - 인스펙터(`StaffCandidatePool.tierConfigs`)에 각 티어의 만족도 비용 + Junior/Senior/Manager weight 등록
  - 제안값: Normal(비용 50, 80/18/2), High(150, 40/50/10), Rare(400, 10/55/35)
- 모집권 구매 = 만족도 차감(`SatisfactionSystem.Spend`) + 2개월 지연 큐(`_pendingTickets`) 추가
- **영업일당 모집권 1회만 구매 가능** (`_purchasedThisMonth` 플래그, 매월 `OnDayStarted`에서 리셋)
- 2개월 후(`monthsRemaining == 0`) 후보 2명 자동 생성:
  - 역할: `isDeliveryUnlocked` 플래그 — false면 Cook/Server 50:50, true면 Cook/Server/Rider 1/3씩
  - 등급: 티어 weight 가중치 추첨
  - 능력치: ±10% 변동치(`_hireVariance`) 1개를 뽑아서 `moveSpeed/kindness/speedMultiplier/salary`에 일괄 적용 (능력↑ ⇒ 월급↑). `hireCost`는 변동 없음
- 후보 풀은 통합 단일 리스트, **최대 5명** 캡. 초과 시 가장 오래된 후보 자동 제거(FIFO). 만료 없음
- 채용 시: `StaffCandidatePool.Hire(candidate)` → `StaffManager.HireXxxStaff(baseData, hireVariance)` → 직원 인스턴스에 `_hireVariance` 저장됨 → 업그레이드해도 변동치 그대로 유지

### 5.12 메뉴-조리도구 매핑
- 각 메뉴는 하나 이상의 조리도구를 순회해서 만들어짐
- 예시: 햄버거는 그릴(패티) + 작업대(조립) 순회
- 어떤 메뉴가 어떤 도구 순서로 가는지: TBD (메뉴 데이터에 포함)
- 단일 도구 메뉴도 가능 (예: 음료는 음료 디스펜서 1번)

### 5.13 직원 클래스 구조 *(이슈 #51 신규)*
```
Staff (abstract MonoBehaviour)         ← 공통 필드/로직
  ├─ _data, id, _stateTimer
  ├─ _tenureMonths, _growthBumps, _growthMultiplier
  ├─ _hireVariance
  ├─ Data, EffectiveMoveSpeed, EffectiveSalary 프로퍼티
  ├─ CanUpgrade, SetData, TickMonth, InitBase, MoveTowards
  │
  ├─ CookStaff : Staff                 ← 주방 FSM
  │    ├─ CookState 상태머신
  │    └─ EffectiveSpeedMultiplier 프로퍼티
  │
  ├─ ServerStaff : Staff               ← 홀 FSM
  │    ├─ ServerState 상태머신
  │    └─ EffectiveKindness 프로퍼티
  │
  └─ (RiderStaff : Staff)              ← 추후 추가
```

---

## 6. 그리드 / 배치 / 맵 확장

### 6.1 그리드 (확정)
- **최대 9×12 (가로 9 × 세로 12)**
- **시작 활성 영역 4×8** (주방 4×3 상단 + 홀 4×5 하단)
- 비활성 셀: 배치 불가, 길찾기 노드 비활성화
- 활성 셀은 단순 사각형이 아닐 수 있음 (확장 단계에 따라 ㄴ자 형태)
- 셀별 활성 플래그: `bool[,] isActive = new bool[9, 12]`
- 셀별 영역(zone): `CellZone[,] zoneMap = new CellZone[9, 12]`
  - `enum CellZone { Inactive, Kitchen, Hall, Border }`
  - Border = 주방-홀 경계 셀 (픽업대 배치 가능)

### 6.2 시작 활성 영역 좌표 (4×8)
```
좌표계: (x=가로, y=세로), 좌상단 (0,0)

      0  1  2  3  (가로)
   ┌─────────────┐
 0 │  주방        │  (y=0~2, 4×3 = Kitchen zone)
 1 │  4 × 3      │
 2 │             │
   ├─────────────┤
 3 │  홀          │  (y=3~7, 4×5 = Hall zone)
 4 │  4 × 5      │
 5 │             │
 6 │             │
 7 │입구 →       │  (0, 7) = 입구/출구 셀 (배치 불가)
   └─────────────┘
(세로)
```

### 6.3 확장 단계 (3단계, 최종 9×12)
1. **홀 1단 확장**: 초기 홀 오른쪽에 **5×5** (가로 5, 세로 5) 추가 → 우하단
   - 가로 4~8, 세로 7~11 영역 활성화 (Hall zone)
   - 5×5 중 2열은 카운터 배치 예정 영역
2. **주방 1단 확장**: 시작 주방 위쪽에 **4×4** (가로 4, 세로 4) 추가 → 좌상단
   - 가로 0~3, 세로 0~3 영역 활성화 (Kitchen zone)
   - 시작 주방까지 합쳐서 주방 최종 4×7
3. **홀 2단 확장**: 주방 오른쪽 + 홀 1단 위쪽 **5×7** (가로 5, 세로 7) 추가 → 우상단
   - 가로 4~8, 세로 0~6 영역 활성화 (Hall zone)
   - 이로써 9×12 직사각형 완성

### 6.4 최종 형태 (9×12)
```
      0  1  2  3 │ 4  5  6  7  8
   ┌──────────────┬────────────────┐
 0 │              │                │
 1 │  주방 1단     │                │
 2 │  4 × 4       │    홀 2단      │
 3 │              │    5 × 7       │
   ├──────────────┤                │
 4 │              │                │
 5 │  시작 주방    │                │
 6 │  4 × 3       │                │
   ├──────────────┼────────────────┤
 7 │              │                │
 8 │  시작 홀      │    홀 1단      │
 9 │  4 × 5       │    5 × 5       │
10 │              │                │
11 │              │                │
   └──────────────┴────────────────┘

검산: 16 + 12 + 35 + 20 + 25 = 108 = 9×12 ✓
```

### 6.5 DT 시스템 (최종 확장 보상)
- 가게 전부 확장(9×12 완성) 이후 해금
- DT 손님 = 차로 와서 창구에서 받고 나감 = Section 4 손님 FSM의 단축 버전
- DT 직원 = 창구 전담 (홀 직원의 변형)
- 1~2주차에는 구현하지 않음. 3주차 또는 그 이후로 미룸.
- 가로가 9이고 최대 12까지는 여유 있음 → DT 차로 라인은 가로 확장 또는 그리드 외부 표현
- 구체 사양: TBD

### 6.6 배치
- 가구, 조리도구, 카운터, 픽업대를 그리드에 배치
- 놓기 / 철거 / **이동** 지원
- 각 배치 오브젝트는 `PlacementZone` 필드 보유
  - `enum PlacementZone { Kitchen, Hall, Border }`
  - 조리도구 = Kitchen, 카운터/테이블/의자 = Hall, 픽업대 = Border
- 배치 시 `isActive` + `zone` 둘 다 체크
- 입구 셀 (0, 7)은 배치 불가
- 영업 중 배치 가능 여부: TBD

### 6.7 카메라 / 화면
- 화면 방향: **세로 (Portrait)**
- 카메라: **고정** (1주차 기준, 줌/스크롤 없음)
- 최종 9×12 그리드를 세로 화면에 표시. Orthographic Size = 6 (셀 크기 1 기준)
- 확장 단계가 늘어나며 화면에 빈 공간이 생기지만, 줌/스크롤은 필요 시 추후 추가

---

## 7. 재화 / 경제 시스템

### 7.1 재화
| 재화 | 획득 | 소비 |
|------|------|------|
| 돈 | 손님 결제 | 가구/조리도구 구매, 직원(종류별/등급별) 채용/업그레이드, 맵 확장, 운영비, 재료비 |
| 만족도 | 손님 식사 후 (대기시간/인테리어 반영, 랜덤) | 신메뉴 해금, 마케팅 |

### 7.2 정산 (인게임 한 달마다) *(이슈 #50에서 구현)*
- `MoneySystem.SettleMonthly()` 호출:
  - **재료비** = `SalesTracker.CalculateMaterialCost()` = Σ(메뉴 원가 × 판매 개수)
  - **운영비** = `GridManager.ActiveCellCount × PricePerSquareMeter` (셀당 비용은 인스펙터)
  - **일당** = `StaffManager.CalculateTotalSalaryCost()` = Σ(직원 `EffectiveSalary`)
- 정산 후 `SalesTracker.ResetMonthly()` 호출 (메뉴 판매 통계 리셋)

### 7.3 평판 *(이슈 #50에서 구현, 의미 변경)*
- **손님 빈도에 영향 없음** (스폰 가중치 multiplier 제거됨)
- `ReputationSystem.AnnualReputation` = 1년간 손님 만족도 합산(`int customerSatisfaction` 누적)
- 손님이 LEAVE 진입 시 `ReputationSystem.Report(_satisfaction)` 호출
- 연말(`OnYearEnded`)에 `RankingSystem`이 사용 후 `ResetAnnual()`

### 7.4 Top 100 순위 (인게임 매년) *(이슈 #50에서 구현)*
- 트리거: `TimeSystem.OnYearEnded` (12월 → 1월 롤오버)
- 점수 = `AnnualRevenue / revenueDivisor + AnnualReputation`
  - 기본 `revenueDivisor = 100` (매출 100분의 1로 축소해서 평판 합산)
- 점수 ≥ `minQualifyingScore`(인스펙터) 미달 시 순위 미표시 (Qualified=false)
- 자격 충족 시 인스펙터의 `dummyTop100`(내림차순 정렬된 더미 점수 리스트)과 대조해 자기 순위 산출
- 점수 임계값/더미 데이터는 1년 플레이 후 캘리브레이션 예정 (TBD)

### 7.5 마케팅 *(이슈 #50에서 구현)*
- `MarketingData` (SO): `satisfactionCost`, `spawnBoost`, `durationMonths`
- `MarketingManager.Apply(data)` → 만족도 차감 + `_pending` 큐에 추가 (오늘 효과 X)
- 다음 영업일 `OnDayStarted`에 `_pending` → `_active`로 이동, 효과 시작
- 매 영업일마다 활성 캠페인 `RemainingMonths -= 1`, 0이면 자동 만료
- 손님 스폰 빈도 가중치: `multiplier = 1 + ln(1 + Σspawnboost)` — 누적될수록 디미니싱, 절대 감소하지 않음
- `CustomerManager.SetMarketingMultiplier(m)` → `RollSpawnInterval`에서 `baseInterval / multiplier`

---

## 8. 컨텐츠 명세

### 8.1 가구
- 의자, 책상 등 (Hall zone 전용)
- 종류/가격/크기/효과: TBD

### 8.2 조리도구
- 요리사가 사용하는 도구 (Kitchen zone 전용)
- 종류/가격/사용시간: TBD

### 8.3 카운터
- 홀 직원 배정 단위 (Hall zone 전용)
- 시작 시 2개
- 가격/크기: TBD

### 8.4 픽업대 (PassWindow)
- 주방-홀 경계 셀(Border) 전용
- 주문 큐 + 음식 큐 관리
- 1주차에는 1개 고정 위치, 점유/큐 상태 관리
- 가격/배치 가능 여부: 2주차 이후 결정

### 8.5 메뉴
- 만족도로 해금
- 종류/원가/판매가/필요 조리도구 순서: TBD

### 8.6 직원 (종류별/등급별)
- 종류: 요리사 / 홀 / 라이더
- 등급: 신입 / 경력 / 매니저
- 종류×등급 매트릭스로 능력치 정의
- 채용 비용, 일당, 업그레이드 항목: TBD

### 8.7 마케팅
- 만족도 소비 → 손님 빈도 증가
- 종류/비용/효과: TBD

### 8.8 손님
- 시스템적 동작은 1종 (홀 손님)
- 외형만 여러 종 (개수 TBD)
- 배달 손님(라이더 대상)은 별도 시스템, 라이더와 함께 추가

---

## 9. 1주차 작업 범위

### 9.1 원본 명세 (변경 전, 참고용)
- 그리드: 12×12 격자, 활성 영역 6×6
- 배치: 가구/조리도구 놓기/철거/이동
- 손님 AI: 입장 → 카운터 → 자리 → 식사 → 퇴장
- 직원 AI: 주문 받음 → 조리도구 순회 → 복귀 → 전달
- 시간: 영업시간 08~24시, 4분 = 한 달
- 돈/만족도: 기본 재화 시스템

### 9.2 신규 명세 (확장 반영, 1주차 실제 작업)
- **그리드**: 9×12 격자, 시작 활성 영역 4×8 (주방 4×3 + 홀 4×5)
- **셀별 활성 플래그 + zone(Kitchen/Hall/Border) 관리**
- **배치**: 가구/조리도구/카운터/픽업대 놓기/철거/이동 + zone 체크
- **손님 AI**: Section 4 신구조 P (7상태, 카운터 → 자리 → 자리에서 음식 대기 → 식사 → 퇴장)
- **직원 AI**: Section 5 신구조 — 1주차에는 enum/필드만 분리, FSM은 통합 유지 (분리는 2주차)
- **픽업대(PassWindow)**: 고정 위치 1개, 주문/음식 큐 관리
- **시간**: 영업시간 08~24시, 4분 = 한 달
- **돈/만족도**: 기본 재화 시스템
- **카메라**: 세로 고정, 9×12 전체 표시
- **입구/출구**: 홀 좌하단 (0, 7), 배치 불가 셀

### 9.3 1주차 작업 완료 상태

#### 그리드/카메라 (Phase 1) ✅
- [x] 시작 그리드 4×8 변경 (주방 4×3 상단 + 홀 4×5 하단)
- [x] 최대 그리드 9×12 변경
- [x] 셀별 `isActive` + `isReserved` 플래그 도입 (`HashSet<Vector2Int>`로 reserved 셀 관리)
- [x] `CellZone` enum 추가 — **Kitchen/Hall만 사용** (Border/Inactive는 필요 시 도입)
- [x] 카메라 Ortho Size = 6 고정, 세로 화면
- [x] 입구 셀 마킹 + 배치 불가 처리 (Unity y-up 좌표계 적용으로 입구는 (0,0))

#### 영역/배치 규칙 (Phase 2) ✅
- [x] `PlacementZone` 필드 추가 + `CanPlace` 시 zone 체크
- [x] 주방/홀 셀 색상 구분 (`GridVisualizer`)

#### 픽업대 + 직원 (Phase 3) ✅ + 추가 작업
- [x] `PassWindow` 클래스 (큐 2개)
- [x] **`PassWindowManager` 신규 도입** (2주차 다중 픽업대 대비, 매니저 패턴)
- [x] 픽업대 시작 위치 고정 배치
- [x] ~~`StaffRole` enum 추가~~ → **`CookStaff` / `ServerStaff` 클래스 완전 분리로 진행** (2주차 작업 땡겨옴)

#### 시작 상태 세팅 (Phase 4) ✅
- [x] `GameInitializer` 신규 — 카운터/픽업대/조리도구/테이블 시작 배치 일괄 처리
- [x] `PlacementSystem.PlaceInitial()` 메서드 신규 — 시작 배치 전용 진입점
- [x] 시작 직원: Cook 1 + Server N (카운터 수만큼 자동 채용 + 배정)

#### 손님/직원 FSM (Phase 5) ✅ + 추가 작업
- [x] 손님 FSM 7상태 P 흐름 (`WAIT_AT_SEAT` 추가)
- [x] 빈 자리 없으면 Init 시점에 즉시 Destroy (자리 선점/확정 로직 같이 정리 — 2주차 작업 땡겨옴)
- [x] 픽업대 주문/음식 큐 연동 (PassWindowManager 경유)
- [x] 만족도 페널티 통합 측정 (ENTER 시점부터)
- [x] **Cook/Server FSM 본격 분리** (원래 2주차) — Server는 IDLE 폴링으로 음식 우선 처리
- [ ] 손님 길찾기 영역 제한 — 현재 레이아웃상 자연 충족, 본격 A* 영역 필터링은 2주차

#### 인프라/패턴 작업 (계획 외 추가)
- [x] 매니저 싱글톤 패턴 통일 (Counter/Seat/Staff/PassWindowManager + Money/Satisfaction)
- [x] 자기 등록 패턴: Counter/Seat/PassWindow가 Awake에서 매니저에 자기 등록 → 런타임 생성 오브젝트 자동 추적
- [x] `TimeSystem._closeHour: 12 → 24` 버그 픽스

#### 1주차에서 미룬 것 (2주차로)
- A* 길찾기 (영역 필터링 포함)
- 만족도 페널티 분리 (카운터 대기 vs 음식 대기)
- 메뉴/조리도구 데이터화 — 1주차에 더미값으로 동작 중

---

## 10. 2주차 작업 범위 (코드 중심)

### 10.1 방향성

1주차에 코드 작업이 빨리 진행돼서 일부 2주차 작업(Staff FSM 분리, 자리 선점, PassWindowManager)을 미리 처리함. 남은 UI/유니티 에디터 작업이 시간을 많이 잡아먹을 것으로 예상되어, **2주차는 핵심 게임 시스템 코드 위주**로 진행하고 UI/폴리시는 3주차로 몰빵.

### 10.2 우선순위

**A. 메뉴/음식/주문 시스템 — 최우선**

지금 모든 더미 데이터(가격, 조리시간, 단일 도구)의 진원지. 이거 들어와야 Cook FSM이 진짜 의미를 가지고 돈/만족도 흐름이 데이터로 작동.

- `MenuData` (이름, 가격, 원가, 조리도구 순서 배열)
- `ToolData` (도구 종류, 사용 시간)
- Cook FSM 단계별 도구 순회 실구현 (현재 단일 `_toolPos` 더미 → 메뉴.tools[] 순회)
- 손님 주문 시 메뉴 선택 → `Order.menu` 보유 → `Counter.ReceiveOrder(menu.price)` 실 가격 반영
- `Counter._currentPrice` 누락 버그 같이 해소

**B. 조리도구/가구/카운터/픽업대 ScriptableObject 데이터화**

`FurnitureData` 패턴 확장. 메뉴 시스템과 묶여있는 도구 먼저 → 그 외 가구.

**C. A* 길찾기 + 영역 필터링**

- A* Pathfinding Project Free 도입
- 노드에 zone 정보 반영
- Customer: Kitchen 진입 금지
- CookStaff: Hall 진입 금지
- ServerStaff: 모든 zone 가능 (Kitchen은 PassWindow 셀까지만)
- 가구 통과 방지

**D. 만족도 페널티 분리**

현재 ENTER~음식받음 통합 측정 → `WAIT_AT_COUNTER 진입~WALK_TO_SEAT 진입` (카운터 대기), `WAIT_AT_SEAT 진입~EAT 진입` (음식 대기) 분리.

**E. 맵 확장 시스템 (코드 레벨)**

- `ExpansionStageData` SO (활성화될 셀 영역 + 비용 + 해금 조건)
- 3단계 확장 데이터 (홀1단 5×5, 주방1단 4×4, 홀2단 5×7)
- `GridManager.ActivateCells(stage)` 메서드 — UI 없이 코드/디버그 키로 동작
- UI는 3주차

**F. 정산/마케팅/순위 시스템 (코드 레벨)** ✅ 완료 (이슈 #50)

- ✅ 월말 정산: `MoneySystem.SettleMonthly` 구현. 재료비/운영비/일당 통합 차감, 메뉴 판매 통계 리셋
- ✅ 마케팅: `MarketingData` SO + `MarketingManager` — 다음 영업일부터 효과, 개월 단위 지속, `ln(1+Σboost)` 디미니싱 (절대 감소 없음)
- ✅ 평판: `ReputationSystem` — 1년 만족도 누적 (스폰 빈도 영향 X)
- ✅ 연말 순위: `RankingSystem` — `OnYearEnded` 트리거, `score = 매출/100 + 평판합`, 임계점 미만 미표시
- ✅ `TimeSystem.OnYearEnded` 이벤트 추가
- UI는 3주차

**G. 직원 고용/배정/업그레이드 시스템 (로직)** ✅ 완료 (이슈 #51)

- ✅ 카운터-직원 귀속 제거 (`Counter.assignedStaff` 삭제, `_servicingServer` claim 시스템). 1:1 비율은 채용 한도로만 유지
- ✅ 손님 자리 대기 큐 (`WAIT_FOR_SEAT` 상태, patience 타임아웃, `_waitingForSeat` 리스트 + 슬롯)
- ✅ 등급별 SO 자산화 (Cook/Server/Rider × Junior/Senior/Manager = 9개)
- ✅ `Staff` 추상 베이스 도입, Cook/Server 상속
- ✅ 근속/성장 시스템 (매월 +3%, 12개월 캡, 36% 누적)
- ✅ 업그레이드 (6/12개월 자격, 비용 `next.hireCost/2`, 누적 능력치 유지)
- ✅ 직원 모집 시스템 (`StaffCandidatePool`) — Normal/High/Rare 모집권, 만족도 비용, 2개월 지연, 풀 캡 5 FIFO, 영업일당 1회
- ✅ 변동치 `_hireVariance` 영구 유지 (±10% 능력/월급 동시, 업그레이드 후에도 유지)
- ✅ 월말 정산 일당 통합 (`EffectiveSalary` 기준)
- 채용 UI는 3주차

**H. 라이더 시스템**

- 매장 손님과 별개의 배달 손님(`DeliveryCustomer`) + 라이더 직원(`RiderStaff`) FSM
- `DeliveryOrderManager`로 배달 주문 발생/관리
- PassWindowManager 경유로 음식 픽업 → 가상 배달 처리

**I. DT(드라이브 스루) 시스템**

- 매장 손님 FSM 단축 버전(`DTCustomer`) + DT 창구(`DTWindow`)
- 9×12 최종 확장 이후 해금 (E 맵 확장과 의존)
- DT 차로는 그리드 외부 표현
- UI/차량 스프라이트는 3주차

### 10.3 미루는 것 (3주차로)

- UI 전반 (빌드 메뉴, 메뉴 개발, 결산, 직원 관리, 마케팅, 순위 표시, 맵 확장 버튼)
- 튜토리얼 진입 상태 분기 (지금은 완료 상태로 시작)
- 회색 박스 → 실제 스프라이트 교체

---

## 11. 3주차 작업 범위 (UI + 폴리시 중심)

### 11.1 방향성

2주차에 게임 시스템 코드가 데이터 기반으로 완성된 상태. 3주차는 **UI 제작 + 아트 교체 + 폴리시**가 메인. 유니티 에디터에서 시간 많이 걸리는 작업이라 충분한 시간 배정.

### 11.2 UI 작업

- **HUD**: 돈, 만족도, 시간(YYYY-MM HH:00), 현재 손님 수 (이미 일부 구현)
- **빌드 메뉴 UI**: 카테고리별 가구 목록, 가격 표시, 배치 시작 버튼
- **메뉴 개발 UI**: 잠긴/해금된 메뉴 목록, 만족도 소비 해금 버튼
- **직원 관리 UI**: 채용/해고/배정/업그레이드. Cook/Server 분리 표시
- **마케팅 UI**: 마케팅 옵션 목록, 만족도 비용/효과
- **결산 UI**: 월말 정산 화면 (매출/재료비/운영비 요약)
- **순위 UI**: 연말 Top 100 표시, 자기 순위 강조
- **맵 확장 UI**: 다음 확장 단계 미리보기 + 해금 버튼

### 11.3 폴리시

- **아트 교체**: 회색 박스 → 실제 픽셀 스프라이트 (참고 에셋: https://limezu.itch.io/moderninteriors)
- **사운드**: BGM + 효과음 7종 (결제, 음식 완성, 손님 입장/퇴장 등)
- **손맛**: 플로팅 텍스트 (+₩, 만족도), 파티클, 카운트업 애니메이션 (DOTween)

### 11.4 영속화/최종화

- **세이브/로드**: JSON으로 진행도 영속화
  - 돈/만족도/연월/확장 단계
  - 직원 (종류·등급·배정 카운터)
  - 배치된 가구/조리도구/카운터/픽업대 (위치·회전)
  - 해금된 메뉴
- **밸런싱**: 2회 풀 플레이 테스트 후 수치 조정
- **엣지 케이스 방어**: 이상 입력/상태 전이 방어
- **빌드/포트폴리오**: 안드로이드 빌드, 시연 영상, README


---

## 12. 시스템 구성

### 12.1 1주차 (완료)
| 시스템 | 책임 |
|--------|------|
| Grid | 9×12 격자, 시작 4×8, `isActive` + `isReserved` + `CellZone`(Kitchen/Hall) |
| Placement | 가구/조리도구/카운터/픽업대 배치/철거/이동 + zone 체크, `PlaceInitial` 시작 배치 |
| GameInitializer | 시작 시 카운터/픽업대/조리도구/테이블 일괄 배치, `StaffManager.Init()` 트리거 |
| Customer AI | 7상태 P 흐름 FSM, 자리 선점(Init), 만족도 페널티 통합 측정 |
| CookStaff AI | 주방 FSM 독립 클래스 — IDLE → 도구 → PassWindow |
| ServerStaff AI | 홀 FSM 독립 클래스 — IDLE 폴링 (음식 우선 / 주문 응대) |
| Counter Management | 싱글톤 매니저, 자기 등록, Server-카운터 1:1 배정 |
| Seat Management | 싱글톤 매니저, 자기 등록 |
| PassWindow + PassWindowManager | 픽업대 큐(주문/음식), 매니저로 다중 픽업대 대비 |
| Time | 영업 16시간 = 실시간 4분 = 한 달 |
| Money / Satisfaction | 싱글톤, 기본 재화 시스템 (정산은 더미) |
| Camera | 세로 화면 고정, Ortho Size = 6 |

### 12.2 2주차 (코드 중심)
| 시스템 | 상태 | 책임 |
|--------|------|------|
| Menu Data (SO) | ✅ | 이름, 가격, 원가, 조리도구 순서 |
| Tool Data (SO) | ✅ | 도구 종류, 사용 시간 |
| Cook FSM 실구현 | ✅ | 메뉴.tools[] 순회 |
| Order/Food 확장 | ✅ | menu 참조 보유, 실제 가격 흐름 |
| Furniture/Counter/PassWindow SO | ✅ | 데이터화 |
| Map Expansion (코드) | ✅ | `ExpansionStageData` + `ExpansionManager.ActivateCells` |
| Settlement | ✅ | `MoneySystem.SettleMonthly` (재료/운영/일당) |
| Marketing | ✅ | `MarketingData` SO + `MarketingManager`, ln-디미니싱, 개월 단위 |
| Reputation | ✅ | `ReputationSystem` 1년 만족도 누적, 스폰 영향 X |
| Ranking | ✅ | `RankingSystem` 연말 순위, `score = 매출/100 + 평판합` |
| Staff 등급/근속/성장 | ✅ | `Staff` 추상 베이스, 매월 +3% 12개월 캡, `EffectiveX` 프로퍼티 |
| Staff Upgrade | ✅ | 6/12개월 자격, `next.hireCost/2`, 누적치 유지 |
| StaffCandidatePool | ✅ | 모집권(Normal/High/Rare), 2개월 지연, 풀 캡 5 FIFO, 영업일당 1회 |
| Counter-Staff 귀속 제거 | ✅ | claim 시스템, 1:1은 채용 한도로만 |
| Customer 자리 대기 큐 | ✅ | `WAIT_FOR_SEAT` 상태, patience 타임아웃 |
| A* Pathfinding | 미진행 | 영역 zone 필터링 + 가구 회피 |
| Satisfaction 페널티 분리 | 미진행 | 카운터 대기 / 음식 대기 분리 측정 |
| 라이더 시스템 | 미진행 | 이슈 #52 (별도 진행 예정) |
| DT 시스템 | 미진행 | 이슈 #53 (별도 진행 예정) |

### 12.3 3주차 (UI + 폴리시)
| 시스템 | 책임 |
|--------|------|
| HUD | 돈, 만족도, 시간, 손님 수 |
| Build Menu UI | 카테고리/가격/배치 시작 |
| Menu Development UI | 잠긴/해금 메뉴, 해금 버튼 |
| Staff Management UI | 채용/해고/배정/업그레이드 |
| Marketing UI | 옵션/비용/효과 |
| Settlement UI | 월말 정산 요약 |
| Ranking UI | 연말 Top 100 표시 |
| Map Expansion UI | 다음 단계 미리보기 + 해금 |
| Tutorial | 시작 진입 상태 분기 (카운터 1 + 홀 1 + 테이블 1) |
| Art | 회색 박스 → 실제 스프라이트 |
| Audio | BGM + 효과음 7종 |
| Juice/Polish | 플로팅 텍스트, 파티클, 카운트업 |
| Save/Load | JSON 영속화 |
| Balancing | 풀 플레이 테스트 기반 수치 조정 |
| Edge Cases | 예외 방어 |
| Build/Portfolio | 빌드, 영상, README |
| Rider/DT (선택) | 시간 여유 시 |

---

## 13. 핵심 결정 사항 (위반 제안 금지)

1. **2D 카이로소프트풍 경영 시뮬레이션**
2. **그리드 시작 4×8, 최대 9×12** *(변경됨)*
3. **영업시간 8:00 ~ 24:00**
4. **영업 1사이클 = 한 달 = 실시간 4분** (1일 개념 없음)
5. **1년 = 12개월 = 약 1시간**
6. **개발 3주, 솔로, 풀타임, 포트폴리오 목적**
7. **참고 에셋**: https://limezu.itch.io/moderninteriors
8. **카운터-직원 귀속 폐기 / 요리사·홀 채용 한도 = 카운터 수 / 라이더는 별도** *(변경됨, 이슈 #51)*
9. **시작 카운터 1개, 시작 직원 = 요리사 Junior 1 + 홀 Junior 1** *(변경됨, 이슈 #51)*
10. **직원이 직접 이동**해서 조리도구 순회 후 음식 전달 (요리사는 픽업대까지, 홀이 픽업대→자리)
11. **손님은 카운터에서 주문/결제 → 자리 이동 → 자리에서 음식 대기 → 식사 → 퇴장** (Section 4 신구조 P) *(변경됨)*
12. **손님 동작은 1종(매장 손님)으로 통일**, 외형만 여러 종. 배달 손님은 라이더와 함께 별도 시스템.
13. **장기 목표 Top 100 1등**, 1등 이후도 지속 플레이
14. **공간 구조 = 주방(상단) + 홀(하단), 픽업대(PassWindow)로 연결** *(추가)*
15. **확장 3단계: 홀1단(우하 5×5) → 주방1단(좌상 4×4) → 홀2단(우상 5×7) → 최종 9×12** *(추가)*
16. **화면 방향 = 세로 (모바일 Portrait)** *(추가)*
17. **카메라 = 고정** (1주차 기준, 줌/스크롤 없음. 필요 시 추후 추가) *(추가)*
18. **입구/출구 = 홀 좌하단 (0, 7), 배치 불가 셀** *(추가)*
19. **자리 없으면 손님은 WAIT_FOR_SEAT에서 대기, patience 초과 시 만족도 0으로 LEAVE** *(변경됨, 이슈 #51)*
20. **직원 3종 분리: 요리사(Cook) / 홀(Server) / 라이더(Rider)** *(추가)*
    - 1주차: enum/필드만 분리, FSM은 통합
    - 2주차: Cook/Server FSM 분리
    - 3주차 또는 그 이후: Rider 추가 검토
21. **직원 등급 3단계: Junior / Senior / Manager 유지** *(추가)*
22. **DT 시스템 = 최종 확장 보상**. 9×12 완성 이후 해금. 3주차 또는 그 이후 검토 *(추가)*
23. **셀별 활성 플래그 + zone(Kitchen/Hall) 관리** *(추가, 1주차 구현 시 단순화 — Border/Inactive는 필요 시 도입)*
24. **셀별 `isReserved` 플래그 + `HashSet<Vector2Int>`로 시스템 예약 셀(입구, 픽업대 등) 관리** *(추가)*
25. **매니저 싱글톤 패턴 통일** — `CounterManager`, `SeatManager`, `StaffManager`, `PassWindowManager`, `MoneySystem`, `SatisfactionSystem` 모두 `Instance` 정적 참조 *(추가)*
26. **자기 등록 패턴** — Counter/Seat/PassWindow는 Awake에서 해당 매니저에 자기 등록 → 런타임 생성 오브젝트 자동 추적 *(추가)*
27. **`PassWindowManager` 도입** — 다중 픽업대 대비 매니저 패턴, 직원은 매니저 API만 호출. 1주차 단일, 2주차 다중 확장 *(추가)*
28. **`Staff` 추상 베이스 + `CookStaff`/`ServerStaff` 상속** *(변경됨, 이슈 #51)* — 공통 필드(데이터/근속/성장/변동치)와 공통 메서드(`MoveTowards`/`SetData`/`TickMonth`/`CanUpgrade`/`InitBase`)를 베이스에 두고, FSM과 role-specific Effective 프로퍼티만 서브클래스에 보유.
29. **Server는 폴링 + claim 방식** *(변경됨, 이슈 #51)* — IDLE에서 ① PassWindow 음식 픽업 → ② `CounterManager.GetCounterWithUnservedCustomer()`로 미점유 대기 카운터 `TryClaim` → ③ 가장 가까운 빈 staffPos로 이동. 어떤 카운터도 영구 귀속되지 않음.
30. **Server가 IDLE에서 음식 즉시 큐에서 dequeue** — 여러 Server 헛걸음 방지. "멘탈 클레임" 방식 *(추가)*
31. **음식은 owner 없음** — `Food.order.customer` 참조로 운반 대상만 식별. 누구든 가져가서 배달 *(추가)*
32. **`GameInitializer` + `PlacementSystem.PlaceInitial()`** — 시작 배치 일괄 처리. 런타임 등록 순서 보장 (시작 가구 배치 → `StaffManager.Init()`) *(추가)*
33. **손님 자리 확보 = WAIT_FOR_SEAT에서 폴링** *(변경됨, 이슈 #51)* — Init 시점에 자리 없으면 destroy 하지 않고 대기 큐(`CustomerManager._waitingForSeat`) 등록 → 매 프레임 자리 폴링 → patience 초과 시 만족도 0으로 LEAVE.
34. **Unity y-up 좌표계 사용** — 설계 문서의 "좌상단 (0,0)"과 y축이 뒤집힘. 입구 셀은 (0,0), 주방은 y=5~7 (위쪽), 홀은 y=0~4 (아래쪽) *(추가)*
35. **직원 매월 +3% 능력치 성장, 12개월 캡 (총 36%)** *(이슈 #51 추가)* — Cook/Rider는 `speedMultiplier`, Server는 `kindness`에 적용. `moveSpeed`/`salary`는 성장 미적용. 업그레이드해도 누적치 보존.
36. **직원 업그레이드 자격: 누적 6개월 Senior, 12개월 Manager (등급 무관, 채용 시점부터)** *(이슈 #51 추가)* — Junior→Manager 직행 불가, 항상 Senior 경유. 비용 = `next.hireCost / 2`.
37. **변동치 `_hireVariance` 영구 유지** *(이슈 #51 추가)* — 채용 시 ±10% 변동률 결정, `Staff` 인스턴스에 저장. `moveSpeed/kindness/speedMultiplier/salary` 모두 동일 비율로 변동, `hireCost`는 변동 X. 업그레이드해도 유지.
38. **모집 시스템 = 만족도로 모집권 구매, 2개월 지연 후 후보 2명, 풀 캡 5 FIFO** *(이슈 #51 추가)* — 자동 매월 리프레시 폐기, 영업일당 1회만 구매, 만료 없음.
39. **평판 시스템 = 1년 만족도 누적 합** *(이슈 #50 추가)* — 스폰 빈도에 영향 없음. 연말 순위 산출에만 사용. `OnYearEnded`에 리셋.
40. **연말 순위 = `score = AnnualRevenue/100 + AnnualReputation`** *(이슈 #50 추가)* — 임계점(`minQualifyingScore`) 미달 시 순위 미표시. 더미 Top 100과 대조.
41. **마케팅 = 다음 영업일부터 효과, 개월 단위 지속, `ln(1+Σboost)` 디미니싱** *(이슈 #50 추가)* — 절대 감소하지 않고, 많이 할수록 증가폭이 줄어듦.

---

## 14. 미해결 / 사용자 확인 필요 사항 (TBD)

### 14.1 게임 규칙 관련
- 시간 배속 옵션 존재 여부
- 모든 카운터 점유 중일 때 신규 손님 행동 (현재는 카운터 줄 `QueueManager`에 대기, 줄 가득 차면 자리 점유 해제하고 LEAVE)
- 영업 중 가구 배치/철거 가능 여부
- 라이더/DT 3주차 포함 여부 (또는 출시 후로 미룸)
- 픽업대 배치 가능 여부 (현재 1개 고정)
- `CustomerManager.waitingSlots` 개수 (좁은 시작 가게에선 1개로 시작, 확장 시 증설?)

### 14.2 수치 (TBD)
- 시작 자금
- 모든 가구/조리도구/카운터/픽업대/메뉴 가격
- 모든 메뉴 원가, 판매가
- 조리도구별 사용시간
- 손님 식사 시간 (`eatSpeed`)
- 손님 patience 값 (자리 대기 + 카운터 대기 공통 한계)
- 등급별 직원 `moveSpeed`/`kindness`/`speedMultiplier`/`hireCost`/`salary` (9개 SO 모두)
  - 단, 등급 튜닝 규칙: Junior×1.36 < Senior 베이스 < Senior×1.36 < Manager 베이스
- 운영비 셀당 단가 (`MoneySystem.PricePerSquareMeter`)
- 만족도 계산식 세부 (현재: `baseSatisfaction` + 직원 친절도 + 식사 가산 - 대기 페널티)
- 신메뉴 해금에 필요한 만족도
- 모집권 만족도 비용 (현재 제안값 50/150/400)
- 모집 티어별 등급 weight (현재 제안값 Normal 80/18/2, High 40/50/10, Rare 10/55/35)
- 마케팅 캠페인 비용/지속/가중치
- 맵 확장 3단계의 비용, 해금 조건
- DT 해금 조건 및 비용
- `RankingSystem.minQualifyingScore`, `revenueDivisor`, `dummyTop100` 값 (1년 플레이 후 캘리브레이션)
- 손님 외형 종류 개수
- 사운드 효과음 7종 구체적 용도
- 메뉴별 조리도구 순회 순서
- Cook의 조리속도 보너스(`EffectiveSpeedMultiplier`) 실제 적용 위치 (현재 도구 `usingDuration` 고정, 향후 보너스로 단축 예정)

### 14.3 해소된 TBD (참고용)
- ~~시작 자금~~ → 여전히 TBD (`MoneySystem.startingMoney` 인스펙터)
- ~~시작 카운터 제공 여부~~ → **카운터 1개로 변경 (이슈 #51)**
- ~~시작 직원 수~~ → **요리사 Junior 1 + 홀 Junior 1 (이슈 #51)**
- ~~자리가 없을 때 손님 행동~~ → **WAIT_FOR_SEAT 대기, patience 초과 시 LEAVE (이슈 #51)**
- ~~화면 방향~~ → **세로 (Portrait)**
- ~~카메라 처리 방식~~ → **고정 (1주차)**
- ~~입구 위치~~ → **홀 좌하단**
- ~~카운터-직원 배정 방식~~ → **귀속 없음, 풀 + claim (이슈 #51)**
- ~~평판 ↔ 손님 빈도 관계~~ → **연결 없음 (이슈 #50)**
- ~~Top 100 점수 계산~~ → **매출/100 + 평판합, 임계점 초과 시 표시 (이슈 #50)**
- ~~마케팅 효과 모델~~ → **다음 영업일부터, 개월 단위, `ln(1+Σboost)` 디미니싱 (이슈 #50)**
- ~~월말 정산 공식~~ → **재료비 + 운영비(셀×단가) + 직원 EffectiveSalary 합 (이슈 #50)**
- ~~직원 업그레이드 비용~~ → **`next.hireCost / 2` (만족도 비용 없음, 이슈 #51)**
- ~~직원 능력치 성장~~ → **매월 +3%, 12개월 캡, 누적 36%, 업그레이드 시 보존 (이슈 #51)**
- ~~채용 후보 풀 방식~~ → **모집권(Normal/High/Rare) 구매 후 2개월 지연, 풀 캡 5 FIFO (이슈 #51)**

---

## 15. 외부 라이브러리 (권장)

| 라이브러리 | 용도 |
|------------|------|
| A* Pathfinding Project Free | 손님/직원 길찾기 (영역 zone 필터링 적용) |
| DOTween Free | 트윈 애니메이션 |
| TextMeshPro (내장) | 텍스트 |

새 라이브러리는 사용자 동의 후.

---

## 16. AI 어시스턴트 가이드라인

### Do (이렇게 할 것):
- ✅ Section 2~13 (확정 사항)을 최우선 기준으로 삼기
- ✅ Section 14 (TBD)는 사용자에게 질문, 임의 가정 금지
- ✅ Section 13 결정 사항은 변경 제안 금지
- ✅ 데이터는 ScriptableObject 기반 (2주차에 명시)
- ✅ 솔로 초급 기준 답변 (영리한 코드보다 단순 해결책)
- ✅ Week 1~2 단계면 회색 박스 프로토타입 기본값
- ✅ 한글 질문엔 한글 답변
- ✅ 코드는 작은 단위로 쪼개서 설명
- ✅ 직원/공간/확장 관련 답변 시 Section 5/6 신구조 기준

### Don't (이러지 말 것):
- ❌ TBD 값을 임의로 채워넣지 말 것
- ❌ 사용자 명세에 없는 시스템을 "이왕이면" 제안 금지
- ❌ 사용자가 모순된 명세를 줬을 때 한쪽으로 임의 결정 금지 → 질문
- ❌ 200줄 코드 한 번에 던지지 말 것
- ❌ 고급 패턴(ECS, DOTS, Zenject) 제안 금지
- ❌ 단위 테스트 프레임워크 제안 금지
- ❌ 라이브러리 추가 제안 시 사용자 동의 먼저
- ❌ 카운터-직원 귀속 가정 금지 (이슈 #51 이후로 풀 운영)
- ❌ 자리 없을 때 즉시 LEAVE 가정 금지 (이슈 #51 이후로 대기 큐)
- ❌ 통합 직원 클래스 가정 금지 (CookStaff/ServerStaff 분리, `Staff` 베이스)
- ❌ 6×6 / 12×12 그리드 가정 금지 (구버전 명세)
- ❌ 후보 풀 자동 매월 리프레시 가정 금지 (이슈 #51 이후로 모집권 기반)
- ❌ 평판이 손님 빈도에 영향 준다는 가정 금지 (이슈 #50 이후로 분리)
- ❌ 직원 업그레이드에 만족도 비용 가정 금지 (돈만)

### 사용자가 막혔을 때:
1. 먼저 시도한 것 물어보기
2. 최소 유효 수정 제안
3. 며칠차인지 참조해서 스코프 설정
4. 근본 문제면 임시 우회책 + "3주차에 재방문" 플래그

### 사용자가 모순된 요청 시:
1. 모순 지점 명확히 지적
2. 양쪽 선택지 제시
3. 사용자 결정 기다리기
4. **절대 임의 결정 후 진행 금지**

---

## 17. 포트폴리오 어필 포인트

- **시스템 설계**: 그리드(셀별 zone 관리), 손님/직원 다중 FSM(`WAIT_FOR_SEAT` 포함 8상태), 직원 종류 분리(`Staff` 추상 베이스), 직원 풀 운영(carrier-staff 귀속 폐기, claim 시스템), 픽업대 큐, 조리도구 순회, 평판/순위 시스템
- **공간 설계**: 주방-홀 분리, 영역 제한 길찾기, 단계적 확장(3단계 → 9×12)
- **데이터 주도 설계**: ScriptableObject 기반 (메뉴, 직원 등급별 9종 SO, 확장 단계, 마케팅 캠페인까지 데이터화)
- **장기 진행/메타 시스템**: 마케팅(다음 영업일~개월 단위, 로그 디미니싱), 평판(1년 누적), 연말 순위(매출+평판 합산), 직원 근속 성장(매월 +3% 캡 36%), 업그레이드(누적 자격 + 능력치 보존), 모집권 시스템(티어×지연)
- **프로젝트 관리**: 3주 마감 준수, 1주차 빠른 진행 시 확장 명세 적용
- **유니티 숙련도**: 다중 AI 길찾기(영역 필터링 — 2주차 남음), JSON 영속화, UI 아키텍처, 모바일 세로 빌드

---

## 18. 빠른 참조 (확정된 수치만)

| 항목 | 값 |
|------|-----|
| 그리드 최대 | **9×12** |
| 시작 활성 영역 | **4×8 (주방 4×3 + 홀 4×5)** |
| 확장 단계 | **3단계 (홀1단 5×5 → 주방1단 4×4 → 홀2단 5×7)** |
| 화면 방향 | **세로 (Portrait)** |
| 카메라 | **고정, Ortho Size = 6** |
| 입구/출구 좌표 | **(0, 7)** |
| 영업시간 | 8:00 ~ 24:00 (16시간) |
| 영업 1사이클 | 실시간 4분 = 인게임 한 달 |
| 인게임 1년 | 12개월 = 실시간 약 48분 |
| 시작 카운터 수 | 1개 *(변경됨)* |
| 시작 직원 | **요리사 Junior 1 + 홀 Junior 1** *(변경됨)* |
| 직원 종류 | **요리사(Cook) / 홀(Server) / 라이더(Rider)** |
| 직원 등급 | **Junior / Senior / Manager** |
| 직원 채용 한도 | 카운터 수 (Cook/Server 각각) |
| 직원 매월 성장 | +3%, 12개월 캡, 최대 +36% |
| 직원 업그레이드 자격 | 누적 6개월(Senior) / 12개월(Manager) |
| 직원 업그레이드 비용 | `next.hireCost / 2` (돈만, 만족도 X) |
| 채용 후보 풀 캡 | 5명 (FIFO) |
| 모집권 지연 | 2개월 |
| 모집권 구매 빈도 | 영업일당 1회 |
| 손님 FSM 상태 | 8개 (`WAIT_FOR_SEAT` 추가) |
| 자리 대기 타임아웃 | `CustomerData.patience` |
| 픽업대 | 1개 고정 |
| 평판 측정 | 1년 만족도 누적 합 |
| 연말 순위 점수 | `매출/100 + 평판합` |
| 마케팅 모델 | `1 + ln(1+Σboost)`, 다음 영업일 ~ 개월 단위 |
| 효과음 종류 | 7종 |
| 개발 기간 | 3주 (21일) |
| 장기 목표 | Top 100 1등 |
| 최종 확장 보상 | DT 시스템 |

---

> **문서 끝.**
> Section 13(결정 사항)과 Section 14(TBD)를 항상 먼저 확인할 것.
> **TBD 값은 절대 임의로 결정하지 말고 사용자에게 질문할 것.**
> *(변경됨)* 표시는 이전 버전 대비 변경된 항목.
> *(추가)* 표시는 이번 버전에서 새로 추가된 항목.
