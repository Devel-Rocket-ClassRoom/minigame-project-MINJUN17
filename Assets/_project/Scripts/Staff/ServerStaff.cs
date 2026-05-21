using System.Collections.Generic;
using UnityEngine;

public class ServerStaff : Staff
{
    private const float kRestBlockRadius = 0.5f;
    private ServerState _state;
    [Header("동작 시간 (더미)")]
    [SerializeField] private float takingOrderDuration = 1f;
    [SerializeField] private float takingDeliveryOrderDuration = 1f;

    private Food _carryingFood;
    private Food _claimedFood;   // 픽업대에 시각적으로 남아있으나 내가 가져갈 거라고 클레임만 한 음식

    private Counter _targetCounter;
    private Phone _targetPhone;
    private Vector3? _currentRestTarget;

    public float EffectiveKindness => _data.kindness * (1f + _hireVariance) * _growthMultiplier;

    public bool IsIdle => _state == ServerState.IDLE_AT_COUNTER;

    public void Init(StaffData data, int id, float hireVariance = 0f)
    {
        InitBase(data, id, hireVariance);
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    protected override PathRole GetPathRole() => PathRole.Server;


    private void Update()
    {
        switch (_state)
        {
            case ServerState.IDLE_AT_COUNTER:        IdleAtCounterState(); break;
            case ServerState.TAKING_ORDER:           TakingOrderState(); break;
            case ServerState.WALK_TO_PASS_WINDOW:    WalkToPassWindowState(); break;
            case ServerState.WALK_TO_SEAT:           WalkToSeatState(); break;
            case ServerState.WALK_TO_PHONE:          WalkToPhoneState(); break;
            case ServerState.TAKING_DELIVERY_ORDER:  TakingDeliveryOrderState(); break;
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
        if (PassWindowManager.Instance.HasReadyHallFood())
        {
            var passWindow = PassWindowManager.Instance.GetFirstPassWindowTransform();
            if (passWindow != null && IsClosestIdleServerTo(passWindow.position))
            {
                _claimedFood = PassWindowManager.Instance.ClaimHallFood(this);
                if (_claimedFood != null)
                {
                    ChangeState(ServerState.WALK_TO_PASS_WINDOW);
                    return;
                }
            }
        }

        // ② 카운터 응대
        var pending = CounterManager.Instance.GetCounterWithUnservedCustomer();
        if (pending != null && IsClosestIdleServerTo(pending.StaffPos.position) && pending.TryClaim(this))
        {
            _targetCounter = pending;
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }

        // ③ 홀 일감 다 비면 ringing 전화 응대 (한 명만 가도록 Claim)
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
            if (c.StaffPos != null) candidates.Add(c.StaffPos.position);

        var occupiers = new List<Vector3>();
        foreach (var s in StaffManager.Instance.ServerStaffs)
            if (s != this) occupiers.Add(s.transform.position);

        return RestSpotPicker.PickClosestFree(
            transform.position, candidates, occupiers, kRestBlockRadius);
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
            menus = customer.OrderedMenus
        };
        PassWindowManager.Instance.SubmitOrder(order);
        customer.OnOrderTaken(this);

        _targetCounter.ReleaseClaim(this);
        _targetCounter = null;
        ChangeState(ServerState.IDLE_AT_COUNTER);
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
            ChangeState(ServerState.WALK_TO_SEAT);
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

        // 음식 놓을 위치 = 의자가 속한 테이블 세트의 FoodDropOff (의자 단독이면 의자 자체)
        Seat seat = customer.AssignedSeat;
        Transform dropOff = seat != null ? seat.FoodDropOff : null;
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
            ChangeState(ServerState.IDLE_AT_COUNTER);
        }
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
}
