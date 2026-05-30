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

    /// <summary>
    /// 지금 실제로 만들 수 있는 메뉴인지. (해금 ≠ 제조 가능)
    /// 도구가 필요 없거나(menu.tool == null), 필요한 조리도구가 씬에 설치돼 있어야 true.
    /// 해금만 되고 도구 미설치인 메뉴를 손님이 주문해 무한대기에 빠지는 것 방지.
    /// </summary>
    public bool IsMakeable(MenuData menu)
    {
        if (menu == null) return false;
        if (menu.tool == null) return true;   // 조리도구 불필요 메뉴 (예: 음료)
        return CookingToolManager.Instance != null
            && CookingToolManager.Instance.GetToolInstance(menu.tool.toolType) != null;
    }

    /// <summary>설치된 조리도구로 만들 수 있는 해금 메뉴가 하나라도 있는가.</summary>
    public bool HasAnyMakeable()
    {
        foreach (var m in UnlockedMenus)
            if (IsMakeable(m)) return true;
        return false;
    }

    /// <summary>
    /// 만들 수 있는(IsMakeable) 메뉴 중에서 가중치 랜덤 픽.
    /// 만들 수 있는 메뉴가 하나도 없으면 null (호출측에서 "주문 없음" 처리).
    /// </summary>
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
        foreach (var m in unlocked) if (IsMakeable(m)) total += m.spawnWeight;
        if (total <= 0f) return null;   // 만들 수 있는 메뉴 없음 (도구 미설치) 또는 가중치 합 0

        float r = Random.Range(0f, total);
        float acc = 0;
        MenuData last = null;
        foreach (var m in unlocked)
        {
            if (!IsMakeable(m)) continue;
            acc += m.spawnWeight;
            last = m;
            if (r <= acc) return m;
        }
        return last;
    }
}
