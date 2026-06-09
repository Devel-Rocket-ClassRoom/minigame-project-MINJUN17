using System;
using UnityEngine;

public class DayCycleController : MonoBehaviour
{
    [SerializeField] private TimeSystem time;
    [SerializeField] private CustomerManager customerManager;

    [Header("DT 안전 타이머")]
    [Tooltip("영업 종료 후 이 시간(초)이 지나도 DT 차가 안 빠지면 강제 정리. 0 이하면 비활성.")]
    [SerializeField] private float dtSafetyTimeout = 60f;

    [Header("직원 출근 시간")]
    [Tooltip("직원이 영업 오픈(손님 등장)보다 일찍 출근하는 시각(시). 영업 오픈 시각보다 작아야 함.")]
    [SerializeField] private int staffArriveHour = 5;

    [Header("첫 오픈 연출")]
    [Tooltip("튜토리얼 끝난 뒤 첫 영업 오픈 시, 직원이 먼저 자리잡도록 이만큼(초) 뒤 손님 1명 등장. 0이면 즉시.")]
    [SerializeField] private float firstOpenCustomerDelay = 2f;
    private bool _spawnOneOnNextOpen;   // 튜토리얼 종료 직후 첫 오픈에만 손님 1명 즉시

    public event Action OnSettlementReady;   // 정산 UI가 구독

    private bool _waitingToClear;
    private float _waitingStartedAt;
    private bool _safetyApplied;

    private void OnEnable()
    {
        time.OnCloseHourReached += HandleClose;
        time.OnDayStarted += HandleDayStarted;
        time.OnHourChanged += HandleHourChanged;
        customerManager.OnEmpty += HandleEmpty;
        if (DTLane.Instance != null) DTLane.Instance.OnEmpty += HandleDTEmpty;
    }

    private void OnDisable()
    {
        time.OnCloseHourReached -= HandleClose;
        time.OnDayStarted -= HandleDayStarted;
        time.OnHourChanged -= HandleHourChanged;
        customerManager.OnEmpty -= HandleEmpty;
        if (DTLane.Instance != null) DTLane.Instance.OnEmpty -= HandleDTEmpty;
    }

    // 영업 오픈(손님 등장) 전, 지정 시각에 직원 먼저 출근.
    // 오픈 전 밤 시간 진행(RollToOpenHour) 중 hour가 staffArriveHour를 지날 때 1회만 발화
    // (영업 중 hour는 openHour 이상이라 다시 staffArriveHour가 되지 않음).
    private void HandleHourChanged()
    {
        if (time.Hour == staffArriveHour)
            StaffManager.Instance?.OnBusinessOpen();
    }

    private void Start()
    {
        // DTLane이 OnEnable 시점에 아직 없을 수 있으므로 한 번 더 시도
        if (DTLane.Instance != null)
        {
            DTLane.Instance.OnEmpty -= HandleDTEmpty;
            DTLane.Instance.OnEmpty += HandleDTEmpty;
        }

        // 튜토리얼 중엔 영업 시작 보류 → 튜토리얼 끝나면 TutorialManager가 StartBusiness() 호출
        if (TutorialManager.IsActive) return;

        bool loaded = SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasActiveSave;
        if (!loaded)
        {
            time.BeginDay();                       // 신규 게임 — 첫 영업일 시작
        }
        else if (time.IsOpen)
        {
            // 영업 중 저장 로드 → 시간/월 롤 없이 그 시점부터 재개, 스포너만 시작
            customerManager.StartSpawning();
            DTSystem.Instance?.StartSpawning();
        }
        // else: 닫힌 상태 로드 → TimeSystem.FromData가 이미 다음 영업일(BeginDay)을 예약함
    }

    /// <summary>튜토리얼 완료 등 외부에서 영업(시간) 시작.</summary>
    public void StartBusiness()
    {
        _spawnOneOnNextOpen = true;   // 튜토리얼 끝난 뒤 첫 오픈엔 손님 1명 바로 등장
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
            DTLane.Instance.ClearAllCars();
        }
        TryTriggerSettlement();
    }

    private void HandleDayStarted()
    {
        CleanupLeftoverFoodAndOrders();   // 영업 시작 전 클린 슬레이트 보장
        StaffManager.Instance?.OnBusinessOpen();   // 직원 먼저 출근 (손님보다 앞서 입장)
        customerManager.StartSpawning();
        DTSystem.Instance?.StartSpawning();

        if (_spawnOneOnNextOpen)
        {
            _spawnOneOnNextOpen = false;
            // 직원이 먼저 자리잡도록 약간 늦춰 손님 1명 등장 (delay=0이면 즉시)
            if (firstOpenCustomerDelay > 0f) Invoke(nameof(SpawnFirstCustomer), firstOpenCustomerDelay);
            else customerManager.SpawnOneNow();
        }
    }

    private void SpawnFirstCustomer() => customerManager.SpawnOneNow();

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
