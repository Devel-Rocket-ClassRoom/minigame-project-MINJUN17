using UnityEngine;
using UnityEngine.Localization;

/// <summary>MenuData → IUnlockEntry 어댑터. 1회 영구 해금, 만족도 차감.</summary>
public class FoodUnlockEntry : IUnlockEntry
{
    private readonly MenuData _data;
    public FoodUnlockEntry(MenuData data) { _data = data; }

    public MenuData Data => _data;

    public LocalizedString DisplayName => _data.menuName;
    public LocalizedString Description => _data.description;
    public Sprite Icon                 => _data.foodSprite;
    public long Cost                   => _data.satisfactionUnlock;
    public CurrencyType Currency       => CurrencyType.Satisfaction;

    public bool IsVisible    => _data != null
                             && (_data.tool == null
                                 || (CatalogManager.Instance != null
                                     && CatalogManager.Instance.IsUnlocked(_data.tool)));
    public bool IsPurchased  => CatalogManager.Instance != null
                             && CatalogManager.Instance.IsUnlocked(_data);
    public bool CanAfford    => SatisfactionSystem.Instance != null
                             && SatisfactionSystem.Instance.CanAfford((int)Cost);

    public bool Purchase()
        => CatalogManager.Instance != null && CatalogManager.Instance.TryUnlock(_data);
}
