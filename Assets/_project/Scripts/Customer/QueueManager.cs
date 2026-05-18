using System.Collections.Generic;
using UnityEngine;

public class QueueManager : MonoBehaviour
{
    [SerializeField] private Transform queueAnchor; // 맨 첫 손님 줄 시작점
    [SerializeField] private float spacing = 1f; // 손님 줄 간격
    [SerializeField] private int maxCapacity = 5;
    private List<Customer> queue = new(); // 현재 줄서고 있는 손님
    public bool HasSpace => queue.Count < maxCapacity;
    public int Count => queue.Count;
    public void Dequeue(Customer c) => queue.Remove(c);
    public bool IsFront(Customer c) => queue.Count > 0 && queue[0] == c;

    public bool TryEnqueue(Customer c)
    {
        if (!HasSpace) return false;
        queue.Add(c);
        return true;
    }

    public Vector3 GetSlotPosition(Customer c)
    {
        int idx = queue.IndexOf(c);
        return queueAnchor.position + -queueAnchor.up * spacing * idx;
    }
}
