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

    [Header("만족도")]
    [SerializeField] private int baseSatisfaction = 50;
    [SerializeField] private int eatGainRate = 5;         // 초당 증가
    [SerializeField] private int waitPenaltyRate = 3;     // patience 초과 1초당 감소

    private float _stateTimer;
    private float _waitStartTime;
    private float _spawnTime;
    private int _satisfaction;
    private PathMover _mover;
    private Food _servedFood;

    public List<MenuData> OrderedMenus { get; private set; }

    private void Awake()
    {
        _mover = GetComponent<PathMover>();
        if (_mover != null) _mover.Role = PathRole.Customer;
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
        _satisfaction = baseSatisfaction;
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
            case CustomerState.WAIT_AT_COUNTER:  break;
            case CustomerState.WALK_TO_SEAT:     WalkToSeatState(); break;
            case CustomerState.WAIT_AT_SEAT:     break;
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
        switch (s)
        {
            case CustomerState.WAIT_AT_COUNTER:
                _targetCounter.OnCustomerArrived(this);
                break;
            case CustomerState.EAT:
                float waitDuration = _waitStartTime > 0 ? Time.time - _waitStartTime : 0f;
                if (waitDuration > _data.patience)
                    _satisfaction -= Mathf.FloorToInt((waitDuration - _data.patience) * waitPenaltyRate);
                _satisfaction = Mathf.Max(0, _satisfaction);
                break;
            case CustomerState.LEAVE:
                _targetSeat?.Release();
                _targetSeat = null;
                SatisfactionSystem.Instance.Earn(_satisfaction);
                ReputationSystem.Instance?.Report(_satisfaction);
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
        MoveTo(_targetCounter.ServicePos.position);
        if (HasArrived())
            ChangeState(CustomerState.WAIT_AT_COUNTER);
    }
    private void WalkToSeatState()
    {
        // 좌석이 사라졌으면(가구 철거/preview race) 크래시 대신 퇴장
        if (_targetSeat == null)
        {
            _targetSeat = null;
            ChangeState(CustomerState.LEAVE);
            return;
        }

        // Seat의 transform.position이 의자 sprite 정렬용으로 셀 중앙에서 어긋날 수 있어서
        // 손님은 그 위치가 속한 셀의 중앙으로 이동
        Vector2Int seatCell = GridManager.Instance.WorldToCell(_targetSeat.transform.position);
        Vector3 target = GridManager.Instance.CellToWorld(seatCell);
        MoveTo(target);
        if (HasArrived())
            ChangeState(CustomerState.WAIT_AT_SEAT);
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
        _satisfaction += Mathf.FloorToInt(Time.deltaTime * eatGainRate);

        if (_stateTimer >= _data.eatSpeed)
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
        MoveTo(_exitPoint);
        if (HasArrived())
            Destroy(gameObject);
    }

    private void OnDestroy() => OnDespawned?.Invoke(this);
}
