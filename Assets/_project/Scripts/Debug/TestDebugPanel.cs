using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 3개월 압축 플레이 테스트용 디버그 패널.
/// 토글 버튼으로 패널 열고 닫고, 각 시스템 기능을 즉시 발동시킬 수 있음.
/// 24시간 = 1달 가정.
/// </summary>
public class TestDebugPanel : MonoBehaviour
{
    [Header("패널 (토글 대상)")]
    [Tooltip("디버그 버튼들이 들어있는 패널 GameObject를 연결. 기본 비활성.")]
    [SerializeField] private GameObject panelRoot;

    [Header("연말 랭킹 결과 팝업 (옵션)")]
    [Tooltip("연말 랭킹 결과 보여줄 Text(TMP/Legacy 둘 다 지원). null이면 OnGUI로 화면 표시.")]
    [SerializeField] private UnityEngine.UI.Text rankingText;
    [SerializeField] private TMPro.TMP_Text rankingTextTMP;
    [Tooltip("랭킹 결과 표시할 패널 (Show/Hide). null이면 OnGUI 사용.")]
    [SerializeField] private GameObject rankingPanel;

    private string _lastRankingMessage;
    private float _onGuiShowUntil;

    [Header("의존성 (없으면 런타임에 자동 탐색)")]
    [SerializeField] private TimeSystem timeSystem;
    [SerializeField] private DayCycleController dayCycle;
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private PlacementSystem placementSystem;

    [Header("배치 테스트용 가구 목록")]
    [Tooltip("새 가구 설치(임의) 버튼이 랜덤으로 하나 골라 설치")]
    [SerializeField] private FurnitureData[] testFurnitures;

    [Header("테스트용 마케팅 데이터")]
    [Tooltip("'마케팅 적용' 버튼이 호출할 ScriptableObject")]
    [SerializeField] private MarketingData testMarketing;

    [Header("시간 배율 (영업 중 1시간 → 실제 X초)")]
    [SerializeField] private float fastHourInterval = 0.3f;
    [SerializeField] private float fastNightInterval = 0.05f;
    [SerializeField] private float normalHourInterval = 15f;
    [SerializeField] private float normalNightInterval = 0.5f;

    [Header("월 스킵 사이 대기 (초)")]
    [SerializeField] private float skipDelay = 1.5f;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (timeSystem == null)       timeSystem       = FindFirstObjectByType<TimeSystem>();
        if (dayCycle == null)         dayCycle         = FindFirstObjectByType<DayCycleController>();
        if (customerManager == null)  customerManager  = FindFirstObjectByType<CustomerManager>();
        if (placementSystem == null)  placementSystem  = FindFirstObjectByType<PlacementSystem>();
    }

    private void Start()
    {
        if (timeSystem != null)
        {
            timeSystem.OnYearEnded += () => Debug.Log($"<color=yellow>[연말 도달!] Year {timeSystem.Year} 종료, 랭킹 산출 중...</color>");
        }
        if (RankingSystem.Instance != null)
        {
            RankingSystem.Instance.OnYearRanked += ShowRankingResult;
        }
    }

    private void ShowRankingResult(RankingSystem.YearResult r)
    {
        string msg =
            $"=== 연말 결산 ===\n" +
            $"연도: {timeSystem.Year}\n" +
            $"매출: {r.Revenue:N0}\n" +
            $"평판: {r.Reputation:N0}\n" +
            $"점수: {r.Score:N0}\n" +
            (r.Qualified ? $"순위: {r.Rank}위" : "순위권 외 (최소 점수 미달)");

        _lastRankingMessage = msg;

        if (rankingTextTMP != null) rankingTextTMP.text = msg;
        if (rankingText    != null) rankingText.text    = msg;
        if (rankingPanel   != null) rankingPanel.SetActive(true);

        // 위 UI 어느 것도 연결 안 되어있으면 OnGUI로 화면에 표시 (5초간)
        if (rankingPanel == null && rankingText == null && rankingTextTMP == null)
            _onGuiShowUntil = Time.time + 5f;

        Debug.Log($"<color=cyan>{msg.Replace("\n", " | ")}</color>");
    }

    /// <summary>랭킹 팝업 닫기 버튼용.</summary>
    public void HideRankingPanel()
    {
        if (rankingPanel != null) rankingPanel.SetActive(false);
        _onGuiShowUntil = 0;
    }

    private void OnGUI()
    {
        if (Time.time > _onGuiShowUntil || string.IsNullOrEmpty(_lastRankingMessage)) return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;
        float w = 400, h = 240;
        GUI.Box(new Rect(Screen.width / 2 - w / 2, Screen.height / 2 - h / 2, w, h), _lastRankingMessage, style);
    }

    // ============================== 패널 토글 ==============================

    /// <summary>토글 버튼에 연결: 패널 보임/숨김 전환.</summary>
    public void TogglePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(!panelRoot.activeSelf);
    }

    public void ShowPanel() { if (panelRoot != null) panelRoot.SetActive(true); }
    public void HidePanel() { if (panelRoot != null) panelRoot.SetActive(false); }

    // ============================== 시간 배율 ==============================

    /// <summary>시간 빠르게 (테스트 모드).</summary>
    public void ApplyFastTime()  => SetTimeIntervals(fastHourInterval, fastNightInterval);

    /// <summary>시간 원상복구.</summary>
    public void ApplyNormalTime() => SetTimeIntervals(normalHourInterval, normalNightInterval);

    private void SetTimeIntervals(float hourInt, float nightInt)
    {
        if (timeSystem == null) { Debug.LogWarning("[TestDebugPanel] TimeSystem 없음"); return; }
        var t = typeof(TimeSystem);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        t.GetField("_hourInterval", flags)?.SetValue(timeSystem, hourInt);
        t.GetField("_nightHourInterval", flags)?.SetValue(timeSystem, nightInt);
        Debug.Log($"[TestDebugPanel] 시간 배율 변경: hour={hourInt}s, night={nightInt}s");
    }

    // ============================== 월/년 스킵 ==============================

    /// <summary>이번 달 강제 종료 + 정산 + 다음 달 시작 (즉시).</summary>
    public void SkipOneMonth() => ForceAdvanceMonths(1);

    /// <summary>3개월 즉시 진행.</summary>
    public void Skip3Months() => ForceAdvanceMonths(3);

    /// <summary>12개월(1년) 즉시 진행. 연말 랭킹까지 발동.</summary>
    public void Skip12Months() => ForceAdvanceMonths(12);

    /// <summary>지정한 개월수만큼 즉시 진행 (정산 + OnYearEnded + OnDayStarted 발동).</summary>
    public void ForceAdvanceMonths(int count)
    {
        if (timeSystem == null) { Debug.LogWarning("[TestDebugPanel] TimeSystem 없음"); return; }
        var t = typeof(TimeSystem);
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;

        // 진행 중인 코루틴 정지
        timeSystem.StopAllCoroutines();

        var hourField   = t.GetField("_hour", bf);
        var monthField  = t.GetField("_month", bf);
        var yearField   = t.GetField("_year", bf);
        var tickField   = t.GetField("_ticking", bf);
        var openField   = t.GetField("_openHour", bf);

        var onYearEnded   = t.GetField("OnYearEnded", bf);
        var onDayStarted  = t.GetField("OnDayStarted", bf);
        var onHourChanged = t.GetField("OnHourChanged", bf);

        int openHour = (int)openField.GetValue(timeSystem);

        // 초기 상태(month=0) 보정: 첫 영업 시작 전 상태 → month=1로 시작 처리
        int startMonth = (int)monthField.GetValue(timeSystem);
        if (startMonth == 0)
        {
            monthField.SetValue(timeSystem, 1);
            Debug.Log("[TestDebugPanel] 초기 month=0 감지 → 1로 보정 (1년=12달 일관성)");
        }

        for (int i = 0; i < count; i++)
        {
            // 1) 정산 (재료비/급여/유지비 차감)
            MoneySystem.Instance?.SettleMonthly();

            // 2) 월/년 진행
            int month = (int)monthField.GetValue(timeSystem);
            int year  = (int)yearField.GetValue(timeSystem);
            month++;
            bool yearEnded = false;
            if (month > 12) { month = 1; year++; yearEnded = true; }
            monthField.SetValue(timeSystem, month);
            yearField.SetValue(timeSystem, year);

            // 3) 시간 = 영업 시작 시간으로 리셋
            hourField.SetValue(timeSystem, openHour);
            tickField.SetValue(timeSystem, true);

            // 4) 이벤트 발동
            (onHourChanged.GetValue(timeSystem) as Action)?.Invoke();
            if (yearEnded)
            {
                Debug.Log($"<color=yellow>[연도 전환] {year-1} → {year}, OnYearEnded 발동</color>");
                (onYearEnded.GetValue(timeSystem) as Action)?.Invoke();
            }
            (onDayStarted.GetValue(timeSystem) as Action)?.Invoke();

            Debug.Log($"[TestDebugPanel] {i+1}/{count}달 스킵 → Year={year}, Month={month}");
        }

        // 스킵 종료 후 랭킹 상태 확인
        if (RankingSystem.Instance != null)
        {
            var r = RankingSystem.Instance.LastResult;
            Debug.Log($"[랭킹 최종상태] Score={r.Score} Rank={r.Rank} Qualified={r.Qualified} (Revenue={r.Revenue}, Reputation={r.Reputation})");
        }
    }

    // ============================== 돈 ==============================

    [Header("돈")]
    [SerializeField] private long addMoneyAmount = 10000;

    /// <summary>설정된 금액(기본 10000) 즉시 획득.</summary>
    public void AddMoney()
    {
        if (MoneySystem.Instance == null) { Debug.LogWarning("[TestDebugPanel] MoneySystem 없음"); return; }
        MoneySystem.Instance.Earn(addMoneyAmount);
        Debug.Log($"[TestDebugPanel] +{addMoneyAmount:N0}원 → 현재 {MoneySystem.Instance.Money:N0}");
    }

    // ============================== 직원 ==============================

    [Header("라이더 테스트")]
    [SerializeField] private StaffData testRiderData;

    /// <summary>만족도 무시하고 Phone 카탈로그 해금.</summary>
    public void ForceUnlockPhone()
    {
        if (PhoneManager.Instance == null) { Debug.LogWarning("[TestDebugPanel] PhoneManager 없음"); return; }
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        var f = typeof(PhoneManager).GetField("unlocked", bf);
        if (f == null) { Debug.LogWarning("[TestDebugPanel] unlocked 필드 못 찾음"); return; }
        if ((bool)f.GetValue(PhoneManager.Instance)) { Debug.Log("[TestDebugPanel] Phone 이미 해금됨"); return; }
        f.SetValue(PhoneManager.Instance, true);
        var ev = typeof(PhoneManager).GetField("OnUnlocked", bf);
        (ev?.GetValue(PhoneManager.Instance) as Action)?.Invoke();
        Debug.Log("[TestDebugPanel] Phone 강제 해금");
    }

    /// <summary>라이더 1명 즉시 고용 (비용 자동 충전).</summary>
    public void HireRider()
    {
        if (StaffManager.Instance == null) { Debug.LogWarning("[TestDebugPanel] StaffManager 없음"); return; }
        if (testRiderData == null) { Debug.LogWarning("[TestDebugPanel] testRiderData 미지정"); return; }

        // 모든 사전 조건을 한 번에 점검 (어디서 막히는지 보이도록)
        var sm = StaffManager.Instance;
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        var prefabField = typeof(StaffManager).GetField("riderStaffPrefab", bf);
        var prefab = prefabField?.GetValue(sm);
        bool prefabOK   = prefab != null && !prefab.Equals(null);
        bool unlockOK   = sm.IsRiderHiringUnlocked;
        bool roomOK     = RiderRoomManager.Instance == null || RiderRoomManager.Instance.HasRiderRoom();
        bool underCap   = sm.RiderCount < sm.MaxRiderCount;
        long money      = MoneySystem.Instance != null ? MoneySystem.Instance.Money : 0;

        Debug.Log(
            $"[HireRider 사전점검]\n" +
            $"  riderStaffPrefab 인스펙터 할당 = {prefabOK}\n" +
            $"  IsRiderHiringUnlocked (Phone 해금+설치) = {unlockOK}\n" +
            $"  HasRiderRoom (RiderRoom zone 존재) = {roomOK}\n" +
            $"  RiderCount {sm.RiderCount}/{sm.MaxRiderCount} (상한 미만? {underCap})\n" +
            $"  현재 돈 = {money:N0}, 고용비 = {testRiderData.hireCost:N0}");

        if (!prefabOK) { Debug.LogError("[TestDebugPanel] ← StaffManager 인스펙터의 riderStaffPrefab 슬롯 비어있음"); return; }
        if (!unlockOK) { Debug.LogError("[TestDebugPanel] ← Phone 미설치 또는 미해금 (ForceUnlockPhone + Phone 가구 배치 확인)"); return; }
        if (!roomOK)   { Debug.LogError("[TestDebugPanel] ← RiderRoom zone 없음 (확장 단계 적용 필요)"); return; }
        if (!underCap) { Debug.LogError($"[TestDebugPanel] ← 라이더 상한 도달 ({sm.RiderCount}/{sm.MaxRiderCount})"); return; }

        // 비용 자동 충전 후 고용
        if (MoneySystem.Instance != null) MoneySystem.Instance.Earn(testRiderData.hireCost);
        var rider = sm.HireRiderStaff(testRiderData);
        if (rider != null)
            Debug.Log($"[TestDebugPanel] 라이더 고용 OK (현재 {sm.RiderCount}명)");
        else
            Debug.LogError("[TestDebugPanel] 사전점검 통과했으나 HireRiderStaff 실패 — StaffData 자체 이슈 가능");
    }

    /// <summary>모든 직원에게 1개월 tenure 부여 (성장 + 승급 조건 체크용).</summary>
    public void TickAllStaffMonth()
    {
        if (StaffManager.Instance == null) { Debug.LogWarning("[TestDebugPanel] StaffManager 없음"); return; }
        int n = 0;
        foreach (var c in StaffManager.Instance.CookStaffs)   { c.TickMonth(); n++; }
        foreach (var s in StaffManager.Instance.ServerStaffs) { s.TickMonth(); n++; }
        Debug.Log($"[TestDebugPanel] 직원 {n}명 +1개월 (tenure/성장)");
    }

    /// <summary>현재 직원 상태 로그 출력.</summary>
    public void LogStaffStatus()
    {
        if (StaffManager.Instance == null) return;
        foreach (var c in StaffManager.Instance.CookStaffs)
            Debug.Log($"[Cook#{c.Id}] grade={c.Data.grade} tenure={c.TenureMonths} growth={c.GrowthMultiplier:F2} canUpgrade={c.CanUpgrade}");
        foreach (var s in StaffManager.Instance.ServerStaffs)
            Debug.Log($"[Server#{s.Id}] grade={s.Data.grade} tenure={s.TenureMonths} growth={s.GrowthMultiplier:F2} canUpgrade={s.CanUpgrade}");
    }

    // ============================== 마케팅 ==============================

    /// <summary>지정된 testMarketing을 즉시 구매 적용 (만족도 차감).</summary>
    public void ApplyTestMarketing()
    {
        if (MarketingManager.Instance == null) { Debug.LogWarning("[TestDebugPanel] MarketingManager 없음"); return; }
        if (testMarketing == null) { Debug.LogWarning("[TestDebugPanel] testMarketing 미지정"); return; }
        bool ok = MarketingManager.Instance.Apply(testMarketing);
        Debug.Log($"[TestDebugPanel] 마케팅 적용 결과: {ok} ({testMarketing.marketingName})");
        if (!ok) Debug.LogWarning($"[TestDebugPanel] 만족도 부족. 현재 만족도: {SatisfactionSystem.Instance?.Satisfaction}, 비용: {testMarketing.satisfactionCost}");
        if (ok) LogSpawnInterval();
    }

    /// <summary>만족도 충전 (테스트용). 마케팅 구매 전에 호출.</summary>
    public void AddSatisfaction1000()
    {
        if (SatisfactionSystem.Instance == null) return;
        SatisfactionSystem.Instance.Earn(1000);
        Debug.Log($"[TestDebugPanel] +1000 만족도 → 현재: {SatisfactionSystem.Instance.Satisfaction}");
    }

    /// <summary>마케팅 강제 적용 (만족도 무시, _pending에 직접 추가).</summary>
    public void ForceApplyTestMarketing()
    {
        if (MarketingManager.Instance == null || testMarketing == null) return;
        var pendingField = typeof(MarketingManager).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance);
        if (pendingField == null) { Debug.LogWarning("[TestDebugPanel] _pending 필드 못 찾음"); return; }
        var list = pendingField.GetValue(MarketingManager.Instance) as System.Collections.Generic.List<MarketingData>;
        list?.Add(testMarketing);
        Debug.Log($"[TestDebugPanel] 마케팅 강제 적용 (다음 영업 시작 시 활성화): {testMarketing.marketingName}");
        LogSpawnInterval();
    }

    /// <summary>현재 + 적용 예정 마케팅 기준 스폰 간격 비교 출력.</summary>
    public void LogSpawnInterval()
    {
        if (customerManager == null || MarketingManager.Instance == null) return;

        var cmType = typeof(CustomerManager);
        var mmType = typeof(MarketingManager);
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;

        float minBase = (float)cmType.GetField("_minSpawnInterval", bf).GetValue(customerManager);
        float maxBase = (float)cmType.GetField("_maxSpawnInterval", bf).GetValue(customerManager);
        float curMult = (float)cmType.GetField("_marketingMultiplier", bf).GetValue(customerManager);

        // _active + _pending 합산 → 다음 BeginDay 후 multiplier 예측
        var activeList = mmType.GetField("_active", bf).GetValue(MarketingManager.Instance) as System.Collections.IList;
        var pendingList = mmType.GetField("_pending", bf).GetValue(MarketingManager.Instance) as System.Collections.Generic.List<MarketingData>;

        float sumBoost = 0f;
        if (activeList != null)
        {
            foreach (var camp in activeList)
            {
                // ActiveCampaign.Data 접근
                var dataField = camp.GetType().GetField("Data");
                var data = dataField?.GetValue(camp) as MarketingData;
                if (data != null) sumBoost += data.spawnBoost;
            }
        }
        if (pendingList != null)
            foreach (var d in pendingList)
                if (d != null) sumBoost += d.spawnBoost;

        float projectedMult = 1f + Mathf.Log(1f + Mathf.Max(0f, sumBoost));

        Debug.Log(
            $"[스폰간격] 기본: {minBase:F1}~{maxBase:F1}초\n" +
            $"  현재 배수={curMult:F2}배 → 실제 간격={minBase/curMult:F1}~{maxBase/curMult:F1}초\n" +
            $"  적용 예정 배수={projectedMult:F2}배 → 예상 간격={minBase/projectedMult:F1}~{maxBase/projectedMult:F1}초\n" +
            $"  (다음 영업 시작 시 적용)");
    }

    // ============================== 손님 ==============================

    public void StartCustomerSpawning()
    {
        if (customerManager == null) return;
        customerManager.StartSpawning();
        Debug.Log("[TestDebugPanel] 손님 스폰 시작");
    }

    public void StopCustomerSpawning()
    {
        if (customerManager == null) return;
        customerManager.StopSpawning();
        Debug.Log("[TestDebugPanel] 손님 스폰 중지");
    }

    /// <summary>손님 1명 즉시 스폰 (private Spawn() reflection 호출).</summary>
    public void SpawnOneCustomer()
    {
        if (customerManager == null) return;
        var m = typeof(CustomerManager).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Debug.LogWarning("[TestDebugPanel] Spawn() 못 찾음"); return; }
        m.Invoke(customerManager, null);
        Debug.Log("[TestDebugPanel] 손님 1명 스폰");
    }

    // ============================== 현재 상태 로그 ==============================

    /// <summary>현재 게임 상태 한번에 로그.</summary>
    public void LogGameStatus()
    {
        if (timeSystem != null)
            Debug.Log($"[Time] Year={timeSystem.Year} Month={timeSystem.Month} Hour={timeSystem.Hour} Open={timeSystem.IsOpen}");
        if (MoneySystem.Instance != null)
            Debug.Log($"[Money] {MoneySystem.Instance.Money}");
        if (SatisfactionSystem.Instance != null)
            Debug.Log($"[Satisfaction] {SatisfactionSystem.Instance.Satisfaction}");
        if (SalesTracker.Instance != null)
            Debug.Log($"[Sales] AnnualRevenue={SalesTracker.Instance.AnnualRevenue}");
        if (RankingSystem.Instance != null && RankingSystem.Instance.LastResult.Qualified)
            Debug.Log($"[LastRank] Rank={RankingSystem.Instance.LastResult.Rank} Score={RankingSystem.Instance.LastResult.Score}");
    }

    // ============================== 맵 확장 ==============================

    /// <summary>다음 확장 단계 활성화. Button.OnClick에 연결.</summary>
    public void ExpandMap()
    {
        if (ExpansionManager.Instance == null) { Debug.LogWarning("[TestDebugPanel] ExpansionManager 없음"); return; }
        if (!ExpansionManager.Instance.CanExpand) { Debug.LogWarning("[TestDebugPanel] 더 이상 확장 단계 없음 (최종 상태)"); return; }

        var next = ExpansionManager.Instance.NextStage;
        bool ok = ExpansionManager.Instance.TryExpand();
        if (ok) Debug.Log($"[TestDebugPanel] 맵 확장 성공: {next?.name ?? "stage"}");
        else    Debug.LogWarning($"[TestDebugPanel] 맵 확장 실패 (자금 부족? 비용={next?.unlockCost})");
    }

    // ============================== 가구 배치 ==============================

    /// <summary>testFurnitures 중 랜덤 하나로 설치 모드 시작.</summary>
    public void StartPlaceRandomFurniture()
    {
        if (placementSystem == null) { Debug.LogWarning("[TestDebugPanel] PlacementSystem 없음"); return; }
        if (testFurnitures == null || testFurnitures.Length == 0)
        {
            Debug.LogWarning("[TestDebugPanel] testFurnitures 비어있음 (Inspector에서 채워주세요)");
            return;
        }
        var data = testFurnitures[UnityEngine.Random.Range(0, testFurnitures.Length)];
        placementSystem.StartPlace(data);
        Debug.Log($"[TestDebugPanel] 설치 모드 시작: {data.name}");
    }

    /// <summary>삭제 모드 진입 (탭으로 대상 선택 후 '확정').</summary>
    public void StartRemoveMode()
    {
        if (placementSystem == null) return;
        placementSystem.StartRemove();
        Debug.Log("[TestDebugPanel] 삭제 모드 시작 - 대상 탭 후 '확정'");
    }

    /// <summary>이동 모드 진입 (탭으로 대상 선택 후 드래그 → '확정').</summary>
    public void StartMoveMode()
    {
        if (placementSystem == null) return;
        placementSystem.StartMove();
        Debug.Log("[TestDebugPanel] 이동 모드 시작 - 대상 탭 후 드래그");
    }

    /// <summary>현재 모드 확정 (배치/이동/삭제 적용).</summary>
    public void ConfirmPlacement()
    {
        if (placementSystem == null) return;
        placementSystem.Confirm();
        Debug.Log($"[TestDebugPanel] 확정 → 현재 모드: {placementSystem.Mode}");
    }

    /// <summary>현재 모드 취소.</summary>
    public void CancelPlacement()
    {
        if (placementSystem == null) return;
        placementSystem.Cancel();
        Debug.Log("[TestDebugPanel] 취소");
    }
}
