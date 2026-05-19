using UnityEngine;

public class CookStaff : MonoBehaviour
{
    private StaffData _data;
    [SerializeField] private int id;
    private CookState _state;

    [Header("주방")]
    [SerializeField] private Transform _kitchenIdlePos;  // 주방 대기 위치
    [SerializeField] private Transform _toolPos;         // 더미 (1주차, 메뉴 시스템 전)
    [SerializeField] private float usingToolDuration = 2f;

    private Order _currentOrder;
    private float _stateTimer;

    public StaffData Data => _data;
    public int Id => id;

    public void Init(StaffData data, int id, Transform kitchenIdlePos, Transform toolPos)
    {
        _data = data;
        this.id = id;
        _kitchenIdlePos = kitchenIdlePos;
        _toolPos = toolPos;
        ChangeState(CookState.IDLE_AT_KITCHEN);
    }

    private void Update()
    {
        switch (_state)
        {
            case CookState.IDLE_AT_KITCHEN:     IdleAtKitchenState(); break;
            case CookState.WALK_TO_TOOL:        WalkToToolState(); break;
            case CookState.USING_TOOL:          UsingToolState(); break;
            case CookState.WALK_TO_PASS_WINDOW: WalkToPassWindowState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    private void ChangeState(CookState next)
    {
        _state = next;
        _stateTimer = 0f;
    }

    // === 상태 핸들러 ===
    private void IdleAtKitchenState()
    {
        // 1) PassWindow 폴링 — 주문 있으면 즉시 조리 시작
        if (PassWindowManager.Instance.HasPendingOrder())
        {
            _currentOrder = PassWindowManager.Instance.DequeueOrder();
            ChangeState(CookState.WALK_TO_TOOL);
            return;
        }
        // 2) 주문 없으면 주방 대기 위치로 이동
        MoveTowards(_kitchenIdlePos.position);
    }

    private void WalkToToolState()
    {
        // TODO: 메뉴 시스템 들어오면 단계별 도구 순회
        if (MoveTowards(_toolPos.position))
            ChangeState(CookState.USING_TOOL);
    }

    private void UsingToolState()
    {
        // TODO: 메뉴 시스템 들어오면 단계별 시간 + 남은 단계 큐 처리
        if (_stateTimer >= usingToolDuration)
            ChangeState(CookState.WALK_TO_PASS_WINDOW);
    }

    private void WalkToPassWindowState()
    {
        Transform target = PassWindowManager.Instance.GetFirstPassWindowTransform();
        if (target == null) return;

        if (MoveTowards(target.position))
        {
            Food food = new Food { order = _currentOrder };
            PassWindowManager.Instance.PlaceFood(food);
            _currentOrder = null;
            ChangeState(CookState.IDLE_AT_KITCHEN);
        }
    }

    private bool MoveTowards(Vector3 target) =>
        MoveUtil.MoveTowards(transform, target, _data.moveSpeed);
}
