using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum DTState
{
    DRIVE,                  // waypoint로 이동 중
    WAIT_AT_ORDER,          // OrderStop에서 직원 응대 대기
    WAIT_AT_PICKUP,         // PickupStop에서 음식 대기
}

public class DTCustomer : MonoBehaviour
{
    // Animator Int 파라미터 "Direction" 값 (0=오른쪽, 1=위, 2=왼쪽, 3=아래)
    public const int DIR_RIGHT = 0;
    public const int DIR_UP    = 1;
    public const int DIR_LEFT  = 2;
    public const int DIR_DOWN  = 3;
    private static readonly int kHashDirection = Animator.StringToHash("Direction");
    private static readonly int kHashTurn      = Animator.StringToHash("Turn");

    private DTCustomerData _data;
    public DTCustomerData Data => _data;

    private DTLane _lane;
    private int _currentWaypointIndex;
    public int CurrentWaypointIndex => _currentWaypointIndex;

    private DTState _state;
    public DTState State => _state;

    private List<MenuData> _orderedMenus;
    public List<MenuData> OrderedMenus => _orderedMenus;

    private Food _receivedFood;
    private SpriteRenderer _sr;
    private Animator _animator;
    private int _currentDir = -1;       // -1 = 미지정
    private float _spawnTime;
    private int _satisfaction;

    private DTOrderWindow _orderWindow;
    private DTPickupWindow _pickupWindow;
    public DTPickupWindow PickupWindow => _pickupWindow;
    public DTOrderWindow OrderWindow => _orderWindow;

    public void Init(DTCustomerData data, DTLane lane, DTOrderWindow orderWindow, DTPickupWindow pickupWindow)
    {
        _data = data;
        _lane = lane;
        _orderWindow = orderWindow;
        _pickupWindow = pickupWindow;
        _spawnTime = Time.time;
        _satisfaction = _data.baseSatisfaction;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && _data.carSprite != null) _sr.sprite = _data.carSprite;
        _animator = GetComponent<Animator>();

        // 주문 메뉴 미리 결정 (서버가 응대 시 이 리스트 사용)
        int n = Random.Range(_data.minOrderCount, _data.maxOrderCount + 1);
        _orderedMenus = new List<MenuData>();
        for (int i = 0; i < n; i++)
            _orderedMenus.Add(MenuManager.Instance.PickRandomByWeight());

        // Entry waypoint에 배치 + 첫 segment 방향으로 초기 Direction 세팅 (스폰 시 Turn 트리거 X)
        if (_lane.WaypointCount > 0)
        {
            transform.position = _lane.GetWaypoint(0).position;
            _currentWaypointIndex = 0;

            if (_lane.WaypointCount > 1)
            {
                int initialDir = ComputeDirection(
                    _lane.GetWaypoint(1).position - _lane.GetWaypoint(0).position);
                ApplyDirection(initialDir, fireTurnTrigger: false);
            }
        }

        _lane.RegisterCar(this);
        _state = DTState.DRIVE;
    }

    private void Update()
    {
        // DT 차량은 patience 무한 — 무조건 응대/음식 받을 때까지 대기
        switch (_state)
        {
            case DTState.DRIVE:           DriveState(); break;
            case DTState.WAIT_AT_ORDER:   /* 외부 트리거(OnOrderTaken) 대기 */ break;
            case DTState.WAIT_AT_PICKUP:  PickupCheck(); break;
        }
    }

    private void DriveState()
    {
        Transform target = _lane.GetWaypoint(_currentWaypointIndex);
        if (target == null) return;

        // 현재 waypoint에 아직 도착 안 했으면 이동
        if (Vector3.Distance(transform.position, target.position) > 0.05f)
        {
            SetDirectionFromVector(target.position - transform.position);
            transform.position = Vector3.MoveTowards(
                transform.position, target.position, _data.moveSpeed * Time.deltaTime);
            return;
        }

        // 도착: 현재 waypoint 타입 분기
        if (_lane.IsOrderStop(_currentWaypointIndex))  { EnterOrderStop(); return; }
        if (_lane.IsPickupStop(_currentWaypointIndex)) { EnterPickupStop(); return; }
        if (_lane.IsExit(_currentWaypointIndex))       { Despawn(); return; }

        // 일반 transit: 다음 waypoint 진입 시도. 다음 waypoint 방향으로 미리 Direction 갱신
        int next = _currentWaypointIndex + 1;
        Transform nextWp = _lane.GetWaypoint(next);
        if (nextWp != null) SetDirectionFromVector(nextWp.position - transform.position);
        TryAdvance();
    }

    // ===== 방향 Animator 연동 =====
    private static int ComputeDirection(Vector3 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0 ? DIR_RIGHT : DIR_LEFT;
        return v.y >= 0 ? DIR_UP : DIR_DOWN;
    }

    private void SetDirectionFromVector(Vector3 v)
    {
        if (v.sqrMagnitude < 0.0001f) return;
        ApplyDirection(ComputeDirection(v), fireTurnTrigger: true);
    }

    private void ApplyDirection(int newDir, bool fireTurnTrigger)
    {
        if (newDir == _currentDir) return;
        bool wasUnset = _currentDir < 0;
        _currentDir = newDir;

        if (_animator == null) return;
        _animator.SetInteger(kHashDirection, newDir);
        if (fireTurnTrigger && !wasUnset)
            _animator.SetTrigger(kHashTurn);
    }

    private void TryAdvance()
    {
        int next = _currentWaypointIndex + 1;
        if (next >= _lane.WaypointCount) return;

        // 다음 waypoint를 다른 차가 점유 중이면 그 자리 유지하고 대기
        if (_lane.IsWaypointOccupiedByOther(next, this)) return;

        _currentWaypointIndex = next;
    }

    // ===== OrderStop =====
    private void EnterOrderStop()
    {
        _state = DTState.WAIT_AT_ORDER;
        _orderWindow?.OnCarArrived(this);
    }

    /// <summary>ServerStaff가 응대 완료 시점에 호출 (5단계에서 ServerStaff가 호출).</summary>
    public void OnOrderTaken(ServerStaff server)
    {
        if (_state != DTState.WAIT_AT_ORDER) return;
        _satisfaction += Mathf.FloorToInt(server.EffectiveKindness);
        _orderWindow?.OnCarLeft();
        _state = DTState.DRIVE;
        TryAdvance();
    }

    // ===== PickupStop =====
    private void EnterPickupStop()
    {
        _state = DTState.WAIT_AT_PICKUP;
        _pickupWindow?.OnCarArrived(this);
    }

    private void PickupCheck()
    {
        if (_pickupWindow != null && _pickupWindow.HasReadyFoodFor(this))
        {
            _receivedFood = _pickupWindow.TakeFoodFor(this);
            Destroy(_receivedFood.gameObject);
            //if (_receivedFood != null)
            //{
            //    // 차에 부착: 시각적으로 차가 음식 들고 가는 것처럼
            //    _receivedFood.transform.SetParent(transform, false);
            //    _receivedFood.transform.localPosition = Vector3.zero;
            //}
            _pickupWindow.OnCarLeft();
            _state = DTState.DRIVE;
            TryAdvance();
        }
    }

    // ===== 종료 처리 =====
    private void ForceLeave()
    {
        // patience 초과 시 강제 퇴장 (큐에서 즉시 제거 — 다른 차에 영향 X)
        if (_orderWindow != null && _orderWindow.WaitingCar == this) _orderWindow.OnCarLeft();
        if (_pickupWindow != null && _pickupWindow.WaitingCar == this) _pickupWindow.OnCarLeft();
        _pickupWindow?.ClearFor(this);
        if (_receivedFood != null) Destroy(_receivedFood.gameObject);
        _receivedFood = null;
        Despawn();
    }

    private void Despawn()
    {
        SatisfactionSystem.Instance?.Earn(_satisfaction);
        ReputationSystem.Instance?.Report(_satisfaction);

        _pickupWindow?.ClearFor(this);
        _lane.UnregisterCar(this);
        Destroy(gameObject);
    }
}
