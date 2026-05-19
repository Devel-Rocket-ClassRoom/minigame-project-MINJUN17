using UnityEngine;

public class ServerStaff : MonoBehaviour
{
    private StaffData _data;
    [SerializeField] private int id;
    private ServerState _state;

    private Counter _assignedCounter;

    [Header("동작 시간 (더미)")]
    [SerializeField] private float takingOrderDuration = 1f;

    private Food _carryingFood;
    private float _stateTimer;

    public StaffData Data => _data;
    public int Id => id;
    public Counter AssignedCounter => _assignedCounter;
    public bool IsAssigned => _assignedCounter != null;

    public void Init(StaffData data, int id)
    {
        _data = data;
        this.id = id;
        _assignedCounter = null;
        ChangeState(ServerState.IDLE_AT_COUNTER);
    }

    public void AssignTo(Counter counter) => _assignedCounter = counter;
    public void Unassign() => _assignedCounter = null;

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

    // === 상태 핸들러 ===
    private void IdleAtCounterState()
    {
        if (_assignedCounter == null) return;

        // 1) 우선순위: 음식 있으면 즉시 큐에서 빼서 내 것으로 확보 → 픽업대로
        if (PassWindowManager.Instance.HasReadyFood())
        {
            _carryingFood = PassWindowManager.Instance.PickupFood();
            ChangeState(ServerState.WALK_TO_PASS_WINDOW);
            return;
        }

        // 2) 카운터 손님 응대
        if (_assignedCounter.WaitingCustomer != null)
        {
            ChangeState(ServerState.TAKING_ORDER);
            return;
        }

        // 3) 할 일 없으면 카운터로 복귀
        MoveTowards(_assignedCounter.StaffPos.position);
    }

    private void TakingOrderState()
    {
        // 카운터 위치 도착 보장
        if (!MoveTowards(_assignedCounter.StaffPos.position)) return;

        if (_stateTimer < takingOrderDuration) return;

        Customer customer = _assignedCounter.WaitingCustomer;
        if (customer == null)
        {
            ChangeState(ServerState.IDLE_AT_COUNTER);
            return;
        }


        Order order = new Order
        {
            customer = customer,
            menus = customer.OrderedMenus
        };
        PassWindowManager.Instance.SubmitOrder(order);

        customer.OnOrderTaken();

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
            customer.OnFoodDelivered();
            _carryingFood = null;
            ChangeState(ServerState.IDLE_AT_COUNTER);
        }
    }

    private bool MoveTowards(Vector3 target) =>
        MoveUtil.MoveTowards(transform, target, _data.moveSpeed);
}
