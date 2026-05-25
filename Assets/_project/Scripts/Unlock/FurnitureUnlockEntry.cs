using UnityEngine;
using UnityEngine.Localization;

/// <summary>FurnitureData → IUnlockEntry 어댑터. 1회 영구 해금, 만족도 차감.</summary>
public class FurnitureUnlockEntry : IUnlockEntry
{
    private readonly FurnitureData _data;
    public FurnitureUnlockEntry(FurnitureData data) { _data = data; }

    public FurnitureData Data => _data;

    public LocalizedString DisplayName => _data.displayName;
    public LocalizedString Description => _data.description;
    public Sprite Icon                 => _data.icon;
    public long Cost                   => _data.satisfactionUnlock;
    public CurrencyType Currency       => CurrencyType.Satisfaction;

    public bool IsVisible    => _data != null;          // 가구는 unlockOnExpansion 가구는 패널 측에서 미리 제외
    public bool IsPurchased  => CatalogManager.Instance != null
                             && CatalogManager.Instance.IsUnlocked(_data);
    public bool CanAfford    => SatisfactionSystem.Instance != null
                             && SatisfactionSystem.Instance.CanAfford((int)Cost);

    public bool Purchase()
        => CatalogManager.Instance != null && CatalogManager.Instance.TryUnlock(_data);
}
