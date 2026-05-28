using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(PathMover))]
public class Customer : MonoBehaviour
{
    private CustomerData _data;
    public CustomerData Data => _data;
    public event Action<Customer> OnDespawned;

    private CounterManager _counterManager;
    private SeatManager _seatManager;
    private QueueManager _queueManager;
    private Vector3 _exitPoint;

    private CustomerState _state;
    private Counter _targetCounter;
    private Seat _targetSeat;
    public Seat AssignedSeat => _targetSeat;

    private float _stateTimer;
    private float _waitStartTime;
    private float _spawnTime;
    private int _satisfaction;
    private PathMover _mover;
    private DirectionalCharacterAnimator _dirAnim;
    private Food _servedFood;

    // 사이드워크 ↔ 가게 안 횡단 시 도어 셀 (0,1)을 경유 — 직선이동이 외벽 가로지르는 것 방지
    private bool _passedDoor;
    private static readonly Vector3 DoorWorld = new Vector3(0.5f, 1.5f, 0f);

    // Stair 경유 보조 필드
    private Stair _pendingStair;
    private CustomerState _stairAfterState;
    private bool _leaveInitDone;

    public List<MenuData> OrderedMenus { get; private set; }

    [Header("머리 위 표시")]
    [Tooltip("카운터에서 주문 대기 중일 때 보이는 말풍선 (자식 SpriteRenderer + IndicatorIcon)")]
    [SerializeField] private IndicatorIcon orderBubble;
    [Tooltip("대기 중 랜덤하게 잠깐씩 뜨는 emote (자식 SpriteRenderer + RandomEmote)")]
    [SerializeField] private RandomEmote impatienceEmote;
    [Tooltip("식사 진행도 표시 (자식 SpriteRenderer + CookingProgressDisplay) — 요리사와 같은 8장 시계")]
    [SerializeField] private CookingProgressDisplay eatProgress;

    private void Awake()
    {
        _mover = GetComponent<PathMover>();
        if (_mover != null) _mover.Role = PathRole.Customer;
        _dirAnim = GetComponent<DirectionalCharacterAnimator>();
    }

    /// <summary>특정 월드 지점을 바라보게 한다 (대기/식사 중 방향 고정용). 애니메이터 없으면 무시.</summary>
    private void FaceToward(Vector3 worldPoint)
    {
        if (_dirAnim != null) _dirAnim.FaceTowards(worldPoint - transform.position);
    }

    private void MoveTo(Vector3 destination)
    {
        _mover.SetDestination(destination);
        _mover.Step(_data != null ? _data.moveSpeed : 1f);
    }

    private bool HasArrived() => _mover.HasArrived();

    public void Init(CustomerData data, CounterManager counterManager, SeatManager seatManager, QueueManager queueManager, Vector3 exitPoint)
    {
        _data = data;
        _counterManager = counterManager;
        _seatManager = seatManager;
        _queueManager = queueManager;
        _exitPoint = exitPoint;
        _satisfaction = _data.baseSatisfaction;
        _spawnTime = Time.time;

        int orderCount = Random.Range(_data.minOrderCount, _data.maxOrderCount + 1);
        OrderedMenus = new List<MenuData>();
        for (int i = 0; i < orderCount; i++)
            OrderedMenus.Add(MenuManager.Instance.PickRandomByWeight());

        CustomerManager.Instance.RegisterWaitingForSeat(this);
        ChangeState(CustomerState.WAIT_FOR_SEAT);
    }

    private void Update()
    {
        switch (_state)
        {
            case CustomerState.WAIT_FOR_SEAT:    WaitForSeatState(); break;
            case CustomerState.Enter:            EnterState(); break;
            case CustomerState.WALK_TO_COUNTER:  WalkToCounterState(); break;
            case CustomerState.WAIT_AT_COUNTER:
                if (_targetCounter != null) FaceToward(_targetCounter.transform.position);
                break;
            case CustomerState.WALK_TO_SEAT:     WalkToSeatState(); break;
            case CustomerState.WALK_TO_STAIR:    WalkToStairState(); break;
            case CustomerState.WAIT_AT_SEAT:
                if (_targetSeat != null) FaceToward(_targetSeat.FoodDropOff.position);
                break;
            case CustomerState.EAT:              EatState(); break;
            case CustomerState.LEAVE:            LeaveState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    private void WaitForSeatState()
    {
        if (Time.time - _spawnTime > _data.patience)
        {
            _satisfaction = 0;
            CustomerManager.Instance.UnregisterWaitingForSeat(this);
            ChangeState(CustomerState.LEAVE);
            return;
        }

        var seat = _seatManager.GetFirstAvailableSeat();
        if (seat != null)
        {
            _targetSeat = seat;
            _targetSeat.Occupy();

            if (!_queueManager.TryEnqueue(this))
            {
                _targetSeat.Release();
                _targetSeat = null;
                return;
            }

            CustomerManager.Instance.UnregisterWaitingForSeat(this);
            _waitStartTime = Time.time;
            ChangeState(CustomerState.Enter);
            return;
        }

        MoveTo(CustomerManager.Instance.GetWaitingSlotPosition(this));
    }

    private void ChangeState(CustomerState next)
    {
        _state = next;
        _stateTimer = 0f;
        OnEnterState(next);
    }

    private void OnEnterState(CustomerState s)
    {
        // 대기 중일 때만 랜덤 emote — 외부 사이드워크/좌석에서. 카운터는 OrderBubble과 겹치므로 제외.
        bool isWaiting = s == CustomerState.WAIT_FOR_SEAT || s == CustomerState.WAIT_AT_SEAT;
        if (isWaiting) impatienceEmote?.Begin();
        else impatienceEmote?.End();

        switch (s)
        {
            case CustomerState.WALK_TO_COUNTER:
                _passedDoor = false;   // 사이드워크 → 도어 → 카운터
                break;
            case CustomerState.WAIT_AT_COUNTER:
                _targetCounter.OnCustomerArrived(this);
                orderBubble?.Show();
                break;
            case CustomerState.EAT:
                float waitDuration = _waitStartTime > 0 ? Time.time - _waitStartTime : 0f;
                if (waitDuration > _data.patience)
                    _satisfaction -= Mathf.FloorToInt((waitDuration - _data.patience) * _data.waitPenaltyRate);
                _satisfaction = Mathf.Max(0, _satisfaction);
                eatProgress?.Show();
                break;
            case CustomerState.LEAVE:
                // 가게 안이면(x≥0) 도어 경유, 이미 사이드워크(x<0)면 도어 건너뛰고 바로 출구로
                _passedDoor = transform.position.x < 0f;  // 매번 재계산 (stair 후 재진입 대비)
                if (!_leaveInitDone)
                {
                    _leaveInitDone = true;
                    _targetSeat?.Release();
                    _targetSeat = null;
                    orderBubble?.Hide();   // 주문 안 받은 채 타임아웃 떠나는 경우 대비
                    eatProgress?.Hide();
                    SatisfactionSystem.Instance.Earn(_satisfaction);
                    FloatingTextSystem.SpawnSatisfaction(transform.position, _satisfaction);
                    ReputationSystem.Instance?.Report(_satisfaction);
                }
                break;
        }
    }

    public void OnOrderTaken(ServerStaff server)
    {
        if (_state == CustomerState.WAIT_AT_COUNTER)
        {
            _satisfaction += Mathf.FloorToInt(server.EffectiveKindness);
            int totalPrice = OrderedMenus.Sum(m => m.price);

            foreach (var menu in OrderedMenus)
            {
                SalesTracker.Instance.RecordSale(menu);
            }
            _targetCounter.OnCustomerPaid(totalPrice);
            _targetCounter = null;
            orderBubble?.Hide();
            ChangeState(CustomerState.WALK_TO_SEAT);
        }
    }

    private void EnterState()
    {
        MoveTo(_queueManager.GetSlotPosition(this));

        if (!_queueManager.IsFront(this)) return;

        var ready = _counterManager.GetReadyCounter();
        if (ready == null) return;

        _targetCounter = ready;
        _targetCounter.Reserve();
        _queueManager.Dequeue(this);
        ChangeState(CustomerState.WALK_TO_COUNTER);
    }

    private void WalkToCounterState()
    {
        // 1단계: 사이드워크 → 도어. 2단계: 도어 → 카운터 (그리드 안에서 정상 길찾기)
        Vector3 dest = _passedDoor ? _targetCounter.ServicePos.position : DoorWorld;
        MoveTo(dest);
        if (HasArrived())
        {
            if (!_passedDoor) { _passedDoor = true; return; }
            ChangeState(CustomerState.WAIT_AT_COUNTER);
        }
    }
    private void WalkToSeatState()
    {
        // 좌석이 사라졌으면(가구 철거/preview race) 크래시 대신 퇴장
        if (_targetSeat == null)
        {
            ChangeState(CustomerState.LEAVE);
            return;
        }

        // 좌석이 다른 floor면 stair 경유
        FloorIndex myFloor = GridManager.Instance.GetFloorAt(transform.position);
        FloorIndex seatFloor = GridManager.Instance.GetFloorAt(_targetSeat.transform.position);
        if (myFloor != seatFloor)
        {
            Stair stair = StairManager.Instance?.FindNearestStairOnFloor(myFloor, transform.position);
            if (stair == null || !stair.HasPair)
            {
                Debug.LogWarning($"[Customer] No stair on {myFloor} to reach {seatFloor} seat — leaving");
                _satisfaction = 0;
                ChangeState(CustomerState.LEAVE);
                return;
            }
            _pendingStair = stair;
            _stairAfterState = CustomerState.WALK_TO_SEAT;
            ChangeState(CustomerState.WALK_TO_STAIR);
            return;
        }

        // 앉을 위치 결정:
        //  - Seat에 sitPoint가 지정돼 있으면 그 위치로 정확히 이동 (2인용 등 자리별 지정용)
        //  - 없으면 좌석이 속한 셀 중앙으로 이동 (기존 동작 — 의자 sprite 정렬 오프셋 보정)
        Vector3 target;
        if (_targetSeat.SitPoint != null)
        {
            target = _targetSeat.SitPoint.position;
        }
        else
        {
            Vector2Int seatCell = GridManager.Instance.WorldToCell(_targetSeat.transform.position);
            target = GridManager.Instance.CellToWorld(seatCell);
        }
        MoveTo(target);
        if (HasArrived())
            ChangeState(CustomerState.WAIT_AT_SEAT);
    }

    private void WalkToStairState()
    {
        if (_pendingStair == null || !_pendingStair.HasPair)
        {
            _pendingStair = null;
            ChangeState(_stairAfterState == CustomerState.WALK_TO_SEAT ? CustomerState.LEAVE : _stairAfterState);
            return;
        }

        Vector3 approachPos = _pendingStair.GetApproachPos(PathRole.Customer, transform.position);
        MoveTo(approachPos);
        if (!HasArrived()) return;

        // 텔레포트
        Vector3 landingPos = _pendingStair.GetTeleportLandingPos(PathRole.Customer, transform.position);
        transform.position = landingPos;
        _mover.Clear();

        _pendingStair = null;
        ChangeState(_stairAfterState);
    }

    // 영업 종료 시 강제 퇴장 (대기 상태 손님용)
    public void ForceLeave()
    {
        if (_state == CustomerState.LEAVE) return;
        _satisfaction = 0;
        _queueManager?.Dequeue(this);
        CustomerManager.Instance?.UnregisterWaitingForSeat(this);
        _targetCounter?.OnCustomerPaid(0);
        _targetCounter = null;
        ChangeState(CustomerState.LEAVE);
    }

    public void OnFoodDelivered(ServerStaff server, Food food)
    {
        if (_state == CustomerState.WAIT_AT_SEAT)
        {
            _servedFood = food;
            _satisfaction += Mathf.FloorToInt(server.EffectiveKindness);
            ChangeState(CustomerState.EAT);
        }
    }

    private void EatState()
    {
        if (_targetSeat != null) FaceToward(_targetSeat.FoodDropOff.position);   // 먹는 동안 테이블 바라보기
        _satisfaction += Mathf.FloorToInt(Time.deltaTime * _data.eatGainRate);

        // 식사 시간은 주문 개수에 비례 (eatSpeed = 메뉴 1개 기준 시간)
        int orderCount = OrderedMenus != null ? Mathf.Max(1, OrderedMenus.Count) : 1;
        float totalEatTime = _data.eatSpeed * orderCount;

        eatProgress?.SetProgress(_stateTimer / totalEatTime);

        if (_stateTimer >= totalEatTime)
        {
            if (_servedFood != null)
            {
                Destroy(_servedFood.gameObject);
                _servedFood = null;
            }
            ChangeState(CustomerState.LEAVE);
        }
    }

    private void LeaveState()
    {
        // F2에 있으면 먼저 stair로 F1 복귀
        FloorIndex myFloor = GridManager.Instance.GetFloorAt(transform.position);
        if (myFloor == FloorIndex.Floor2)
        {
            Stair stair = StairManager.Instance?.FindNearestStairOnFloor(myFloor, transform.position);
            if (stair != null && stair.HasPair)
            {
                _pendingStair = stair;
                _stairAfterState = CustomerState.LEAVE;
                ChangeState(CustomerState.WALK_TO_STAIR);
                return;
            }
            // stair 없으면 안전장치: 그 자리에서 사라짐
            Destroy(gameObject);
            return;
        }

        // 가게 안에서 출발하는 경우(주문/식사 후): 도어 → 출구. 이미 밖이면(자리 대기/큐 중 강제 퇴장) 바로 출구.
        Vector3 dest = _passedDoor ? _exitPoint : DoorWorld;
        MoveTo(dest);
        if (HasArrived())
        {
            if (!_passedDoor) { _passedDoor = true; return; }
            Destroy(gameObject);
        }
    }

    private void OnDestroy() => OnDespawned?.Invoke(this);
}
