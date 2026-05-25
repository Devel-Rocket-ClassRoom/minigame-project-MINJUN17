using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ExpansionStageData → IUnlockEntry 어댑터. 1회 영구 해금, 돈 차감.
/// ExpansionManager 가 순차 진행이라 한 번에 하나만 존재 (NextStage).
/// </summary>
public class MapExpansionUnlockEntry : IUnlockEntry
{
    private readonly ExpansionStageData _data;
    public MapExpansionUnlockEntry(ExpansionStageData data) { _data = data; }

    public ExpansionStageData Data => _data;

    public LocalizedString DisplayName => _data.stageName;
    public LocalizedString Description => _data.description;
    public Sprite Icon                 => _data.icon;
    public long Cost                   => _data.unlockCost;
    public CurrencyType Currency       => CurrencyType.Money;

    public bool IsVisible    => _data != null;
    public bool IsPurchased  => false;  // NextStage 가 항상 다음 거라 "구매했다" 개념은 패널이 NextStage 바꿔서 처리
    public bool CanAfford    => MoneySystem.Instance != null
                             && MoneySystem.Instance.CanAfford(Cost);

    public bool Purchase()
        => ExpansionManager.Instance != null && ExpansionManager.Instance.TryExpand();
}
