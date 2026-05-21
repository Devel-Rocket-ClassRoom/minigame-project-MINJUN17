using System;
using UnityEngine;

public class DayCycleController : MonoBehaviour
{
    [SerializeField] private TimeSystem time;
    [SerializeField] private CustomerManager customerManager;

    public event Action OnSettlementReady;   // 정산 UI가 구독

    private bool _waitingToClear;
    private void OnEnable()
    {
        time.OnCloseHourReached += HandleClose;
        time.OnDayStarted += HandleDayStarted;
        customerManager.OnEmpty += HandleEmpty;
    }

    private void OnDisable()
    {
        time.OnCloseHourReached -= HandleClose;
        time.OnDayStarted -= HandleDayStarted;
        customerManager.OnEmpty -= HandleEmpty;
    }

    private void Start()
    {
        time.BeginDay();
        OnSettlementReady += AutoConfirm;
    }

    private void HandleDayStarted()
    {
        customerManager.StartSpawning();
        DTSystem.Instance?.StartSpawning();
    }
    private void AutoConfirm() // 임시
    {
        Debug.Log($"[정산] Day 정산 — 보유금: {MoneySystem.Instance.Money}");
        ConfirmSettlement();
    }

    private void HandleClose()
    {
        customerManager.StopSpawning();
        DTSystem.Instance?.StopSpawning();
        customerManager.ForceLeaveWaitingCustomers(); // 줄 서 있던 손님 즉시 퇴장
        _waitingToClear = true;

        // 영업 종료 순간 이미 손님이 0이면 바로 정산
        if (customerManager.ActiveCount == 0) TriggerSettlement();
    }

    private void HandleEmpty()
    {
        // 영업 중에 0명 된 경우는 무시. 닫은 후의 0명만 처리
        if (!_waitingToClear) return;
        TriggerSettlement();
    }

    private void TriggerSettlement()
    {
        _waitingToClear = false;
        OnSettlementReady?.Invoke();
    }

    // 정산 UI의 "다음 날" 버튼이 호출
    public void ConfirmSettlement()
    {
        MoneySystem.Instance.SettleMonthly();
        time.BeginDay();
    }
}
