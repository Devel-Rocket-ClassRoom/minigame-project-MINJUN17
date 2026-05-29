using UnityEngine;
using UnityEngine.Localization;

/// <summary>CookingToolData → IUnlockEntry 어댑터. 설명은 공통 고정 메시지 사용.</summary>
public class CookingToolUnlockEntry : IUnlockEntry
{
    private static readonly LocalizedString SharedDesc =
        new("StringTable", "unlock.cookingtool");

    private readonly CookingToolData _data;
    public CookingToolUnlockEntry(CookingToolData data) { _data = data; }

    public CookingToolData Data => _data;

    public LocalizedString DisplayName => _data.displayName;
    public LocalizedString Description => SharedDesc;
    public Sprite Icon                 => _data.icon;
    public long Cost                   => _data.satisfactionUnlock;
    public CurrencyType Currency       => CurrencyType.Satisfaction;

    public bool IsVisible    => _data != null;
    public bool IsPurchased  => CatalogManager.Instance != null
                             && CatalogManager.Instance.IsUnlocked(_data);
    public bool CanAfford    => SatisfactionSystem.Instance != null
                             && SatisfactionSystem.Instance.CanAfford((int)Cost);

    public bool Purchase()
        => CatalogManager.Instance != null && CatalogManager.Instance.TryUnlock(_data);
}
