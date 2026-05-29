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
    private CookingToolInstance _currentTool;

    [Header("음식")]
    [SerializeField] private GameObject foodPrefab;
    private Food _carryingFood;

    [Header("조리 진행도 표시")]
    [Tooltip("머리 위 자식 SpriteRenderer에 부착된 CookingProgressDisplay")]
    [SerializeField] private CookingProgressDisplay progressDisplay;

    public float EffectiveSpeedMultiplier => _data.speedMultiplier * (1f + _hireVariance) * _growthMultiplier;

    public void Init(StaffData data, int id, string nameKey, float hireVariance = 0f)
    {
        InitBase(data, id, nameKey, hireVariance);
        ChangeState(CookState.IDLE_AT_KITCHEN);
    }

    private void Update()
    {
        if (TickCommute()) { _stateTimer += Time.deltaTime; return; }

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

    protected override Vector3 GetWorkPosition()
    {
        Vector3 spot = PickRestSpot();
        if (spot != Vector3.zero) return spot;
        var cells = GridManager.Instance.GetWalkableCellsInZone(CellZone.Kitchen);
        return cells.Count > 0 ? cells[0] : transform.position;
    }

    protected override void OnArrivedAtWork() => ChangeState(CookState.IDLE_AT_KITCHEN);

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
        _dirAnim?.FaceDirection(DirectionalCharacterAnimator.DIR_DOWN);  // 휴식 시 정면 (이동 중엔 LateUpdate가 덮어씀)
    }

    private Vector3 PickRestSpot()
    {
        var candidates = GridManager.Instance.GetWalkableCellsInZone(CellZone.Kitchen);

        // 문 앞에서 쉬지 않도록 문 주변 셀 제외
        Vector3 doorPos = GridManager.Instance.CellToWorld(GridManager.Instance.KitchenDoorCell);
        candidates.RemoveAll(c => Vector3.Distance(c, doorPos) <= 1.5f);

        // 제외했더니 후보가 하나도 안 남으면(주방이 아주 좁을 때) 전체로 복구
        if (candidates.Count == 0)
            candidates = GridManager.Instance.GetWalkableCellsInZone(CellZone.Kitchen);

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
        {
            MenuData currentMenu = _remainingMenus.Peek();
            _currentTool = CookingToolManager.Instance.GetToolInstance(currentMenu.tool.toolType);
            _currentTool?.SetCooking(true);
            progressDisplay?.Show();
            ChangeState(CookState.USING_TOOL);
        }
    }

    private void UsingToolState()
    {
        MenuData currentMenu = _remainingMenus.Peek();

        // 조리 중엔 도구를 바라보게
        var toolT = CookingToolManager.Instance.GetToolTransform(currentMenu.tool.toolType);
        if (toolT != null) FaceToward(toolT.position);

        float effectiveDuration = currentMenu.tool.usingDuration / Mathf.Max(0.01f, EffectiveSpeedMultiplier);
        progressDisplay?.SetProgress(_stateTimer / effectiveDuration);
        if (_stateTimer < effectiveDuration) return;

        _currentTool?.SetCooking(false);   // 이 메뉴 조리 끝 → 도구 애니 정지
        _currentTool = null;
        progressDisplay?.Hide();

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