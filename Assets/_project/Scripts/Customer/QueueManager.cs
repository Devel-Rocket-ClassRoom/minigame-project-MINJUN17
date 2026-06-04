using System.Collections.Generic;
using UnityEngine;

public class QueueManager : MonoBehaviour
{
    [Header("줄 경로 (앞→뒤 순서로 점 배치. 한 번 꺾으려면 3개: 맨앞 / 꺾는 지점 / 끝)")]
    [Tooltip("비우면 기존 직선(월드 -0.5,1.5 에서 -X 방향)으로 폴백")]
    [SerializeField] private Transform[] pathPoints;
    [Tooltip("손님 사이 간격 (월드 단위)")]
    [SerializeField] private float slotSpacing = 1f;
    [Tooltip("화면에 펼쳐 보일 최대 인원. 초과분은 맨 뒤에 겹쳐 대기")]
    [SerializeField] private int visibleSlots = 7;

    // 경로 미지정 시 폴백 직선 좌표 (기존 동작)
    private const float FallbackSlotY  = 1.5f;
    private const float FallbackFirstX = -0.5f;

    private readonly List<Customer> queue = new();

    public int Count => queue.Count;
    public bool HasSpace => true; // 무제한 큐 — 항상 받음
    public void Dequeue(Customer c) => queue.Remove(c);
    public bool IsFront(Customer c) => queue.Count > 0 && queue[0] == c;

    public bool TryEnqueue(Customer c)
    {
        if (queue.Contains(c)) return false;
        queue.Add(c);
        return true;
    }

    public Vector3 GetSlotPosition(Customer c)
    {
        int idx = queue.IndexOf(c);
        if (idx < 0) return Vector3.zero;
        int clamped = Mathf.Min(idx, Mathf.Max(1, visibleSlots) - 1);   // 초과분은 마지막 슬롯에 겹쳐 대기
        return PointAlongPath(clamped * slotSpacing);
    }

    /// <summary>경로가 설정돼 있는가 (start/mid/end 등 2점 이상).</summary>
    public bool HasPath => pathPoints != null && pathPoints.Length >= 2;

    /// <summary>
    /// 카운터 접근 경로를 도어(end) → 코너(mid) → 맨앞(start) 순서의 월드좌표로 반환.
    /// pathPoints는 앞→뒤(start→end) 순서라 뒤집어서 반환한다. 경로 미설정 시 빈 리스트.
    /// </summary>
    public List<Vector3> GetApproachPath()
    {
        var list = new List<Vector3>();
        if (pathPoints == null) return list;
        for (int i = pathPoints.Length - 1; i >= 0; i--)
            if (pathPoints[i] != null) list.Add(pathPoints[i].position);
        return list;
    }

    /// <summary>
    /// 손님이 도어(end)에서 자기 줄 슬롯까지 폴리라인을 따라 지날 월드 좌표 리스트.
    /// 맨 끝 원소가 슬롯(최종 대기 위치). 경로 미설정 시 빈 리스트.
    /// 예) 맨앞 손님: [end, mid, start]  /  뒤쪽 손님: [end, (mid,) 슬롯]
    /// </summary>
    public List<Vector3> GetWalkPathToSlot(Customer c)
    {
        var list = new List<Vector3>();
        if (!HasPath) return list;

        int idx = queue.IndexOf(c);
        if (idx < 0) idx = 0;
        int clamped = Mathf.Min(idx, Mathf.Max(1, visibleSlots) - 1);
        float slotArc = clamped * slotSpacing;   // start(맨앞) 기준 arc 거리

        int n = pathPoints.Length;
        // 각 정점의 start 기준 누적 arc 거리
        var arc = new float[n];
        for (int i = 1; i < n; i++)
        {
            float seg = (pathPoints[i] != null && pathPoints[i - 1] != null)
                ? Vector3.Distance(pathPoints[i - 1].position, pathPoints[i].position) : 0f;
            arc[i] = arc[i - 1] + seg;
        }

        // 도어(끝)→앞 순서로, 슬롯보다 도어쪽(arc가 더 큰)에 있는 정점만 통과
        for (int i = n - 1; i >= 0; i--)
            if (pathPoints[i] != null && arc[i] > slotArc + 0.0001f)
                list.Add(pathPoints[i].position);

        list.Add(PointAlongPath(slotArc));   // 마지막: 실제 슬롯 위치
        return list;
    }

    /// <summary>
    /// 이 손님의 슬롯이 카운터 다리(start~mid 구간)에 있는가.
    /// 코너(mid)보다 뒤쪽(도어쪽)에 선 손님은 false → 카운터를 바라보지 않음.
    /// </summary>
    public bool IsOnCounterLeg(Customer c)
    {
        if (pathPoints == null || pathPoints.Length < 2) return true;
        int idx = queue.IndexOf(c);
        if (idx < 0) idx = 0;
        int clamped = Mathf.Min(idx, Mathf.Max(1, visibleSlots) - 1);
        float slotArc = clamped * slotSpacing;
        float midArc = (pathPoints[0] != null && pathPoints[1] != null)
            ? Vector3.Distance(pathPoints[0].position, pathPoints[1].position) : 0f;
        return slotArc <= midArc + 0.0001f;
    }

    /// <summary>줄 맨 앞(카운터 다음) 지점 = start. 대기 손님이 바라볼 기준 방향.</summary>
    public Vector3 FrontPoint =>
        (pathPoints != null && pathPoints.Length > 0 && pathPoints[0] != null)
            ? pathPoints[0].position
            : new Vector3(FallbackFirstX, FallbackSlotY, 0f);

    /// <summary>경로(pathPoints)를 따라 dist 만큼 떨어진 지점. 경로 미지정 시 기존 직선으로 폴백.</summary>
    private Vector3 PointAlongPath(float dist)
    {
        if (pathPoints == null || pathPoints.Length < 2)
            return new Vector3(FallbackFirstX - dist, FallbackSlotY, 0f);

        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            if (pathPoints[i] == null || pathPoints[i + 1] == null) continue;
            Vector3 a = pathPoints[i].position;
            Vector3 b = pathPoints[i + 1].position;
            float seg = Vector3.Distance(a, b);
            if (seg <= 0.0001f) continue;
            if (dist <= seg) return Vector3.Lerp(a, b, dist / seg);
            dist -= seg;
        }
        // 경로 길이 초과 → 맨 끝 점에 겹쳐 대기
        return pathPoints[pathPoints.Length - 1].position;
    }

    /// <summary>영업 종료 시 큐 손님 전원 강제 퇴장.</summary>
    public void ForceLeaveAll()
    {
        var snapshot = new List<Customer>(queue);
        foreach (var c in snapshot)
            if (c != null) c.ForceLeave();
    }
}
