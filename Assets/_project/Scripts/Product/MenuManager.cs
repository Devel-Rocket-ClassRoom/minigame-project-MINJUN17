using System.Collections.Generic;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    [SerializeField] private List<MenuData> startingMenus;  // 처음부터 풀려있는 메뉴
    private readonly List<MenuData> _unlockedMenus = new();

    public IReadOnlyList<MenuData> UnlockedMenus => _unlockedMenus;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _unlockedMenus.AddRange(startingMenus);
    }

    public bool Unlock(MenuData menu)
    {
        if (_unlockedMenus.Contains(menu)) return false;
        if (!SatisfactionSystem.Instance.CanAfford(menu.satisfactionUnlock)) return false;

        SatisfactionSystem.Instance.Spend(menu.satisfactionUnlock);
        _unlockedMenus.Add(menu);

        if (menu.tool != null)
            CookingToolManager.Instance.UnlockTool(menu.tool);

        return true;
    }

    public MenuData PickRandomByWeight()
    {
        float total = 0;
        foreach (var m in _unlockedMenus) total += m.spawnWeight;
        float r = Random.Range(0f, total);
        float acc = 0;
        foreach (var m in _unlockedMenus)
        {
            acc += m.spawnWeight;
            if (r <= acc) return m;
        }
        return _unlockedMenus[_unlockedMenus.Count - 1];
    }
}