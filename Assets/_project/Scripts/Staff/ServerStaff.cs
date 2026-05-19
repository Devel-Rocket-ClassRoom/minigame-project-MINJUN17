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

    public void Init(StaffData data, int id, float hireVariance = 0f)
    {
        InitBase(data, id, hireVariance);
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

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
        if (PassWindowManager.Instance.HasReadyFood())
        {
            _carryingFood = PassWindowManager.Instance.PickupFood();
            ChangeState(ServerState.WALK_TO_PASS_WINDOW);
            return;
        }

        var pending = CounterManager.Instance.GetCounterWithUnservedCustomer();
        if (pending != null && pending.TryClaim(this))
        {
            _targetCounter = pending;
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }

        _idleHome = PickClosestFreeIdleHome();
        if (_idleHome != null)
            MoveTowards(_idleHome.StaffPos.position);
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

        if (!MoveTowards(_targetCounter.StaffPos.position)) return;
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
        Transform target = PassWindowManager.Instance.GetFirstPassWindowTransform();
        if (target == null) return;

        if (MoveTowards(target.position))
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

        if (MoveTowards(customer.transform.position))
        {
            customer.OnFoodDelivered(this);
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
        }
    }
}
