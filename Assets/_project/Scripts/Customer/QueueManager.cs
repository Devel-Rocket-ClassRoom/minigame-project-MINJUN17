using System.Collections.Generic;
using UnityEngine;

public class QueueManager : MonoBehaviour
{
    // 인도(사이드워크) y=1 라인 슬롯 7개 + 초과시 스폰점(-7,1) 겹침
    // 좌표는 코드 계산 — 셀 (-1,1)..(-7,1) → 월드 (-0.5, 1.5)..(-6.5, 1.5)
    private const int VisibleSlots = 7;
    private const float SlotY = 1.5f;
    private const float FirstSlotX = -0.5f;

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
        int clamped = Mathf.Min(idx, VisibleSlots - 1);   // 7번째 이후는 마지막 슬롯(=스폰)에 겹쳐 대기
        return new Vector3(FirstSlotX - clamped, SlotY, 0f);
    }

    /// <summary>영업 종료 시 큐 손님 전원 강제 퇴장.</summary>
    public void ForceLeaveAll()
    {
        var snapshot = new List<Customer>(queue);
        foreach (var c in snapshot)
            if (c != null) c.ForceLeave();
    }
}
