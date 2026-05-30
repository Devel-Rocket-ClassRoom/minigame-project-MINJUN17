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
    public CustomerState State => _state;
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

    // 만들 수 있는(도구 설치된) 메뉴가 0개라 주문 못 하는 손님 — 계산대만 찍고 퇴장
    private bool _noServeableMenu;
    private const float kNoOrderCounterDwell = 1.2f;   // 카운터에서 잠깐 머무는 시간

    // 총 대기가 patience의 이 배수를 넘으면 포기하고 퇴장 (무한대기 안전망).
    // patience 자체는 "만족도 차감 시작점"이라, 그 N배를 "완전 포기" 임계로 재활용.
    private const float kGiveUpPatienceMult = 2.5f;

    // 화장실 사이클
    public enum ToiletPhase { None, Stall, Sink }
    private ToiletPhase _toiletPhase = ToiletPhase.None;
    public ToiletPhase Phase => _toiletPhase;
    private Toilet _targetToilet;
    private Sink _targetSink;
    public Toilet TargetToilet => _targetToilet;
    public Sink TargetSink => _targetSink;
    private bool _toiletDecided;

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

        int wallet = _data.wallet;
        OrderedMenus = new List<MenuData>();

        // 선호 메뉴도 "만들 수 있을 때만" 주문 (도구 미설치면 스킵)
        if (_data.preferredMenu != null && MenuManager.Instance.IsMakeable(_data.preferredMenu))
        {
            OrderedMenus.Add(_data.preferredMenu);
            wallet -= _data.preferredMenu.price;
        }

        MenuData extra;
        while (Random.value < 0.5f && (extra = PickAffordable(wallet)) != null)
        {
            OrderedMenus.Add(extra);
            wallet -= extra.price;
        }

        if (OrderedMenus.Count == 0)
        {
            var fallback = MenuManager.Instance.PickRandomByWeight();   // 만들 수 있는 메뉴 중 픽 (없으면 null)
            if (fallback != null) OrderedMenus.Add(fallback);
        }

        // 만들 수 있는 메뉴가 하나도 없음 → 자리 안 잡고 줄 서서 계산대만 찍고 퇴장
        if (OrderedMenus.Count == 0)
        {
            _noServeableMenu = true;
            if (!_queueManager.TryEnqueue(this))
            {
                ChangeState(CustomerState.LEAVE);   // 줄이 꽉 차면 그냥 떠남
                return;
            }
            ChangeState(CustomerState.Enter);
            return;
        }

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
                // 주문할 게 없는 손님: 잠깐 머물다 카운터 비우고 퇴장 (중립)
                if (_noServeableMenu && _stateTimer >= kNoOrderCounterDwell)
                {
                    _targetCounter?.OnCustomerPaid(0);   // Enter에서 Reserve된 카운터 해제
                    _targetCounter = null;
                    ChangeState(CustomerState.LEAVE);
                }
                // 참을성 한계(patience×배수) 넘게 주문 접수가 안 되면 포기하고 퇴장 (불만족)
                else if (!_noServeableMenu && GaveUpWaiting())
                {
                    _satisfaction = 0;
                    _targetCounter?.OnCustomerPaid(0);
                    _targetCounter = null;
                    ChangeState(CustomerState.LEAVE);
                }
                break;
            case CustomerState.WALK_TO_SEAT:     WalkToSeatState(); break;
            case CustomerState.WALK_TO_STAIR:    WalkToStairState(); break;
            case CustomerState.WAIT_AT_SEAT:
                if (_servedFood != null) { ChangeState(CustomerState.EAT); break; }
                // 음식이 참을성 한계(patience×배수)를 넘게 안 나오면 포기하고 퇴장 (무한대기 안전망)
                if (GaveUpWaiting())
                {
                    _satisfaction = 0;
                    ChangeState(CustomerState.LEAVE);
                    break;
                }
                if (_targetSeat != null) FaceToward(_targetSeat.FoodDropOff.position);
                break;
            case CustomerState.EAT:              EatState(); break;
            case CustomerState.WALK_TO_TOILET:   WalkToToiletState(); break;
            case CustomerState.USING_TOILET:     UsingToiletState(); break;
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
                // 주문할 게 없는 손님은 서버를 부르지 않음(OnCustomerArrived 생략) → Update에서 곧 퇴장
                if (!_noServeableMenu)
                {
                    _targetCounter.OnCustomerArrived(this);
                    orderBubble?.Show();
                }
                break;
            case CustomerState.EAT:
                float waitDuration = _waitStartTime > 0 ? Time.time - _waitStartTime : 0f;
                if (waitDuration > _data.patience)
                    _satisfaction -= Mathf.FloorToInt((waitDuration - _data.patience) * _data.waitPenaltyRate);
                _satisfaction = Mathf.Max(0, _satisfaction);
                eatProgress?.Show();
                break;
            case CustomerState.USING_TOILET:
                // 실제 도착해서 사용 시작 — 가구 애니메이션 트리거 (예약 시점이 아니라 도착 시점)
                if (_toiletPhase == ToiletPhase.Stall) _targetToilet?.StartUse();
                else if (_toiletPhase == ToiletPhase.Sink) _targetSink?.StartUse();
                break;
            case CustomerState.LEAVE:
                // 가게 안이면(x≥0) 도어 경유, 이미 사이드워크(x<0)면 도어 건너뛰고 바로 출구로
                _passedDoor = transform.position.x < 0f;  // 매번 재계산 (stair 후 재진입 대비)
                if (!_leaveInitDone)
                {
                    _leaveInitDone = true;
                    _targetSeat?.Release();
                    _targetSeat = null;
                    _targetToilet?.Release();
                    _targetToilet = null;
                    _targetSink?.Release();
                    _targetSink = null;
                    orderBubble?.Hide();   // 주문 안 받은 채 타임아웃 떠나는 경우 대비
                    eatProgress?.Hide();
                    // 만들 수 있는 메뉴가 없어 그냥 돌아가는 손님은 만족도/평판에 반영하지 않음(중립)
                    if (!_noServeableMenu)
                    {
                        SatisfactionSystem.Instance.Earn(_satisfaction);
                        FloatingTextSystem.SpawnSatisfaction(transform.position, _satisfaction);
                        ReputationSystem.Instance?.Report(_satisfaction);
                    }
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
            if (!_passedDoor)
            {
                _passedDoor = true;
                // 문 통과 = 가게 진입. 새로 해금된 손님이면 이 시점에 "새 손님 등장!" 팝업.
                CustomerManager.Instance?.TryIntroduceNewCustomer(_data);
                return;
            }
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
        _servedFood = food;
        _satisfaction += Mathf.FloorToInt(server.EffectiveKindness);
        if (_state == CustomerState.WAIT_AT_SEAT)
            ChangeState(CustomerState.EAT);
        // 아직 좌석 도착 전이면 _servedFood만 저장 → WAIT_AT_SEAT 진입 시 자동 EAT
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
            eatProgress?.Hide();
            DecideAfterEat();
        }
    }

    private void DecideAfterEat()
    {
        if (_toiletDecided) { ChangeState(CustomerState.LEAVE); return; }
        _toiletDecided = true;

        // 의지 없으면 그냥 떠남
        if (Random.value > _data.toiletUrgeProbability)
        {
            ChangeState(CustomerState.LEAVE);
            return;
        }

        // 변기 시설 자체가 없으면 LEAVE (페널티 X)
        if (ToiletManager.Instance == null || !ToiletManager.Instance.HasAny)
        {
            ChangeState(CustomerState.LEAVE);
            return;
        }

        // 좌석 풀어줌 — 식사 끝났으니 회전
        _targetSeat?.Release();
        _targetSeat = null;

        _toiletPhase = ToiletPhase.Stall;
        ChangeState(CustomerState.WALK_TO_TOILET);
    }

    private void WalkToToiletState()
    {
        if (_toiletPhase == ToiletPhase.Stall)
            StepToToiletStall();
        else if (_toiletPhase == ToiletPhase.Sink)
            StepToSink();
        else
            ChangeState(CustomerState.LEAVE);
    }

    private void StepToToiletStall()
    {
        if (ToiletManager.Instance == null || !ToiletManager.Instance.HasAny)
        { ChangeState(CustomerState.LEAVE); return; }

        // 아직 찜 못 했으면 찜 시도
        if (_targetToilet == null)
        {
            var t = ToiletManager.Instance.FindNearestAvailable(transform.position);
            if (t == null) { ChangeState(CustomerState.LEAVE); return; }   // 만석 → 못 감
            _targetToilet = t;
            _targetToilet.Occupy();
        }

        if (NeedStairTo(_targetToilet.transform.position)) return;

        MoveTo(_targetToilet.UsePosition);
        if (HasArrived()) ChangeState(CustomerState.USING_TOILET);
    }

    private void StepToSink()
    {
        // 세면대 없으면 LEAVE (변기 보너스만 챙기고 나감)
        if (SinkManager.Instance == null || !SinkManager.Instance.HasAny)
        { ChangeState(CustomerState.LEAVE); return; }

        if (_targetSink == null)
        {
            var s = SinkManager.Instance.FindNearestAvailable(transform.position);
            if (s == null) { ChangeState(CustomerState.LEAVE); return; }   // 만석 → 변기 보너스만 챙기고 나감
            _targetSink = s;
            _targetSink.Occupy();
        }

        if (NeedStairTo(_targetSink.transform.position)) return;

        MoveTo(_targetSink.UsePosition);
        if (HasArrived()) ChangeState(CustomerState.USING_TOILET);
    }

    /// <summary>대상 floor가 다르면 stair 세팅 + WALK_TO_STAIR 전이. true 반환 시 호출측은 return.</summary>
    private bool NeedStairTo(Vector3 targetWorld)
    {
        FloorIndex myFloor = GridManager.Instance.GetFloorAt(transform.position);
        FloorIndex targetFloor = GridManager.Instance.GetFloorAt(targetWorld);
        if (myFloor == targetFloor) return false;

        Stair stair = StairManager.Instance?.FindNearestStairOnFloor(myFloor, transform.position);
        if (stair == null || !stair.HasPair)
        {
            _targetToilet?.Release(); _targetToilet = null;
            _targetSink?.Release();   _targetSink = null;
            ChangeState(CustomerState.LEAVE);
            return true;
        }
        _pendingStair = stair;
        _stairAfterState = CustomerState.WALK_TO_TOILET;
        ChangeState(CustomerState.WALK_TO_STAIR);
        return true;
    }

    private void UsingToiletState()
    {
        if (_toiletPhase == ToiletPhase.Stall)
        {
            _dirAnim?.FaceDirection(DirectionalCharacterAnimator.DIR_DOWN);  // 변기 사용 중엔 정면
            if (_stateTimer >= _data.toiletUseDuration)
            {
                _satisfaction += _data.toiletStallBonus;
                _targetToilet?.Release();
                _targetToilet = null;
                _toiletPhase = ToiletPhase.Sink;
                ChangeState(CustomerState.WALK_TO_TOILET);
            }
        }
        else if (_toiletPhase == ToiletPhase.Sink)
        {
            if (_targetSink != null) FaceToward(_targetSink.UsePosition);
            if (_stateTimer >= _data.sinkUseDuration)
            {
                _satisfaction += _data.sinkBonus;
                _targetSink?.Release();
                _targetSink = null;
                _toiletPhase = ToiletPhase.None;
                ChangeState(CustomerState.LEAVE);
            }
        }
        else
        {
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

    /// <summary>총 대기(_waitStartTime 기준)가 참을성 한계를 넘었는가 — 서비스 포기 임계.</summary>
    private bool GaveUpWaiting()
        => _waitStartTime > 0f
        && (Time.time - _waitStartTime) > _data.patience * kGiveUpPatienceMult;

    private MenuData PickAffordable(int budget)
    {
        var unlocked = MenuManager.Instance.UnlockedMenus;
        float total = 0f;
        foreach (var m in unlocked)
            if (m.price <= budget && MenuManager.Instance.IsMakeable(m)) total += m.spawnWeight;
        if (total <= 0f) return null;

        float r = Random.Range(0f, total);
        float acc = 0f;
        MenuData last = null;
        foreach (var m in unlocked)
        {
            if (m.price > budget || !MenuManager.Instance.IsMakeable(m)) continue;
            acc += m.spawnWeight;
            last = m;
            if (r <= acc) return m;
        }
        return last;
    }

    private void OnDestroy() => OnDespawned?.Invoke(this);
}
