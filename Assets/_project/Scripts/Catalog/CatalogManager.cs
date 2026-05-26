using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 모든 가구/메뉴의 해금 상태를 한 곳에서 관리하는 단일 진실(SSoT).
/// - 가구(기본/조리도구 포함) + 메뉴 둘 다 다룸
/// - 만족도 해금: TryUnlock(...)
/// - 이벤트/연쇄 해금: ForceUnlock(...)
/// - 메뉴 해금 시 매칭 조리도구 자동 해금
/// - ExpansionManager.OnExpanded 구독해서 unlockOnExpansion 설정된 가구 자동 해금
/// 다른 매니저들(MenuManager/CookingToolManager/PhoneManager/DTSystem)은 위임만 한다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class CatalogManager : MonoBehaviour
{
    public static CatalogManager Instance;

    [Header("전체 카탈로그")]
    [SerializeField] private List<FurnitureData> allFurniture;
    [SerializeField] private List<MenuData> allMenus;

    [Header("시작 해금")]
    [SerializeField] private List<FurnitureData> startingFurniture;
    [SerializeField] private List<MenuData> startingMenus;

    private readonly HashSet<FurnitureData> _unlockedFurniture = new();
    private readonly HashSet<MenuData> _unlockedMenus = new();

    public event Action<FurnitureData> OnFurnitureUnlocked;
    public event Action<MenuData> OnMenuUnlocked;

    public IReadOnlyCollection<FurnitureData> UnlockedFurniture => _unlockedFurniture;
    public IReadOnlyCollection<MenuData> UnlockedMenus => _unlockedMenus;
    public IReadOnlyList<FurnitureData> AllFurniture => allFurniture;
    public IReadOnlyList<MenuData> AllMenus => allMenus;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (startingFurniture != null)
            foreach (var f in startingFurniture) if (f != null) _unlockedFurniture.Add(f);
        if (startingMenus != null)
            foreach (var m in startingMenus)     if (m != null) _unlockedMenus.Add(m);

        // 시작부터 풀린 메뉴의 조리도구도 자동 해금
        foreach (var m in _unlockedMenus)
            if (m.tool != null) _unlockedFurniture.Add(m.tool);
    }

    private void Start()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded += OnExpanded;
    }

    private void OnDestroy()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded -= OnExpanded;
    }

    // ===== 조회 =====
    public bool IsUnlocked(FurnitureData f) => f != null && _unlockedFurniture.Contains(f);
    public bool IsUnlocked(MenuData m)      => m != null && _unlockedMenus.Contains(m);

    public IEnumerable<FurnitureData> UnlockedByZone(PlacementZone zone)
        => _unlockedFurniture.Where(f => f.zone == zone);

    // ===== 만족도 해금 (상점 UI에서 호출) =====
    public bool TryUnlock(FurnitureData f)
    {
        if (f == null || _unlockedFurniture.Contains(f)) return false;
        if (SatisfactionSystem.Instance == null) return false;
        if (!SatisfactionSystem.Instance.Spend(f.satisfactionUnlock)) return false;
        ForceUnlock(f);
        return true;
    }

    public bool TryUnlock(MenuData m)
    {
        if (m == null || _unlockedMenus.Contains(m)) return false;
        if (SatisfactionSystem.Instance == null) return false;
        if (!SatisfactionSystem.Instance.Spend(m.satisfactionUnlock)) return false;
        ForceUnlock(m);
        return true;
    }

    // ===== 이벤트/연쇄 해금 (비용 무시) =====
    public void ForceUnlock(FurnitureData f)
    {
        if (f == null) return;
        if (!_unlockedFurniture.Add(f)) return;
        OnFurnitureUnlocked?.Invoke(f);
    }

    public void ForceUnlock(MenuData m)
    {
        if (m == null) return;
        if (!_unlockedMenus.Add(m)) return;
        OnMenuUnlocked?.Invoke(m);
        if (m.tool != null) ForceUnlock(m.tool);   // 메뉴 → 매칭 도구 자동
    }

    private void OnExpanded(ExpansionStageData stage)
    {
        if (allFurniture == null) return;
        foreach (var f in allFurniture)
            if (f != null && f.unlockOnExpansion == stage) ForceUnlock(f);
    }

    // ─── Save / Load ───
    public CatalogData ToData()
    {
        var furn = new List<string>();
        foreach (var f in _unlockedFurniture)
            if (f != null && !string.IsNullOrEmpty(f.SaveId)) furn.Add(f.SaveId);

        var menus = new List<string>();
        foreach (var m in _unlockedMenus)
            if (m != null && !string.IsNullOrEmpty(m.SaveId)) menus.Add(m.SaveId);

        return new CatalogData { unlockedFurnitureIds = furn, unlockedMenuIds = menus };
    }

    public void FromData(CatalogData data)
    {
        // 시작 해금은 Awake에서 이미 채워졌음. 추가로 세이브된 해금만 더한다.
        if (data == null) return;

        if (data.unlockedFurnitureIds != null)
        {
            foreach (var id in data.unlockedFurnitureIds)
            {
                var f = SaveIdRegistry.GetById<FurnitureData>(id);
                if (f != null) _unlockedFurniture.Add(f);
            }
        }
        if (data.unlockedMenuIds != null)
        {
            foreach (var id in data.unlockedMenuIds)
            {
                var m = SaveIdRegistry.GetById<MenuData>(id);
                if (m == null) continue;
                _unlockedMenus.Add(m);
                if (m.tool != null) _unlockedFurniture.Add(m.tool);   // 메뉴 → 매칭 도구 자동
            }
        }
        // 이벤트 발화 X — 로드 시점에는 UI가 직접 조회로 갱신
    }
}
