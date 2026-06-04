using UnityEngine;
using UnityEngine.Localization;

/// <summary>MarketingData → IUnlockEntry 어댑터. 효과 지속 중에는 재구매 불가(비활성화), 만족도 차감.</summary>
public class MarketingUnlockEntry : IUnlockEntry
{
    private readonly MarketingData _data;
    public MarketingUnlockEntry(MarketingData data) { _data = data; }

    public MarketingData Data => _data;
    public int DurationMonths => _data.durationMonths;

    /// <summary>현재 효과가 적용 중(활성 또는 당일 대기)인지 — 그렇다면 재구매 불가.</summary>
    public bool IsRunning => MarketingManager.Instance != null
                          && MarketingManager.Instance.IsActiveOrPending(_data);

    /// <summary>남은 개월 수 (적용 중이 아니면 0).</summary>
    public int RemainingMonths => MarketingManager.Instance != null
                          ? MarketingManager.Instance.GetRemainingMonths(_data) : 0;

    public LocalizedString DisplayName => _data.marketingName;
    public LocalizedString Description => _data.description;
    public Sprite Icon                 => _data.icon;
    public long Cost                   => _data.satisfactionCost;
    public CurrencyType Currency       => CurrencyType.Satisfaction;

    public bool IsVisible    => _data != null && _data.prerequisiteZone.IsSatisfied();
    public bool IsPurchased  => false;  // 중첩 가능 — 영영 사라지지 않음
    public bool CanAfford    => SatisfactionSystem.Instance != null
                             && SatisfactionSystem.Instance.CanAfford((int)Cost);

    public bool Purchase()
        => MarketingManager.Instance != null && MarketingManager.Instance.Apply(_data);
}
