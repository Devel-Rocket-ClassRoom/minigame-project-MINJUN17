using System.Collections.Generic;
using UnityEngine;

public class CookStaff : MonoBehaviour
{
    private StaffData _data;
    [SerializeField] private int id;
    private CookState _state;

    [Header("주방")]
    [SerializeField] private Transform _kitchenIdlePos;

    private Order _currentOrder;
    private Queue<MenuData> _remainingMenus;
    private Transform _currentToolTransform;   // 이번 사이클에 가야할 도구 위치
    private float _stateTimer;

    public StaffData Data => _data;
    public int Id => id;

    public void Init(StaffData data, int id, Transform kitchenIdlePos)
    {
        _data = data;
        this.id = id;
        _kitchenIdlePos = kitchenIdlePos;
        GetComponent<SpriteRenderer>().sprite = data.sprite;
        ChangeState(CookState.IDLE_AT_KITCHEN);
    }

    private void Update()
    {
        switch (_state)
        {
            case CookState.IDLE_AT_KITCHEN: IdleAtKitchenState(); break;
            case CookState.WALK_TO_TOOL: WalkToToolState(); break;
            case CookState.USING_TOOL: UsingToolState(); break;
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
        // 주문 있으면 큐 통째로 받아서 조리 시작
        if (PassWindowManager.Instance.HasPendingOrder())
        {
            _currentOrder = PassWindowManager.Instance.DequeueOrder();
            _remainingMenus = new Queue<MenuData>(_currentOrder.menus);
            PrepareNextTool();
            return;
        }
        MoveTowards(_kitchenIdlePos.position);
    }

    private void WalkToToolState()
    {
        if (_currentToolTransform == null)
        {
            // 도구가 씬에 없는 경우 방어 (배치 안 됐거나 삭제됨)
            // → 큐 비우고 픽업대로 가서 그냥 끝내거나, IDLE로 복귀
            _remainingMenus.Clear();
            ChangeState(CookState.WALK_TO_PASS_WINDOW);
            return;
        }

        if (MoveTowards(_currentToolTransform.position))
            ChangeState(CookState.USING_TOOL);
    }

    private void UsingToolState()
    {
        MenuData currentMenu = _remainingMenus.Peek();
        if (_stateTimer < currentMenu.tool.usingDuration) return;

        _remainingMenus.Dequeue();

        // 남은 메뉴 있으면 다음 도구로, 없으면 픽업대로
        if (_remainingMenus.Count > 0)
            PrepareNextTool();
        else
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
            _remainingMenus = null;
            _currentToolTransform = null;
            ChangeState(CookState.IDLE_AT_KITCHEN);
        }
    }

    // === 헬퍼 ===
    private void PrepareNextTool()
    {
        MenuData nextMenu = _remainingMenus.Peek();
        _currentToolTransform = CookingToolManager.Instance.GetToolTransform(nextMenu.tool.toolType);
        ChangeState(CookState.WALK_TO_TOOL);
    }

    private bool MoveTowards(Vector3 target) =>
        MoveUtil.MoveTowards(transform, target, _data.moveSpeed);
}