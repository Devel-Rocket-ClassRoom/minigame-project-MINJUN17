using System;
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

        ChangeState(CustomerState.Enter);
    }

    private void Update()
    {
        switch (_state)
        {
            case CustomerState.Enter:            EnterState(); break;
            case CustomerState.WALK_TO_COUNTER:  WalkToCounterState(); break;
            case CustomerState.WAIT_AT_COUNTER:  WaitAtCounterState(); break;
            case CustomerState.PAY:              break;
            case CustomerState.WALK_TO_SEAT:     WalkToSeatState(); break;
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
                _waitStartTime = Time.time;
                int dummyPrice = Random.Range(3000, 10000);   // 임시 (메뉴 시스템 별도 이슈)
                _targetCounter.ReceiveOrder(dummyPrice);
                break;

            case CustomerState.PAY:
                _targetCounter.OnCustomerPaid();
                _targetCounter = null;
                ChangeState(CustomerState.WALK_TO_SEAT);
                break;

            case CustomerState.LEAVE:
                float waitDuration = _waitStartTime > 0 ? Time.time - _waitStartTime : 0f;
                if (waitDuration > _data.patience)
                    _satisfaction -= Mathf.FloorToInt((waitDuration - _data.patience) * waitPenaltyRate);
                _satisfaction = Mathf.Max(0, _satisfaction);

                _targetSeat?.Release();
                _targetSeat = null;
                SatisfactionSystem.Instance.Earn(_satisfaction);

                break;
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

    private void WaitAtCounterState()
    {
        // 직원이 OnFoodReady 호출할 때까지 대기
    }

    // === 외부 진입점 (Counter가 호출) ===
    public void OnFoodReady()
    {
        if (_state == CustomerState.WAIT_AT_COUNTER)
            ChangeState(CustomerState.PAY);
    }

    private void WalkToSeatState()
    {
        if (MoveTowards(_targetSeat.transform.position))
            ChangeState(CustomerState.EAT);
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
