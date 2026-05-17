using UnityEngine;

public class Staff : MonoBehaviour
{
    [SerializeField] private StaffData data;
    [SerializeField] private Counter assignedCounter;   // null = 미배정(대기 중)
    [SerializeField] private int id;

    public StaffData Data => data;
    public Counter AssignedCounter => assignedCounter;
    public int Id => id;
    public bool IsAssigned => assignedCounter != null;

    public void Init(StaffData data, int id)
    {
        this.data = data;
        this.id = id;
        this.assignedCounter = null;
    }

    public void AssignTo(Counter counter) => assignedCounter = counter;
    public void Unassign() => assignedCounter = null;

    // 추후 이동 및 상태머신 구현 예정 (#5)
}
