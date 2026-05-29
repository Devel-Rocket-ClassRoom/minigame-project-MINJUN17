using System;
using UnityEngine;

public class DayCycleController : MonoBehaviour
{
    [SerializeField] private TimeSystem time;
    [SerializeField] private CustomerManager customerManager;

    [Header("DT 안전 타이머")]
    [Tooltip("영업 종료 후 이 시간(초)이 지나도 DT 차가 안 빠지면 강제 정리. 0 이하면 비활성.")]
    [SerializeField] private float dtSafetyTimeout = 60f;

    public event Action OnSettlementReady;   // 정산 UI가 구독

    private bool _waitingToClear;
    private float _waitingStartedAt;
    private bool _safetyApplied;

    private void OnEnable()
    {
        time.OnCloseHourReached += HandleClose;
        time.OnDayStarted += HandleDayStarted;
        customerManager.OnEmpty += HandleEmpty;
        if (DTLane.Instance != null) DTLane.Instance.OnEmpty += HandleDTEmpty;
    }

    private void OnDisable()
    {
        time.OnCloseHourReached -= HandleClose;
        time.OnDayStarted -= HandleDayStarted;
        customerManager.OnEmpty -= HandleEmpty;
        if (DTLane.Instance != null) DTLane.Instance.OnEmpty -= HandleDTEmpty;
    }

    private void Start()
    {
        // DTLane이 OnEnable 시점에 아직 없을 수 있으므로 한 번 더 시도
        if (DTLane.Instance != null)
        {
            DTLane.Instance.OnEmpty -= HandleDTEmpty;
            DTLane.Instance.OnEmpty += HandleDTEmpty;
        }

        time.BeginDay();
    }

    private void Update()
    {
        if (!_waitingToClear || _safetyApplied) return;
        if (dtSafetyTimeout <= 0f) return;
        if (Time.unscaledTime - _waitingStartedAt < dtSafetyTimeout) return;

        _safetyApplied = true;
        int remaining = DTLane.Instance != null ? DTLane.Instance.ActiveCarCount : 0;
        if (remaining > 0)
        {
            Debug.LogWarning($"[DayCycle] DT safety timeout ({dtSafetyTimeout}s) — forcing {remaining} car(s) to clear");
            DTLane.Instance.ClearAllCars();
        }
        TryTriggerSettlement();
    }

    private void HandleDayStarted()
    {
        customerManager.StartSpawning();
        DTSystem.Instance?.StartSpawning();
        StaffManager.Instance?.OnBusinessOpen();
    }

    private void HandleClose()
    {
        customerManager.StopSpawning();
        DTSystem.Instance?.StopSpawning();
        customerManager.ForceLeaveWaitingCustomers(); // 줄 서 있던 손님 즉시 퇴장

        _waitingToClear = true;
        _waitingStartedAt = Time.unscaledTime;
        _safetyApplied = false;

        TryTriggerSettlement();
    }

    private void HandleEmpty()
    {
        TryTriggerSettlement();
    }

    private void HandleDTEmpty()
    {
        TryTriggerSettlement();
    }

    private void TryTriggerSettlement()
    {
        if (!_waitingToClear) return;
        if (customerManager.ActiveCount > 0) return;
        if (DTLane.Instance != null && DTLane.Instance.ActiveCarCount > 0) return;
        TriggerSettlement();
    }

    private void TriggerSettlement()
    {
        _waitingToClear = false;
        _safetyApplied = false;
        StaffManager.Instance?.OnBusinessClose(); // 손님 다 나간 뒤에야 직원 퇴근
        OnSettlementReady?.Invoke();
    }

    // 정산 UI의 "다음 날" 버튼이 호출
    public void ConfirmSettlement()
    {
        MoneySystem.Instance.SettleMonthly();
        time.BeginDay();
    }
}
