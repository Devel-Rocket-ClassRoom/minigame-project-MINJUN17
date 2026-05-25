using System.Collections.Generic;
using UnityEngine;

public class CookStaff : Staff
{
    private const float kRestBlockRadius = 0.5f;

    private CookState _state;
    private Order _currentOrder;
    private Queue<MenuData> _remainingMenus;
    private Vector3? _currentToolTargetPos;
    private Vector3? _currentRestTarget;

    [Header("음식")]
    [SerializeField] private GameObject foodPrefab;
    private Food _carryingFood;

    public float EffectiveSpeedMultiplier => _data.speedMultiplier * (1f + _hireVariance) * _growthMultiplier;

    public void Init(StaffData data, int id, string nameKey, float hireVariance = 0f)
    {
        InitBase(data, id, nameKey, hireVariance);
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
        if (next != CookState.IDLE_AT_KITCHEN) _currentRestTarget = null;
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

        // 한 번 정한 휴식지는 다른 Cook이 차지하기 전까진 유지 (흔들림 방지)
        if (_currentRestTarget == null
            || IsRestTargetTakenByOther(_currentRestTarget.Value))
        {
            Vector3 picked = PickRestSpot();
            _currentRestTarget = picked != Vector3.zero ? picked : (Vector3?)null;
        }
        if (_currentRestTarget.HasValue) MoveTo(_currentRestTarget.Value);
    }

    private Vector3 PickRestSpot()
    {
        var candidates = GridManager.Instance.GetWalkableCellsInZone(CellZone.Kitchen);
        var occupiers = new List<Vector3>();
        foreach (var c in StaffManager.Instance.CookStaffs)
            if (c != this) occupiers.Add(c.transform.position);
        return RestSpotPicker.PickClosestFree(
            transform.position, candidates, occupiers, kRestBlockRadius);
    }

    private bool IsRestTargetTakenByOther(Vector3 target)
    {
        if (Vector3.Distance(transform.position, target) < kRestBlockRadius) return false;
        foreach (var c in StaffManager.Instance.CookStaffs)
        {
            if (c == this) continue;
            if (Vector3.Distance(c.transform.position, target) < kRestBlockRadius)
                return true;
        }
        return false;
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
        {
            PrepareNextTool();
        }
        else
        {
            // 조리 완료: 음식 생성 후 요리사가 들고 픽업대로
            if (foodPrefab != null)
            {
                _carryingFood = Instantiate(foodPrefab).GetComponent<Food>();
                _carryingFood.SetUp(_currentOrder);
                AttachFood(_carryingFood);   // Staff 베이스 헬퍼
            }
            ChangeState(CookState.WALK_TO_PASS_WINDOW);
        }
    }

    private void WalkToPassWindowState()
    {
        Vector3 target = PassWindowManager.Instance.GetApproachPosition(PathRole.Cook, transform.position);
        if (target == Vector3.zero) return;
        MoveTo(target);
        if (HasArrived())
        {
            if (_carryingFood != null)
            {
                // PassWindow.PlaceFood가 SetParent + 슬롯 정렬 처리
                PassWindowManager.Instance.PlaceFood(_carryingFood);
                _carryingFood = null;
            }
            _currentOrder = null;
            _remainingMenus = null;
            _currentToolTargetPos = null;
            ChangeState(CookState.IDLE_AT_KITCHEN);
        }
    }

    private void PrepareNextTool()
    {
        MenuData nextMenu = _remainingMenus.Peek();
        Vector3 approachPos = CookingToolManager.Instance.GetToolApproachPosition(nextMenu.tool.toolType, transform.position);
        _currentToolTargetPos = approachPos != Vector3.zero ? approachPos : (Vector3?)null;
        ChangeState(CookState.WALK_TO_TOOL);
    }
}