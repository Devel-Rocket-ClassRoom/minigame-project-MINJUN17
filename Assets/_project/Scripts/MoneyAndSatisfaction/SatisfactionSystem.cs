using System;
using UnityEngine;

public class SatisfactionSystem : MonoBehaviour
{
    public event Action<int> OnSatisfactionChanged;
    public event Action OnInsufficientSatisfaction;   // 만족도 부족 시 (HUD 흔들림용)
    /// <summary>평생 누적 만족도가 바뀔 때(= Earn 발생 시). 손님 해금 임계점 감시용.</summary>
    public event Action<long> OnLifetimeSatisfactionChanged;
    public static SatisfactionSystem Instance;
    private int _satisfaction = 0;
    public int Satisfaction => _satisfaction;

    /// <summary>지금까지 벌어들인 만족도의 총합. Spend로 깎이지 않고, 연도 넘어가도 초기화 안 됨.</summary>
    private long _lifetimeSatisfaction = 0;
    public long LifetimeSatisfaction => _lifetimeSatisfaction;

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
        if (!CanAfford(amount)) { OnInsufficientSatisfaction?.Invoke(); return false; }
        _satisfaction -= amount;
        OnSatisfactionChanged?.Invoke(_satisfaction);
        if (amount > 0) SoundManager.Instance?.PlaySfx(SfxId.Purchase);
        return true;
    }

    /// <summary>만족도 잔액을 직접 설정 (누적 lifetime은 건드리지 않음 → 손님 해금 트리거 안 됨). 튜토리얼용.</summary>
    public void SetSatisfaction(int amount)
    {
        _satisfaction = Mathf.Max(0, amount);
        OnSatisfactionChanged?.Invoke(_satisfaction);
    }

    public void Earn(int amount)
    {
        _satisfaction += amount;
        if (amount > 0)
        {
            _lifetimeSatisfaction += amount;   // 누적은 Earn으로만 증가, Spend로는 안 깎임
            OnLifetimeSatisfactionChanged?.Invoke(_lifetimeSatisfaction);
        }
        OnSatisfactionChanged?.Invoke(_satisfaction);
    }

    // ─── Save / Load ───
    public SatisfactionData ToData() => new SatisfactionData
    {
        satisfaction = _satisfaction,
        lifetimeSatisfaction = _lifetimeSatisfaction,
    };

    public void FromData(SatisfactionData data)
    {
        if (data == null) return;
        _satisfaction = data.satisfaction;
        // 구버전 세이브(lifetime 없음) 호환: 최소한 현재 잔액만큼은 누적으로 인정
        _lifetimeSatisfaction = Math.Max(data.lifetimeSatisfaction, _satisfaction);
        OnSatisfactionChanged?.Invoke(_satisfaction);
        OnLifetimeSatisfactionChanged?.Invoke(_lifetimeSatisfaction);
    }
}
