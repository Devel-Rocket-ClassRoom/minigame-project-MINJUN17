using UnityEngine;

public class ServerStaff : Staff
{
    private ServerState _state;

    [Header("동작 시간 (더미)")]
    [SerializeField] private float takingOrderDuration = 1f;

    private Food _carryingFood;

    private Counter _targetCounter;   // 응대 중인 카운터(claim 상태)
    private Counter _idleHome;        // idle 시 머무를 staffPos 카운터

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
            case ServerState.IDLE_AT_COUNTER:     IdleAtCounterState(); break;
            case ServerState.TAKING_ORDER:        TakingOrderState(); break;
            case ServerState.WALK_TO_PASS_WINDOW: WalkToPassWindowState(); break;
            case ServerState.WALK_TO_SEAT:        WalkToSeatState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    private void ChangeState(ServerState next)
    {
        _state = next;
        _stateTimer = 0f;
    }

    private void IdleAtCounterState()
    {
        // ① 픽업대에 음식 있으면 가장 가까운 idle 서버가 픽업
        if (PassWindowManager.Instance.HasReadyFood())
        {
            var passWindow = PassWindowManager.Instance.GetFirstPassWindowTransform();
            if (passWindow != null && IsClosestIdleServerTo(passWindow.position))
            {
                _carryingFood = PassWindowManager.Instance.PickupFood();
                ChangeState(ServerState.WALK_TO_PASS_WINDOW);
                return;
            }
        }

        // ② 대기 손님 있는 카운터 응대 — 가장 가까운 idle 서버가 우선
        var pending = CounterManager.Instance.GetCounterWithUnservedCustomer();
        if (pending != null && IsClosestIdleServerTo(pending.StaffPos.position) && pending.TryClaim(this))
        {
            _targetCounter = pending;
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }

        // ③ idle home으로 이동
        _idleHome = PickClosestFreeIdleHome();
        if (_idleHome != null)
        {
            MoveTo(_idleHome.StaffPos.position);
        }
    }

    // 나(this)가 idle 서버들 중 target에 가장 가까운지 (동률은 Id 낮은 쪽 우선)
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

    private Counter PickClosestFreeIdleHome()
    {
        Counter best = null;
        float bestDist = float.MaxValue;
        var counters = CounterManager.Instance.Counters;
        var servers = StaffManager.Instance.ServerStaffs;

        foreach (var c in counters)
        {
            bool taken = false;
            foreach (var s in servers)
            {
                if (s == this) continue;
                if (s._idleHome == c) { taken = true; break; }
            }
            if (taken) continue;

            float d = Vector3.Distance(transform.position, c.StaffPos.position);
            if (d < bestDist) { bestDist = d; best = c; }
        }

        if (best == null && counters.Count > 0) best = counters[0];
        return best;
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
        Vector3 target = PassWindowManager.Instance.GetApproachPosition(PathRole.Server);
        if (target == Vector3.zero) return;

        MoveTo(target);
        if (HasArrived())
            ChangeState(ServerState.WALK_TO_SEAT);
    }

    private void WalkToSeatState()
    {
        Customer customer = _carryingFood?.order?.customer;
        if (customer == null)
        {
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }

        MoveTo(customer.transform.position);
        if (HasArrived())
        {
            customer.OnFoodDelivered(this);
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
        }
    }
}
