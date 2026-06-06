using System;
using UnityEngine;

// staff 구현을 위한 임시 MoneySystem

public class MoneySystem : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private long startingMoney = 10000;
    [SerializeField] private long PricePerSquareMeter = 500; // 1셀당 유지비
    public event Action<long> OnMoneyChanged;
    public event Action OnInsufficientFunds;   // 돈 부족 시 (HUD 흔들림용)
    public static MoneySystem Instance;
    private long _money;  // 임시 시작 자금
    public long Money => _money;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _money = startingMoney;
    }

    public bool CanAfford(long amount) => _money >= amount;

    public bool Spend(long amount)
    {
        if (!CanAfford(amount)) { OnInsufficientFunds?.Invoke(); return false; }
        _money -= amount;
        OnMoneyChanged?.Invoke(_money);
        if (amount > 0) SoundManager.Instance?.PlaySfx(SfxId.Purchase);   // 정산(ForceSpend)은 제외
        return true;
    }

    /// <summary>실제 차감 없이 "잔액 부족" 피드백(흔들림)만 트리거 (설치모드 진입 거절 등).</summary>
    public void NotifyInsufficientFunds() => OnInsufficientFunds?.Invoke();

    public void Earn(long amount)
    {
        _money += amount;
        OnMoneyChanged?.Invoke(_money);
    }

    /// <summary>잔액을 직접 설정 (튜토리얼 지급/리셋용).</summary>
    public void SetMoney(long amount)
    {
        _money = amount;
        OnMoneyChanged?.Invoke(_money);
    }

    // 잔액 부족해도 강제로 차감 (마이너스 허용). 정산 등에서 사용.
    public void ForceSpend(long amount)
    {
        _money -= amount;
        OnMoneyChanged?.Invoke(_money);
    }

    public void SettleMonthly()
    {
        var r = CalculateSettlement();
        ForceSpend(r.TotalExpense);
        SalesTracker.Instance.ResetMonthly();
    }

    public SettlementResult CalculateSettlement()
    {
        long operationCost = gridManager != null ? gridManager.ActiveCellCount * PricePerSquareMeter : 0;
        return new SettlementResult
        {
            MaterialCost  = SalesTracker.Instance != null
                             ? SalesTracker.Instance.CalculateMaterialCost() : 0,
            SalaryCost    = StaffManager.Instance != null
                             ? StaffManager.Instance.CalculateTotalSalaryCost() : 0,
            OperationCost = operationCost,
            AirconCost    = AirconManager.Instance != null
                             ? AirconManager.Instance.CalculateExtraCost(operationCost) : 0,
        };
    }

    // ─── Save / Load ───
    public MoneyData ToData() => new MoneyData { money = _money };

    public void FromData(MoneyData data)
    {
        if (data == null) return;
        _money = data.money;
        OnMoneyChanged?.Invoke(_money);
    }
}
