using UnityEngine;

/// <summary>
/// 1층/2층 사이 텔레포트 가구. 페어 stair와 짝을 이룸.
/// Customer/Server FSM이 stair에 접근 → 페어로 텔레포트.
/// </summary>
public class Stair : MonoBehaviour
{
    [Header("이 stair가 속한 floor")]
    [SerializeField] private FloorIndex floor = FloorIndex.Floor1;

    [Header("짝 stair (인스펙터에서 수동 연결)")]
    [SerializeField] private Stair pairStair;

    public FloorIndex Floor => floor;
    public Stair PairStair => pairStair;
    public bool HasPair => pairStair != null;

    private void OnEnable()
    {
        StairManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        StairManager.Instance?.Unregister(this);
    }

    /// <summary>이 stair의 인접 walkable 셀 (요청자가 stair에 접근할 때 머무는 위치).</summary>
    public Vector3 GetApproachPos(PathRole role, Vector3 fromWorld)
    {
        return GridManager.Instance.GetFurnitureApproachPosition(transform.position, role, fromWorld);
    }

    /// <summary>페어 stair의 approach 위치 — 텔레포트 후 도착할 위치.</summary>
    public Vector3 GetTeleportLandingPos(PathRole role, Vector3 fromWorld)
    {
        if (pairStair == null) return transform.position;
        return pairStair.GetApproachPos(role, fromWorld);
    }
}
