using UnityEngine;

// staff 구현을 위한 임시 MoneySystem

public class MoneySystem : MonoBehaviour
{
    public static MoneySystem Instance;
    public long money = 10000;  // 임시 시작 자금

    void Awake() { Instance = this; }

    public bool CanAfford(long amount) => money >= amount;

    public bool Spend(long amount)
    {
        if (!CanAfford(amount)) return false;
        money -= amount;
        Debug.Log($"[Money] -{amount} → 잔액 {money}");
        return true;
    }

    public void Earn(long amount)
    {
        money += amount;
        Debug.Log($"[Money] +{amount} → 잔액 {money}");
    }
}
