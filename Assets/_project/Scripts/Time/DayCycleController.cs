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
        CleanupLeftoverFoodAndOrders();   // 영업 시작 전 클린 슬레이트 보장
        customerManager.StartSpawning();
        DTSystem.Instance?.StartSpawning();
        StaffManager.Instance?.OnBusinessOpen();
    }

    /// <summary>맵에 남은 음식 프리팹 + 미처리 주문 전부 제거. 손님/차 다 빠진 뒤 호출.</summary>
    private void CleanupLeftoverFoodAndOrders()
    {
        // 픽업대 주문 큐 + 픽업대 위 음식 정리
        PassWindowManager.Instance?.ClearAll();

        // 테이블/직원 손/DT 픽업창구 등 어디든 남아있는 음식 프리팹 전부 제거
        foreach (var f in UnityEngine.Object.FindObjectsByType<Food>(FindObjectsSortMode.None))
            if (f != null) Destroy(f.gameObject);
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
        CleanupLeftoverFoodAndOrders();           // 영업 종료 시 남은 음식/주문 정리
        OnSettlementReady?.Invoke();
    }

    // 정산 UI의 "다음 날" 버튼이 호출
    public void ConfirmSettlement()
    {
        // 방금 끝난 달의 매출을 히스토리에 기록 (SettleMonthly의 ResetMonthly 전에 호출)
        SalesTracker.Instance?.CloseMonth(time.Year, time.Month);
        MoneySystem.Instance.SettleMonthly();
        time.BeginDay();
    }
}
