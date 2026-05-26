using UnityEngine;
using UnityEngine.Localization;

/// <summary>MarketingData → IUnlockEntry 어댑터. 중첩 구매 가능, 만족도 차감.</summary>
public class MarketingUnlockEntry : IUnlockEntry
{
    private readonly MarketingData _data;
    public MarketingUnlockEntry(MarketingData data) { _data = data; }

    public MarketingData Data => _data;
    public int DurationMonths => _data.durationMonths;

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
