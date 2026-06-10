using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SaveId → ScriptableObject 매핑 캐시.
/// 매니저들(CatalogManager, StaffManager, ExpansionManager)이 보유한 SO 리스트에서 자동 수집.
/// 첫 GetById 호출 시 자동 Rebuild. 게임 도중 SO가 추가되면 Rebuild() 수동 호출.
/// </summary>
public static class SaveIdRegistry
{
    private static Dictionary<string, ScriptableObject> _byId;

    public static void Rebuild()
    {
        _byId = new Dictionary<string, ScriptableObject>();

        if (CatalogManager.Instance != null)
        {
            AddRange(CatalogManager.Instance.AllFurniture);
            AddRange(CatalogManager.Instance.AllMenus);
        }
        if (StaffManager.Instance != null)
        {
            AddRange(StaffManager.Instance.CookGrades);
            AddRange(StaffManager.Instance.ServerGrades);
            AddRange(StaffManager.Instance.RiderGrades);
        }
        if (ExpansionManager.Instance != null)
        {
            AddRange(ExpansionManager.Instance.Stages);
        }
        if (MarketingManager.Instance != null)
        {
            AddRange(MarketingManager.Instance.AllMarketing);
        }
        if (CustomerManager.Instance != null)
        {
            AddRange(CustomerManager.Instance.Pool);
        }
    }

    private static void AddRange<T>(IEnumerable<T> items) where T : ScriptableObject
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (item is ISaveIdentifiable ident && !string.IsNullOrEmpty(ident.SaveId))
            {
                if (_byId.ContainsKey(ident.SaveId))
                    _byId[ident.SaveId] = item;
                else
                    _byId[ident.SaveId] = item;
            }
        }
    }

    public static T GetById<T>(string id) where T : ScriptableObject
    {
        if (_byId == null) Rebuild();
        if (string.IsNullOrEmpty(id)) return null;
        return _byId.TryGetValue(id, out var so) ? so as T : null;
    }
}
