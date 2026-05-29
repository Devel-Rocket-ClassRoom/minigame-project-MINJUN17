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

    [Header("진입/도착 지점 (1층 stair는 계단 발치, 2층 stair는 계단 꼭대기)")]
    [Tooltip("손님이 텔레포트 직전에 도달하는 셀, 그리고 텔레포트 후 도착 위치. " +
             "비워두면 transform.position. 자식 빈 GameObject로 만들어 원하는 셀 중앙에 위치시킴.")]
    [SerializeField] private Transform approachPoint;

    public FloorIndex Floor => floor;
    public Stair PairStair => pairStair;
    public bool HasPair => pairStair != null;

    private Vector3 ApproachPos => approachPoint != null ? approachPoint.position : transform.position;

    private void OnEnable()
    {
        StairManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        StairManager.Instance?.Unregister(this);
    }

    /// <summary>이 stair의 진입 위치 (요청자가 stair에 접근할 때 머무는 자리).</summary>
    public Vector3 GetApproachPos(PathRole role, Vector3 fromWorld) => ApproachPos;

    /// <summary>페어 stair의 진입 위치 — 텔레포트 후 도착 지점.</summary>
    public Vector3 GetTeleportLandingPos(PathRole role, Vector3 fromWorld)
    {
        if (pairStair == null) return transform.position;
        return pairStair.ApproachPos;
    }
}
