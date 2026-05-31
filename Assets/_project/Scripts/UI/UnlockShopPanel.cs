using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 마케팅 / 음식 / 맵확장 / 가구 4탭 해금 패널.
/// 탭별로 IUnlockEntry 목록을 만들어 슬롯에 뿌린다.
/// CatalogManager / MarketingManager / ExpansionManager 이벤트 구독으로 자동 갱신.
/// </summary>
public class UnlockShopPanel : MonoBehaviour
{
    [Header("탭 / 슬롯")]
    [SerializeField] private UnlockCategory currentCategory = UnlockCategory.Marketing;
    [SerializeField] private UnlockSlot slotPrefab;
    [SerializeField] private RectTransform content;

    [Header("빈 상태 메시지")]
    [SerializeField] private GameObject emptyMessage;   // "가능한 해금이 없습니다" GO

    [Header("통화 아이콘")]
    [SerializeField] private Sprite satisfactionIcon;   // ♥
    [SerializeField] private Sprite moneyIcon;          // 💰

    [Header("참조")]
    [SerializeField] private PlacementSystem placementSystem;

    private readonly List<UnlockSlot> _spawned = new();

    /// <summary>튜토리얼: 값이 있으면 모집 탭에서 이 등급만 노출 (null=전체).</summary>
    public static RecruitmentTier? TutorialOnlyTier;

    /// <summary>튜토리얼: 값이 있으면 가구 탭에서 이 가구(그릴 등)만 노출 (null=전체).</summary>
    public static FurnitureData TutorialOnlyFurniture;

    public UnlockCategory CurrentCategory => currentCategory;

    /// <summary>탭 버튼 OnClick — int 로 PoolCategory 값 전달.</summary>
    public void SetCategory(int categoryInt) => SetCategory((UnlockCategory)categoryInt);

    public void SetCategory(UnlockCategory cat)
    {
        currentCategory = cat;
        Refresh();
    }

    private void OnEnable()
    {
        SubscribeAll();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeAll();
    }

    private void SubscribeAll()
    {
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnFurnitureUnlocked += OnFurnitureUnlocked;
            CatalogManager.Instance.OnMenuUnlocked      += OnMenuUnlocked;
        }
        if (MarketingManager.Instance != null)
        {
            MarketingManager.Instance.OnMarketingPurchased += OnMarketingPurchased;
        }
        if (ExpansionManager.Instance != null)
        {
            ExpansionManager.Instance.OnExpanded += OnExpanded;
        }
        if (SatisfactionSystem.Instance != null)
        {
            SatisfactionSystem.Instance.OnSatisfactionChanged += OnSatisfactionChanged;
        }
        if (MoneySystem.Instance != null)
        {
            MoneySystem.Instance.OnMoneyChanged += OnMoneyChanged;
        }
        if (StaffCandidatePool.Instance != null)
        {
            StaffCandidatePool.Instance.OnPurchaseStateChanged += OnRecruitmentPurchaseStateChanged;
        }
    }

    private void UnsubscribeAll()
    {
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnFurnitureUnlocked -= OnFurnitureUnlocked;
            CatalogManager.Instance.OnMenuUnlocked      -= OnMenuUnlocked;
        }
        if (MarketingManager.Instance != null)
        {
            MarketingManager.Instance.OnMarketingPurchased -= OnMarketingPurchased;
        }
        if (ExpansionManager.Instance != null)
        {
            ExpansionManager.Instance.OnExpanded -= OnExpanded;
        }
        if (SatisfactionSystem.Instance != null)
        {
            SatisfactionSystem.Instance.OnSatisfactionChanged -= OnSatisfactionChanged;
        }
        if (MoneySystem.Instance != null)
        {
            MoneySystem.Instance.OnMoneyChanged -= OnMoneyChanged;
        }
        if (StaffCandidatePool.Instance != null)
        {
            StaffCandidatePool.Instance.OnPurchaseStateChanged -= OnRecruitmentPurchaseStateChanged;
        }
    }

    private void OnFurnitureUnlocked(FurnitureData _) => Refresh();
    private void OnMenuUnlocked(MenuData _)            => Refresh();
    private void OnMarketingPurchased(MarketingData _) => Refresh();
    private void OnExpanded(ExpansionStageData _)      => Refresh();
    private void OnSatisfactionChanged(int _)          => RefreshCanAffordOnly();
    private void OnMoneyChanged(long _)                => RefreshCanAffordOnly();
    /// <summary>월 1회 제한 토글 — 설명 텍스트가 바뀌므로 슬롯 재생성 필요.</summary>
    private void OnRecruitmentPurchaseStateChanged()   => Refresh();

    public void Refresh()
    {
        ClearSlots();

        var visible = GetEntries(currentCategory)
                      .Where(e => e.IsVisible && !e.IsPurchased)
                      .ToList();

        foreach (var e in visible)
        {
            var slot = Instantiate(slotPrefab, content);
            slot.Setup(e, satisfactionIcon, moneyIcon);
            _spawned.Add(slot);
        }

        if (emptyMessage != null) emptyMessage.SetActive(visible.Count == 0);
    }

    /// <summary>잔액만 바뀌었을 때는 슬롯 재생성 없이 색만 갱신.</summary>
    private void RefreshCanAffordOnly()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) _spawned[i].RefreshAffordability();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
    }

    private IEnumerable<IUnlockEntry> GetEntries(UnlockCategory cat)
    {
        switch (cat)
        {
            case UnlockCategory.Marketing:
                if (MarketingManager.Instance == null || MarketingManager.Instance.AllMarketing == null)
                    yield break;
                foreach (var d in MarketingManager.Instance.AllMarketing)
                    if (d != null) yield return new MarketingUnlockEntry(d);
                yield break;

            case UnlockCategory.Food:
                if (CatalogManager.Instance == null) yield break;
                foreach (var m in CatalogManager.Instance.AllMenus)
                {
                    if (m == null) continue;
                    if (m.satisfactionUnlock <= 0) continue;       // 시작 해금
                    yield return new FoodUnlockEntry(m);
                }
                yield break;

            case UnlockCategory.Furniture:
                if (CatalogManager.Instance == null) yield break;
                foreach (var f in CatalogManager.Instance.AllFurniture)
                {
                    if (f == null) continue;
                    if (f.satisfactionUnlock <= 0) continue;       // 시작 해금
                    if (f.unlockOnExpansion != null) continue;     // 확장으로 자동 해금되는 가구
                    if (f.fixedSingle && placementSystem != null && placementSystem.IsAlreadyPlaced(f)) continue; // 1회 설치 가구는 설치 후 숨김
                    if (TutorialOnlyFurniture != null && f != TutorialOnlyFurniture) continue;   // 튜토리얼: 그릴만
                    yield return f is CookingToolData ct
                        ? (IUnlockEntry)new CookingToolUnlockEntry(ct)
                        : new FurnitureUnlockEntry(f);
                }
                yield break;

            case UnlockCategory.MapExpansion:
                if (ExpansionManager.Instance == null || ExpansionManager.Instance.NextStage == null) yield break;
                yield return new MapExpansionUnlockEntry(ExpansionManager.Instance.NextStage);
                yield break;

            case UnlockCategory.Recruitment:
                if (StaffCandidatePool.Instance == null || StaffCandidatePool.Instance.TierConfigs == null)
                    yield break;
                foreach (var cfg in StaffCandidatePool.Instance.TierConfigs)
                {
                    if (cfg == null) continue;
                    if (TutorialOnlyTier.HasValue && cfg.tier != TutorialOnlyTier.Value) continue;  // 튜토리얼: 일반만
                    yield return new RecruitmentUnlockEntry(cfg);
                }
                yield break;
        }
    }
}
