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

        // 옵션 A: 자리 먼저 예약. 없으면 입장 거부
        _targetSeat = _seatManager.GetFirstAvailableSeat();
        
        if (_targetSeat == null)
        {
            Destroy(gameObject);
            return;
        }
        _targetSeat.Occupy();

        // 줄에 등록 (Spawner가 HasRoom 미리 체크하지만 방어적으로)
        if (!_queueManager.TryEnqueue(this))
        {
            _targetSeat.Release();
            Destroy(gameObject);
            return;
        }

        int orderCount = Random.Range(_data.minOrderCount, _data.maxOrderCount + 1);
        OrderedMenus = new List<MenuData>();
        for (int i = 0; i < orderCount; i++)
            OrderedMenus.Add(MenuManager.Instance.PickRandomByWeight());

        _waitStartTime = Time.time;
        ChangeState(CustomerState.Enter);
    }

    private void Update()
    {
        switch (_state)
        {
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
                break;
        }
    }

    public void OnOrderTaken(ServerStaff server)
    {
        if (_state == CustomerState.WAIT_AT_COUNTER)
        {
            _satisfaction += Mathf.FloorToInt(server.Data.kindness);
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
            _satisfaction += Mathf.FloorToInt(server.Data.kindness);
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
