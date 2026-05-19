using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

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

    [Header("만족도")]
    [SerializeField] private int baseSatisfaction = 50;
    [SerializeField] private int eatGainRate = 5;         // 초당 증가
    [SerializeField] private int waitPenaltyRate = 3;     // patience 초과 1초당 감소

    private float _stateTimer;
    private float _waitStartTime;
    private float _spawnTime;
    private int _satisfaction;

    public List<MenuData> OrderedMenus { get; private set; }
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
        // 타임아웃: spawn 이후 patience 초과 → 만족도 0으로 떠남
        if (Time.time - _spawnTime > _data.patience)
        {
            _satisfaction = 0;
            CustomerManager.Instance.UnregisterWaitingForSeat(this);
            ChangeState(CustomerState.LEAVE);
            return;
        }

        // 자리 확보 시도
        var seat = _seatManager.GetFirstAvailableSeat();
        if (seat != null)
        {
            _targetSeat = seat;
            _targetSeat.Occupy();

            if (!_queueManager.TryEnqueue(this))
            {
                _targetSeat.Release();
                _targetSeat = null;
                return; // 다음 프레임 재시도
            }

            CustomerManager.Instance.UnregisterWaitingForSeat(this);
            _waitStartTime = Time.time;
            ChangeState(CustomerState.Enter);
            return;
        }

        MoveTowards(CustomerManager.Instance.GetWaitingSlotPosition(this));
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
        MoveTowards(_queueManager.GetSlotPosition(this));

        if (!_queueManager.IsFront(this)) return;

        var ready = _counterManager.GetReadyCounter();
        if (ready == null) return;

        _targetCounter = ready;
        _targetCounter.Reserve();   // 즉시 점유 표시 (다른 손님 못 가져가게)
        _queueManager.Dequeue(this);
        ChangeState(CustomerState.WALK_TO_COUNTER);
    }

    private void WalkToCounterState()
    {
        if (MoveTowards(_targetCounter.ServicePos.position))
            ChangeState(CustomerState.WAIT_AT_COUNTER);
    }
    private void WalkToSeatState()
    {
        if (MoveTowards(_targetSeat.transform.position))
            ChangeState(CustomerState.WAIT_AT_SEAT);
    }

    public void OnFoodDelivered(ServerStaff server)
    {
        if (_state == CustomerState.WAIT_AT_SEAT)
        {
            _satisfaction += Mathf.FloorToInt(server.EffectiveKindness);
            ChangeState(CustomerState.EAT);
        }
    }

    private void EatState()
    {
        // TODO: 인테리어/직원 능력치 보너스 곱셈으로 반영
        _satisfaction += Mathf.FloorToInt(Time.deltaTime * eatGainRate);

        if (_stateTimer >= _data.eatSpeed)
            ChangeState(CustomerState.LEAVE);
    }

    private void LeaveState()
    {
        if (MoveTowards(_exitPoint))
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy() => OnDespawned?.Invoke(this);
    private bool MoveTowards(Vector3 target) => MoveUtil.MoveTowards(transform, target, _data.moveSpeed);
}
