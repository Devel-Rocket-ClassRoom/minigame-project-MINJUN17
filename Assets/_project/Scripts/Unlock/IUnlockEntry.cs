using UnityEngine;
using UnityEngine.Localization;

public enum UnlockCategory { Marketing, Food, MapExpansion, Furniture, Recruitment }
public enum CurrencyType   { Satisfaction, Money }

/// <summary>
/// CellZone 에 "조건 없음" 을 추가한 enum.
/// Inspector 에서 None / Kitchen / Hall / RiderRoom 선택용.
/// </summary>
public enum ZonePrerequisite { None, Kitchen, Hall, RiderRoom }

public static class ZonePrerequisiteExtensions
{
    /// <summary>None 이면 항상 true, 아니면 GridManager 에 해당 zone 활성 셀이 있는지.</summary>
    public static bool IsSatisfied(this ZonePrerequisite p)
    {
        if (p == ZonePrerequisite.None) return true;
        if (GridManager.Instance == null) return false;

        CellZone zone = p switch
        {
            ZonePrerequisite.Kitchen   => CellZone.Kitchen,
            ZonePrerequisite.Hall      => CellZone.Hall,
            ZonePrerequisite.RiderRoom => CellZone.RiderRoom,
            _ => CellZone.Kitchen
        };
        return GridManager.Instance.GetWalkableCellsInZone(zone).Count > 0;
    }
}

/// <summary>
/// 해금 패널의 슬롯 한 칸이 표시하는 데이터 인터페이스.
/// MarketingData, MenuData, FurnitureData, ExpansionStageData 를 어댑터로 감싸서 통일된 형태로 노출.
/// </summary>
public interface IUnlockEntry
{
    LocalizedString DisplayName { get; }
    LocalizedString Description { get; }
    Sprite Icon                 { get; }
    long Cost                   { get; }
    CurrencyType Currency       { get; }
    bool IsVisible              { get; }   // prerequisite 만족 여부
    bool IsPurchased            { get; }   // 영구 해금 항목이 이미 풀렸는지 (마케팅은 항상 false)
    bool CanAfford              { get; }   // 잔액 충분?
    bool Purchase();
}
