using System.Collections.Generic;
using UnityEngine;

public class CookStaff : Staff
{
    private CookState _state;

    [Header("주방")]
    [SerializeField] private Transform _kitchenIdlePos;

    private Order _currentOrder;
    private Queue<MenuData> _remainingMenus;
    private Vector3? _currentToolTargetPos;

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

    protected override PathRole GetPathRole() => PathRole.Cook;

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
        MoveTo(_kitchenIdlePos.position);
    }

    private void WalkToToolState()
    {
        if (_currentToolTargetPos == null)
        {
            _remainingMenus.Clear();
            ChangeState(CookState.WALK_TO_PASS_WINDOW);
            return;
        }
        MoveTo(_currentToolTargetPos.Value);
        if (HasArrived())
            ChangeState(CookState.USING_TOOL);
    }

    private void UsingToolState()
    {
        MenuData currentMenu = _remainingMenus.Peek();
        float effectiveDuration = currentMenu.tool.usingDuration / Mathf.Max(0.01f, EffectiveSpeedMultiplier);
        if (_stateTimer < effectiveDuration) return;

        _remainingMenus.Dequeue();

        if (_remainingMenus.Count > 0)
            PrepareNextTool();
        else
            ChangeState(CookState.WALK_TO_PASS_WINDOW);
    }

    private void WalkToPassWindowState()
    {
        Vector3 target = PassWindowManager.Instance.GetApproachPosition(PathRole.Cook);
        if (target == Vector3.zero) return;
        MoveTo(target);
        if (HasArrived())
        {
            Food food = new Food { order = _currentOrder };
            PassWindowManager.Instance.PlaceFood(food);
            _currentOrder = null;
            _remainingMenus = null;
            _currentToolTargetPos = null;
            ChangeState(CookState.IDLE_AT_KITCHEN);
        }
    }

    private void PrepareNextTool()
    {
        MenuData nextMenu = _remainingMenus.Peek();
        Vector3 approachPos = CookingToolManager.Instance.GetToolApproachPosition(nextMenu.tool.toolType);
        _currentToolTargetPos = approachPos != Vector3.zero ? approachPos : (Vector3?)null;
        ChangeState(CookState.WALK_TO_TOOL);
    }
}
