using System.Collections.Generic;
using UnityEngine;

public class SalesTracker : MonoBehaviour
{
    public static SalesTracker Instance;

    private readonly Dictionary<MenuData, int> _monthlySales = new();
    private long _annualRevenue;

    // 정보 탭 통계
    private long _lifetimeRevenue;   // 총매출 (누적, 리셋 없음)
    private long _monthlyRevenue;    // 진행 중인 이번 달 매출 (정산 시 히스토리로 넘김)
    private long _totalCustomers;    // 누적 방문 손님 수
    private readonly List<MonthlyRevenueEntry> _monthlyHistory = new();

    public IReadOnlyDictionary<MenuData, int> MonthlySales => _monthlySales;
    public long AnnualRevenue => _annualRevenue;

    public long LifetimeRevenue => _lifetimeRevenue;
    public long TotalCustomers  => _totalCustomers;
    public IReadOnlyList<MonthlyRevenueEntry> MonthlyHistory => _monthlyHistory;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RecordSale(MenuData menu)
    {
        if (menu == null) return;
        _monthlySales.TryGetValue(menu, out int count);
        _monthlySales[menu] = count + 1;
        _annualRevenue   += menu.price;
        _monthlyRevenue  += menu.price;
        _lifetimeRevenue += menu.price;   // 총매출 누적
    }

    /// <summary>손님이 가게에 들어올 때 호출 (누적 방문 손님 수).</summary>
    public void RecordCustomerVisit() => _totalCustomers++;

    /// <summary>
    /// 월 정산 확정 시 호출. 이번 달 매출을 히스토리에 기록하고 리셋.
    /// (메뉴별 판매량 _monthlySales clear는 ResetMonthly가 담당 — 재료비 계산 후 호출되므로 분리)
    /// </summary>
    public void CloseMonth(int year, int month)
    {
        _monthlyHistory.Add(new MonthlyRevenueEntry { year = year, month = month, revenue = _monthlyRevenue });
        _monthlyRevenue = 0;
    }

    public void ResetAnnual() => _annualRevenue = 0;

    public long CalculateMaterialCost()
    {
        long total = 0;
        foreach (var kv in _monthlySales)
            total += (long)kv.Key.cost * kv.Value;
        return total;
    }

    public void ResetMonthly() => _monthlySales.Clear();

    // ─── Save / Load ───
    public SalesData ToData()
    {
        var monthly = new List<MonthlySaleEntry>();
        foreach (var kv in _monthlySales)
        {
            if (kv.Key == null) continue;
            if (kv.Key is not ISaveIdentifiable ident || string.IsNullOrEmpty(ident.SaveId)) continue;
            monthly.Add(new MonthlySaleEntry { menuId = ident.SaveId, count = kv.Value });
        }
        return new SalesData
        {
            annualRevenue     = _annualRevenue,
            monthlySales      = monthly,
            lifetimeRevenue   = _lifetimeRevenue,
            monthlyRevenueAcc = _monthlyRevenue,
            totalCustomers    = _totalCustomers,
            monthlyHistory    = new List<MonthlyRevenueEntry>(_monthlyHistory),
        };
    }

    public void FromData(SalesData data)
    {
        if (data == null) return;
        _annualRevenue = data.annualRevenue;
        _monthlySales.Clear();
        if (data.monthlySales != null)
        {
            foreach (var entry in data.monthlySales)
            {
                var menu = SaveIdRegistry.GetById<MenuData>(entry.menuId);
                if (menu != null) _monthlySales[menu] = entry.count;
            }
        }

        _lifetimeRevenue = data.lifetimeRevenue;
        _monthlyRevenue  = data.monthlyRevenueAcc;
        _totalCustomers  = data.totalCustomers;
        _monthlyHistory.Clear();
        if (data.monthlyHistory != null) _monthlyHistory.AddRange(data.monthlyHistory);
    }
}