using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 해금 상태는 CatalogManager로 이관. 여기서는 씬에 배치된 인스턴스 추적만.
/// </summary>
public class CookingToolManager : MonoBehaviour
{
    public static CookingToolManager Instance;

    private readonly List<CookingToolInstance> _instances = new();    // 씬에 배치된 인스턴스

    public IEnumerable<CookingToolData> UnlockedTools =>
        CatalogManager.Instance != null
            ? CatalogManager.Instance.UnlockedFurniture.OfType<CookingToolData>()
            : Enumerable.Empty<CookingToolData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // === 인스턴스 추적 ===
    public void Register(CookingToolInstance inst)
    {
        if (!_instances.Contains(inst)) _instances.Add(inst);
    }
    public void Unregister(CookingToolInstance inst)
    {
        _instances.Remove(inst);
    }
    public Transform GetToolTransform(ToolType type)
    {
        foreach (var inst in _instances)
            if (inst.data != null && inst.data.toolType == type) return inst.transform;
        return null;
    }

    // 요리사가 도구 옆에 설 수 있는 인접 walkable 셀 위치 반환 (풋프린트 전체 고려)
    public Vector3 GetToolApproachPosition(ToolType type, Vector3 from)
    {
        foreach (var inst in _instances)
        {
            if (inst.data == null || inst.data.toolType != type) continue;
            return GridManager.Instance.GetFurnitureApproachPosition(inst.transform.position, PathRole.Cook, from);
        }
        return Vector3.zero;
    }
}
