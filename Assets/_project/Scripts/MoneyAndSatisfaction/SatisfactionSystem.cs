using System;
using UnityEngine;

public class SatisfactionSystem : MonoBehaviour
{
    public event Action<int> OnSatisfactionChanged;
    public static SatisfactionSystem Instance;
    private int _satisfaction = 0;
    public int Satisfaction => _satisfaction;

    void Awake() 
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this; 
    }
    public bool CanAfford(int amount) => _satisfaction >= amount;
    public bool Spend(int amount)
    {
        if (!CanAfford(amount)) return false;
        _satisfaction -= amount;
        OnSatisfactionChanged?.Invoke(_satisfaction);
        return true;
    }

    public void Earn(int amount)
    {
        _satisfaction += amount;
        OnSatisfactionChanged?.Invoke(_satisfaction);
    }

    // ─── Save / Load ───
    public SatisfactionData ToData() => new SatisfactionData { satisfaction = _satisfaction };

    public void FromData(SatisfactionData data)
    {
        if (data == null) return;
        _satisfaction = data.satisfaction;
        OnSatisfactionChanged?.Invoke(_satisfaction);
    }
}
