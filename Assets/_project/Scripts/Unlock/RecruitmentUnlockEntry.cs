using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// RecruitmentTierConfig → IUnlockEntry 어댑터. 중첩 구매 가능, 만족도 차감.
/// 월 1회 제한 — 이미 이번 달 구매했으면 CanAfford=false, 설명에 "(이번 달 구매 완료)" 부착.
/// </summary>
public class RecruitmentUnlockEntry : IUnlockEntry
{
    private readonly RecruitmentTierConfig _cfg;
    public RecruitmentUnlockEntry(RecruitmentTierConfig cfg) { _cfg = cfg; }

    public RecruitmentTierConfig Config => _cfg;

    public LocalizedString DisplayName => _cfg.displayName;
    public LocalizedString Description => _cfg.description;
    public Sprite Icon                 => _cfg.icon;
    public long Cost                   => _cfg.satisfactionCost;
    public CurrencyType Currency       => CurrencyType.Satisfaction;

    public bool IsVisible    => _cfg != null;
    public bool IsPurchased  => false;   // 중첩 가능 — 영영 사라지지 않음
    public bool CanAfford    => SatisfactionSystem.Instance != null
                             && SatisfactionSystem.Instance.CanAfford((int)Cost)
                             && IsPurchasableThisMonth;

    /// <summary>월 1회 제한 — 이번 달 아직 안 샀음.</summary>
    public bool IsPurchasableThisMonth =>
        StaffCandidatePool.Instance != null && StaffCandidatePool.Instance.CanPurchaseThisMonth;

    public bool Purchase()
        => StaffCandidatePool.Instance != null
        && StaffCandidatePool.Instance.PurchaseRecruitment(_cfg.tier);
}
