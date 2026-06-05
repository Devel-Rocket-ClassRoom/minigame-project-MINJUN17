using System.Collections.Generic;
using UnityEngine;

public class ServerStaff : Staff
{
    private const float kRestBlockRadius = 0.5f;
    private ServerState _state;
    [Header("동작 시간 (더미)")]
    [SerializeField] private float takingOrderDuration = 1f;
    [SerializeField] private float takingDeliveryOrderDuration = 1f;
    [Tooltip("주문 연속 응대 중 다음 손님이 카운터에 안 오면 이 시간(초) 뒤 다른 일로 전환")]
    [SerializeField] private float nextOrderWaitTimeout = 3f;

    private Food _carryingFood;
    private Food _claimedFood;   // 픽업대에 시각적으로 남아있으나 내가 가져갈 거라고 클레임만 한 음식

    private Counter _targetCounter;
    private Phone _targetPhone;
    private DTOrderWindow _targetDTOrderWindow;
    private Vector3? _currentRestTarget;

    // Stair 경유 보조 필드
    private Stair _pendingStair;
    private ServerState _stairAfterState;

    public float EffectiveKindness => _data.kindness * (1f + _hireVariance) * _growthMultiplier;

    public bool IsIdle => _state == ServerState.IDLE_AT_COUNTER;

    /// <summary>현재 향하고 있는(찜한) 휴식 자리. 다른 서버가 같은 자리를 고르지 않도록 점유 판정에 사용.</summary>
    public Vector3? CurrentRestTarget => _currentRestTarget;

    public void Init(StaffData data, int id, string nameKey, float hireVariance = 0f)
    {
        InitBase(data, id, nameKey, hireVariance);
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    protected override PathRole GetPathRole() => PathRole.Server;

    protected override Vector3 GetWorkPosition()
    {
        Vector3 spot = PickRestSpot();
        return spot != Vector3.zero ? spot : transform.position;
    }

    protected override void OnArrivedAtWork() => ChangeState(ServerState.IDLE_AT_COUNTER);


    private void Update()
    {
        if (TickCommute()) { _stateTimer += Time.deltaTime; return; }

        switch (_state)
        {
            case ServerState.IDLE_AT_COUNTER:        IdleAtCounterState(); break;
            case ServerState.TAKING_ORDER:           TakingOrderState(); break;
            case ServerState.WAIT_FOR_NEXT_ORDER:    WaitForNextOrderState(); break;
            case ServerState.WALK_TO_PASS_WINDOW:    WalkToPassWindowState(); break;
            case ServerState.WALK_TO_SEAT:           WalkToSeatState(); break;
            case ServerState.WALK_TO_STAIR:          WalkToStairState(); break;
            case ServerState.WALK_TO_PHONE:          WalkToPhoneState(); break;
            case ServerState.TAKING_DELIVERY_ORDER:  TakingDeliveryOrderState(); break;
            case ServerState.WALK_TO_DT_ORDER:       WalkToDTOrderState(); break;
            case ServerState.TAKING_DT_ORDER:        TakingDTOrderState(); break;
            case ServerState.WALK_TO_DT_PICKUP:      WalkToDTPickupState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    private void ChangeState(ServerState next)
    {
        _state = next;
        _stateTimer = 0f;
        if (next != ServerState.IDLE_AT_COUNTER) _currentRestTarget = null;
    }

    private void IdleAtCounterState()
    {
        // ① 홀 음식 클레임 (실제 픽업은 픽업대 도착 시점에)
        if (PassWindowManager.Instance.HasReadyFood(OrderType.Hall))
        {
            var passWindow = PassWindowManager.Instance.GetFirstPassWindowTransform();
            if (passWindow != null && IsClosestIdleServerTo(passWindow.position))
            {
                _claimedFood = PassWindowManager.Instance.ClaimFood(OrderType.Hall, this);
                if (_claimedFood != null)
                {
                    ChangeState(ServerState.WALK_TO_PASS_WINDOW);
                    return;
                }
            }
        }

        // ② DT 음식 클레임 (PassWindow에 DT 음식 ready → 픽업창구로 운반)
        if (PassWindowManager.Instance.HasReadyFood(OrderType.DT))
        {
            var passWindow = PassWindowManager.Instance.GetFirstPassWindowTransform();
            if (passWindow != null && IsClosestIdleServerTo(passWindow.position))
            {
                _claimedFood = PassWindowManager.Instance.ClaimFood(OrderType.DT, this);
                if (_claimedFood != null)
                {
                    ChangeState(ServerState.WALK_TO_PASS_WINDOW);
                    return;
                }
            }
        }

        // ③ 카운터 응대
        var pending = CounterManager.Instance.GetCounterWithUnservedCustomer();
        if (pending != null && IsClosestIdleServerTo(pending.StaffPos.position) && pending.TryClaim(this))
        {
            _targetCounter = pending;
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }

        // ④ DT 주문 응대 (차가 OrderWindow에서 대기 중)
        if (DTWindowManager.Instance != null)
        {
            var dtOrder = DTWindowManager.Instance.GetOrderWindowWithUnservedCar();
            if (dtOrder != null && dtOrder.StaffPos != null
                && IsClosestIdleServerTo(dtOrder.StaffPos.position)
                && dtOrder.TryClaim(this))
            {
                _targetDTOrderWindow = dtOrder;
                ChangeState(ServerState.WALK_TO_DT_ORDER);
                return;
            }
        }

        // ⑤ 홀 일감 다 비면 ringing 전화 응대 (한 명만 가도록 Claim)
        if (PhoneManager.Instance != null && PhoneManager.Instance.HasRingingPhone())
        {
            var phone = PhoneManager.Instance.GetRingingPhone();
            if (phone != null
                && !phone.IsClaimedByOther(this)
                && IsClosestIdleServerTo(phone.transform.position)
                && phone.TryClaim(this))
            {
                _targetPhone = phone;
                ChangeState(ServerState.WALK_TO_PHONE);
                return;
            }
        }

        // ④ 빈 카운터 StaffPos 중 가장 가까운 곳으로 이동
        // (한 번 정한 목표는 다른 서버가 차지하기 전까진 유지 — 흔들림 방지)
        if (_currentRestTarget == null
            || IsRestTargetTakenByOther(_currentRestTarget.Value))
        {
            Vector3 picked = PickRestSpot();
            _currentRestTarget = picked != Vector3.zero ? picked : (Vector3?)null;
        }
        if (_currentRestTarget.HasValue) MoveTo(_currentRestTarget.Value);
        // 휴식지 도착 후에만 정면 고정. 이동 중에 호출하면 Animator tick(Update~LateUpdate 사이)이 DIR_DOWN을 읽어 정면으로 걷는 버그
        if (HasArrived())
            _dirAnim?.FaceDirection(DirectionalCharacterAnimator.DIR_DOWN);
    }

    private bool IsRestTargetTakenByOther(Vector3 target)
    {
        // 내가 이미 그 자리에 있으면 점유자는 나 자신 → 양보 안 함
        if (Vector3.Distance(transform.position, target) < kRestBlockRadius) return false;
        foreach (var s in StaffManager.Instance.ServerStaffs)
        {
            if (s == this) continue;
            if (Vector3.Distance(s.transform.position, target) < kRestBlockRadius)
                return true;
        }
        return false;
    }

    private bool IsClosestIdleServerTo(Vector3 target)
    {
        float myDist = Vector3.Distance(transform.position, target);
        foreach (var s in StaffManager.Instance.ServerStaffs)
        {
            if (s == this) continue;
            if (!s.IsIdle) continue;
            float d = Vector3.Distance(s.transform.position, target);
            if (d < myDist) return false;
            if (Mathf.Approximately(d, myDist) && s.Id < this.Id) return false;
        }
        return true;
    }

    private Vector3 PickRestSpot()
    {
        var counters = CounterManager.Instance.Counters;
        var candidates = new List<Vector3>();
        foreach (var c in counters)
        {
            // 카운터에 지정된 휴식 포지션들(보통 3개) 사용, 없으면 staffPos로 폴백
            if (c.RestPositions != null && c.RestPositions.Count > 0)
            {
                foreach (var rp in c.RestPositions)
                    if (rp != null) candidates.Add(rp.position);
            }
            else if (c.StaffPos != null)
            {
                candidates.Add(c.StaffPos.position);
            }
        }

        if (candidates.Count == 0) return Vector3.zero;

        // 다른 서버가 이미 찜했거나(=CurrentRestTarget) 머무는 자리는 점유로 간주.
        // 점유되지 않은 자리 중 내 위치에서 가장 가까운 곳에서 쉼. (자리가 다 차면 가까운 자리로 폴백)
        var occupiers = new List<Vector3>();
        foreach (var s in StaffManager.Instance.ServerStaffs)
        {
            if (s == this) continue;
            occupiers.Add(s.CurrentRestTarget ?? s.transform.position);
        }

        return RestSpotPicker.PickClosestFree(transform.position, candidates, occupiers, kRestBlockRadius);
    }

    private void TakingOrderState()
    {
        if (_targetCounter == null)
        {
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }
        MoveTo(_targetCounter.StaffPos.position);
        if (!HasArrived()) return;
        FaceToward(_targetCounter.transform.position);   // 카운터 바라보기
        if (_stateTimer < takingOrderDuration) return;

        Customer customer = _targetCounter.WaitingCustomer;
        if (customer == null)
        {
            _targetCounter.ReleaseClaim(this);
            _targetCounter = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }

        Order order = new Order
        {
            customer = customer,
            menus = customer.OrderedMenus,
            type = OrderType.Hall,
        };
        PassWindowManager.Instance.SubmitOrder(order);
        customer.OnOrderTaken(this);

        _targetCounter.ReleaseClaim(this);
        _targetCounter = null;

        // 한 번 주문 받으러 왔으면 줄이 빌 때까지 연달아 받음 (음식 운반은 그 다음)
        var nextCounter = CounterManager.Instance.GetCounterWithUnservedCustomer();
        if (nextCounter != null && nextCounter.TryClaim(this))
        {
            _targetCounter = nextCounter;
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }
        // 줄에 손님이 카운터로 오는 중 → 잠깐 기다렸다 마저 받기
        if (HasPendingHallCustomers())
        {
            ChangeState(ServerState.WAIT_FOR_NEXT_ORDER);
            return;
        }
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    // 줄(큐) 또는 카운터에 아직 응대할 홀 손님이 남아있는지
    private bool HasPendingHallCustomers()
    {
        if (QueueManager.Instance != null && QueueManager.Instance.Count > 0) return true;
        return CounterManager.Instance.GetCounterWithUnservedCustomer() != null;
    }

    private void WaitForNextOrderState()
    {
        // 줄·카운터 모두 비면 일반 IDLE로 (음식 운반 등 다른 일)
        if (!HasPendingHallCustomers()) { ChangeState(ServerState.IDLE_AT_COUNTER); return; }

        // 카운터에 손님이 도착하면 바로 받기
        var c = CounterManager.Instance.GetCounterWithUnservedCustomer();
        if (c != null && c.TryClaim(this))
        {
            _targetCounter = c;
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }

        // 손님이 카운터로 걸어오는 동안 대기(정면). 너무 오래 못 받으면 빠져서 다른 일.
        _dirAnim?.FaceDirection(DirectionalCharacterAnimator.DIR_DOWN);
        if (_stateTimer >= nextOrderWaitTimeout) ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    private void WalkToPassWindowState()
    {
        Vector3 target = PassWindowManager.Instance.GetApproachPosition(PathRole.Server, transform.position);
        if (target == Vector3.zero) return;

        MoveTo(target);
        if (HasArrived())
        {
            // 픽업대 도착: 클레임한 음식 실제로 들기 (SetParent 자기 자신으로)
            if (_claimedFood != null)
            {
                _carryingFood = PassWindowManager.Instance.TakeFood(_claimedFood);
                _claimedFood = null;
                if (_carryingFood != null) AttachFood(_carryingFood);
            }

            // 음식 종류에 따라 다음 상태 분기
            var nextType = _carryingFood?.order?.type ?? OrderType.Hall;
            ChangeState(nextType == OrderType.DT
                ? ServerState.WALK_TO_DT_PICKUP
                : ServerState.WALK_TO_SEAT);
        }
    }

    private void WalkToSeatState()
    {
        Customer customer = _carryingFood?.order?.customer;
        if (customer == null)
        {
            if (_carryingFood != null) Destroy(_carryingFood.gameObject);
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }

        Seat seat = customer.AssignedSeat;
        if (seat == null)
        {
            if (_carryingFood != null) Destroy(_carryingFood.gameObject);
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }

        // 좌석 floor가 다르면 먼저 stair 경유
        FloorIndex myFloor = GridManager.Instance.GetFloorAt(transform.position);
        FloorIndex seatFloor = GridManager.Instance.GetFloorAt(seat.transform.position);
        if (myFloor != seatFloor)
        {
            Stair stair = StairManager.Instance?.FindNearestStairOnFloor(myFloor, transform.position);
            if (stair == null || !stair.HasPair)
            {
                Debug.LogWarning($"[ServerStaff] No stair on {myFloor} to reach {seatFloor} seat — discarding food");
                if (_carryingFood != null) Destroy(_carryingFood.gameObject);
                _carryingFood = null;
                ChangeState(ServerState.IDLE_AT_COUNTER);
                return;
            }
            _pendingStair = stair;
            _stairAfterState = ServerState.WALK_TO_SEAT;
            ChangeState(ServerState.WALK_TO_STAIR);
            return;
        }

        // 음식 놓을 위치 = 의자가 속한 테이블 세트의 FoodDropOff (의자 단독이면 의자 자체)
        Transform dropOff = seat.FoodDropOff;
        Vector3 target = dropOff != null
            ? GridManager.Instance.GetFurnitureApproachPosition(dropOff.position, PathRole.Server, transform.position)
            : customer.transform.position;   // 좌석 정보 없으면 fallback

        MoveTo(target);
        if (HasArrived())
        {
            // 음식 테이블 위에 놓기
            if (_carryingFood != null && dropOff != null)
            {
                _carryingFood.transform.SetParent(dropOff, false);
                _carryingFood.transform.localPosition = Vector3.zero;
            }
            customer.OnFoodDelivered(this, _carryingFood);
            _carryingFood = null;

            // 배달 끝나고 F2에 있으면 stair로 F1 복귀
            FloorIndex afterFloor = GridManager.Instance.GetFloorAt(transform.position);
            if (afterFloor == FloorIndex.Floor2)
            {
                Stair backStair = StairManager.Instance?.FindNearestStairOnFloor(afterFloor, transform.position);
                if (backStair != null && backStair.HasPair)
                {
                    _pendingStair = backStair;
                    _stairAfterState = ServerState.IDLE_AT_COUNTER;
                    ChangeState(ServerState.WALK_TO_STAIR);
                    return;
                }
            }
            ChangeState(ServerState.IDLE_AT_COUNTER);
        }
    }

    private void WalkToStairState()
    {
        if (_pendingStair == null || !_pendingStair.HasPair)
        {
            _pendingStair = null;
            ChangeState(_stairAfterState);
            return;
        }

        Vector3 approachPos = _pendingStair.GetApproachPos(PathRole.Server, transform.position);
        MoveTo(approachPos);
        if (!HasArrived()) return;

        // 텔레포트
        Vector3 landingPos = _pendingStair.GetTeleportLandingPos(PathRole.Server, transform.position);
        transform.position = landingPos;
        _mover.Clear();

        _pendingStair = null;
        ChangeState(_stairAfterState);
    }

    private void WalkToPhoneState()
    {
        // ring 종료(타임아웃/타직원 처리)되면 복귀
        if (_targetPhone == null || !_targetPhone.IsRinging)
        {
            _targetPhone?.ReleaseClaim(this);
            _targetPhone = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }
        Vector3 target = PhoneManager.Instance.GetPhoneApproachPosition(PathRole.Server, transform.position);
        if (target == Vector3.zero) return;

        MoveTo(target);
        if (HasArrived())
            ChangeState(ServerState.TAKING_DELIVERY_ORDER);
    }

    private void TakingDeliveryOrderState()
    {
        if (_targetPhone == null || !_targetPhone.IsRinging)
        {
            _targetPhone?.ReleaseClaim(this);
            _targetPhone = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }
        if (_stateTimer < takingDeliveryOrderDuration) return;

        PhoneManager.Instance.AcceptCall(_targetPhone);   // 내부에서 StopRinging → claimer null로 정리
        _targetPhone = null;
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    // ===== DT 응대 =====
    private void WalkToDTOrderState()
    {
        if (_targetDTOrderWindow == null || _targetDTOrderWindow.StaffPos == null)
        {
            _targetDTOrderWindow?.ReleaseClaim(this);
            _targetDTOrderWindow = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }
        MoveTo(_targetDTOrderWindow.StaffPos.position);
        if (HasArrived()) ChangeState(ServerState.TAKING_DT_ORDER);
    }

    private void TakingDTOrderState()
    {
        if (_targetDTOrderWindow == null)
        {
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }
        FaceToward(_targetDTOrderWindow.transform.position);   // DT 창구 바라보기
        if (_stateTimer < takingOrderDuration) return;

        DTCustomer car = _targetDTOrderWindow.WaitingCar;
        if (car == null)
        {
            _targetDTOrderWindow.ReleaseClaim(this);
            _targetDTOrderWindow = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }

        // 매출/판매 기록 (DT는 주문 시점에 결제 — 전화 배달과 동일 패턴)
        int totalPrice = 0;
        foreach (var menu in car.OrderedMenus)
        {
            totalPrice += menu.price;
            SalesTracker.Instance?.RecordSale(menu);
        }
        MoneySystem.Instance?.Earn(totalPrice);
        FloatingTextSystem.SpawnMoney(car.transform.position, totalPrice);

        var order = new Order
        {
            customer = null,
            dtCustomer = car,
            menus = new List<MenuData>(car.OrderedMenus),
            type = OrderType.DT,
        };
        PassWindowManager.Instance.SubmitOrder(order);

        car.OnOrderTaken(this);

        _targetDTOrderWindow.ReleaseClaim(this);
        _targetDTOrderWindow = null;
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    private void WalkToDTPickupState()
    {
        DTCustomer car = _carryingFood?.order?.dtCustomer;
        DTPickupWindow pw = car != null ? car.PickupWindow : null;
        if (pw == null && DTWindowManager.Instance != null)
            pw = DTWindowManager.Instance.FirstPickupWindow;

        if (pw == null || pw.StaffPos == null)
        {
            // 픽업창구 사라짐 → 음식 폐기
            if (_carryingFood != null) Destroy(_carryingFood.gameObject);
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }

        Vector3 target = GridManager.Instance.GetFurnitureApproachPosition(
            pw.StaffPos.position, PathRole.Server, transform.position);
        if (target == Vector3.zero) target = pw.StaffPos.position;

        MoveTo(target);
        if (HasArrived())
        {
            if (_carryingFood != null) pw.PlaceFood(_carryingFood);
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
        }
    }
}
