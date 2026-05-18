using UnityEngine;

public class Counter : MonoBehaviour
{
    [SerializeField] private Staff assignedStaff;   // null = 빈 카운터

    public Staff AssignedStaff => assignedStaff;
    public bool IsEmpty => assignedStaff == null;

    public void AssignStaff(Staff staff) => assignedStaff = staff;
    public void UnassignStaff() => assignedStaff = null;
    private bool _isOccupied;
    public bool IsOccupied => _isOccupied; // 직원 없으면 작동x

    public void OnCustomerArrived()
    {
        _isOccupied = true;
        // 해당 손님의 음식 만들러 직원 이동 로직
    }

    public void OnCustomerPaid()
    {
        _isOccupied = false;
        // 제품 금액 만큼 결제
    }


    // 점유/손님 관련은 #3에서 추가
}
