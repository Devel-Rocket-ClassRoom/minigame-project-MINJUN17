using System;
using UnityEngine;

// staff 구현을 위한 임시 MoneySystem

public class MoneySystem : MonoBehaviour
{
    [SerializeField] private long startingMoney = 10000;
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
        Debug.Log($"[Money] Earn({amount}) 호출됨, before={_money}");

        _money += amount;
        OnMoneyChanged?.Invoke(_money);
    }

    public void SettleDailyOrMonthly()
    {
        long dummyMaterialCost = 5000;   // TODO: 메뉴 시스템에서 실제 계산
        Spend(dummyMaterialCost);
    }
}
