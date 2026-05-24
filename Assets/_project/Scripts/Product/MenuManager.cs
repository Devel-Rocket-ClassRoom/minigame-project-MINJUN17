using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 해금 상태는 CatalogManager가 보관. MenuManager는 메뉴 가중치 픽 같은 도메인 로직만.
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public IReadOnlyCollection<MenuData> UnlockedMenus =>
        CatalogManager.Instance != null
            ? CatalogManager.Instance.UnlockedMenus
            : System.Array.Empty<MenuData>();

    /// <summary>상점/디버그에서 만족도 해금 트리거. 내부는 CatalogManager 위임.</summary>
    public bool Unlock(MenuData menu)
        => CatalogManager.Instance != null && CatalogManager.Instance.TryUnlock(menu);

    public MenuData PickRandomByWeight()
    {
        var unlocked = UnlockedMenus;
        if (unlocked.Count == 0)
        {
            Debug.LogWarning("[MenuManager] PickRandomByWeight 호출됐지만 해금된 메뉴가 0개 — " +
                             "씬에 CatalogManager 있는지 / Starting Menus 채워졌는지 확인");
            return null;
        }

        float total = 0;
        foreach (var m in unlocked) total += m.spawnWeight;
        if (total <= 0f)
        {
            Debug.LogWarning("[MenuManager] 해금된 메뉴는 있지만 spawnWeight 합이 0 — MenuData.spawnWeight 확인");
            // 폴백: 첫 메뉴 반환 (null보단 낫게)
            foreach (var m in unlocked) return m;
            return null;
        }

        float r = Random.Range(0f, total);
        float acc = 0;
        MenuData last = null;
        foreach (var m in unlocked)
        {
            acc += m.spawnWeight;
            last = m;
            if (r <= acc) return m;
        }
        return last;
    }
}
