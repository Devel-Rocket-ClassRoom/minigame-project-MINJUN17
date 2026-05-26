using System;
using UnityEngine;

// staff 구현을 위한 임시 MoneySystem

public class MoneySystem : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private long startingMoney = 10000;
    [SerializeField] private long PricePerSquareMeter = 500; // 1셀당 유지비
    public event Action<long> OnMoneyChanged;
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
        if (!CanAfford(amount)) return false;
        _money -= amount;
        OnMoneyChanged?.Invoke(_money);
        return true;
    }

    public void Earn(long amount)
    {
        _money += amount;
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
        return new SettlementResult
        {
            MaterialCost = SalesTracker.Instance != null
                            ? SalesTracker.Instance.CalculateMaterialCost() : 0,
            SalaryCost = StaffManager.Instance != null
                            ? StaffManager.Instance.CalculateTotalSalaryCost() : 0,
            OperationCost = gridManager != null
                            ? gridManager.ActiveCellCount * PricePerSquareMeter : 0,
        };
    }

}
