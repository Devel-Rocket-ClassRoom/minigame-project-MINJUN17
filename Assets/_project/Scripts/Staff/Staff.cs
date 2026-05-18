using UnityEngine;

public class Staff : MonoBehaviour
{
    private StaffData _data;
    private Counter assignedCounter;   // null = 미배정(대기 중)
    [SerializeField] private int id;
    private StaffState _state;

    [Header("조리")]
    private Transform _toolPos;                  // 더미 (메뉴 시스템에서 단계별 도구로 교체)
    [SerializeField] private float takingOrderDuration = 1f;     // 더미
    [SerializeField] private float usingToolDuration = 2f;       // 더미

    private float _stateTimer;

    public StaffData Data => _data;
    public Counter AssignedCounter => assignedCounter;
    public int Id => id;
    public bool IsAssigned => assignedCounter != null;

    public void Init(StaffData data, int id, Transform toolPos) // toolPos 는 임시
    {
        _data = data;
        this.id = id;
        assignedCounter = null;
        _toolPos = toolPos;
        ChangeState(StaffState.IdleAtCounter);
    }

    public void AssignTo(Counter counter) => assignedCounter = counter;
    public void Unassign() => assignedCounter = null;

    private void Update()
    {
        switch (_state)
        {
            case StaffState.IdleAtCounter:     IdleAtCounterState(); break;
            case StaffState.TakingOrder:       TakingOrderState(); break;
            case StaffState.WalkToTool:        WalkToToolState(); break;
            case StaffState.UsingTool:         UsingToolState(); break;
            case StaffState.DeliverToCustomer: DeliverToCustomerState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    private void ChangeState(StaffState next)
    {
        _state = next;
        _stateTimer = 0f;
    }

    // === 외부 진입점 ===
    public void OnOrderReceived()
    {
        if (_state == StaffState.IdleAtCounter)
            ChangeState(StaffState.TakingOrder);
    }

    // === 상태 핸들러 ===
    private void IdleAtCounterState()
    {
        if (assignedCounter != null)
            MoveTowards(assignedCounter.StaffPos.position);
    }

    private void TakingOrderState()
    {
        if (_stateTimer >= takingOrderDuration)
            ChangeState(StaffState.WalkToTool);
    }

    private void WalkToToolState()
    {
        // TODO: 메뉴 시스템 들어오면 현재 조리 단계의 도구 위치로 변경
        if (MoveTowards(_toolPos.position))
            ChangeState(StaffState.UsingTool);
    }

    private void UsingToolState()
    {
        // TODO: 메뉴 시스템 들어오면 단계별 시간 + 남은 단계 큐 처리
        if (_stateTimer >= usingToolDuration)
            ChangeState(StaffState.DeliverToCustomer);
    }

    private void DeliverToCustomerState()
    {
        if (MoveTowards(assignedCounter.StaffPos.position))
        {
            assignedCounter.OnFoodReady();
            ChangeState(StaffState.IdleAtCounter);
        }
    }

    private bool MoveTowards(Vector3 target) => MoveUtil.MoveTowards(transform, target, _data.moveSpeed);
}
