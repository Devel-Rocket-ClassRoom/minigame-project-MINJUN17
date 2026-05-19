using System.Collections.Generic;
using UnityEngine;

public class CookStaff : Staff
{
    private CookState _state;

    [Header("주방")]
    [SerializeField] private Transform _kitchenIdlePos;

    private Order _currentOrder;
    private Queue<MenuData> _remainingMenus;
    private Transform _currentToolTransform;

    public float EffectiveSpeedMultiplier => _data.speedMultiplier * (1f + _hireVariance) * _growthMultiplier;

    public void Init(StaffData data, int id, Transform kitchenIdlePos, float hireVariance = 0f)
    {
        InitBase(data, id, hireVariance);
        _kitchenIdlePos = kitchenIdlePos;
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

    private void IdleAtKitchenState()
    {
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

    private void PrepareNextTool()
    {
        MenuData nextMenu = _remainingMenus.Peek();
        _currentToolTransform = CookingToolManager.Instance.GetToolTransform(nextMenu.tool.toolType);
        ChangeState(CookState.WALK_TO_TOOL);
    }
}
