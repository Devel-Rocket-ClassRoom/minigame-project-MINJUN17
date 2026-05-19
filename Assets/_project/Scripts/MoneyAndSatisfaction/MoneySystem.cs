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

    public void SettleMonthly()
    {
        long materialCost = SalesTracker.Instance.CalculateMaterialCost();
        long totalSalaryCost = StaffManager.Instance.CalculateTotalSalaryCost();
        long operationCost = gridManager.ActiveCellCount * PricePerSquareMeter;
        Spend(materialCost + operationCost + totalSalaryCost);
        SalesTracker.Instance.ResetMonthly();
    }

    

}
