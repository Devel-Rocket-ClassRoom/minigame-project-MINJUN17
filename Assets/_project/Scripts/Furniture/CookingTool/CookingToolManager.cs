using System.Collections.Generic;
using UnityEngine;

public class CookingToolManager : MonoBehaviour
{
    public static CookingToolManager Instance;

    [SerializeField] private List<CookingToolData> startingTools;     // 처음부터 해금
    private readonly List<CookingToolData> _unlockedTools = new();    // 해금된 도구 데이터
    private readonly List<CookingToolInstance> _instances = new();    // 씬에 배치된 인스턴스

    public IReadOnlyList<CookingToolData> UnlockedTools => _unlockedTools;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _unlockedTools.AddRange(startingTools);
    }

    // === 해금 ===
    public bool UnlockTool(CookingToolData tool)
    {
        if (_unlockedTools.Contains(tool)) return false;
        _unlockedTools.Add(tool);
        return true;
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
    public Vector3 GetToolApproachPosition(ToolType type)
    {
        foreach (var inst in _instances)
        {
            if (inst.data == null || inst.data.toolType != type) continue;
            return GridManager.Instance.GetFurnitureApproachPosition(inst.transform.position, PathRole.Cook);
        }
        return Vector3.zero;
    }
}