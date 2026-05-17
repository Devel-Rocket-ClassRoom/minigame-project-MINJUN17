using UnityEngine;

public class Counter : MonoBehaviour
{
    [SerializeField] private Staff assignedStaff;   // null = 빈 카운터

    public Staff AssignedStaff => assignedStaff;
    public bool IsEmpty => assignedStaff == null;

    public void AssignStaff(Staff staff) => assignedStaff = staff;
    public void UnassignStaff() => assignedStaff = null;

    // 점유/손님 관련은 #3에서 추가
}
