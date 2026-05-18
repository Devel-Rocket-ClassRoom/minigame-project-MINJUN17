using Unity.VisualScripting;
using UnityEngine;

public class Counter : MonoBehaviour
{
    [SerializeField] private Staff assignedStaff;   // null = 빈 카운터
    [SerializeField] private Transform servicePos;

    public Transform ServicePos => servicePos;
    public Staff AssignedStaff => assignedStaff;
    public bool IsEmpty => assignedStaff == null;

    public void AssignStaff(Staff staff) => assignedStaff = staff;
    public void UnassignStaff() => assignedStaff = null;
    private bool _isOccupied;// 현재 카운터에 손님이 있는지
    public bool IsOccupied => _isOccupied; // 직원 없으면 작동x
    public Vector3 CounterPos { get; private set; }
    
    public void Reserve()
    {
        // 손님이 카운터로 출발하는 순간 호출 — 다른 손님이 못 가져가게 즉시 점유 표시
        _isOccupied = true;
    }

    public void OnCustomerArrived()
    {
        if (IsEmpty) return;
        _isOccupied = true;
        // TODO: 직원 음식 준비 트리거 (#5)
    }

    private int _currentPrice; // 아직 상품 시스템 미구현 더미데이터

    public void ReceiveOrder(int price) => _currentPrice = price;

    public void OnCustomerPaid()
    {
        _isOccupied = false;
        if (_currentPrice > 0)
        {
            // TODO: #7 MoneySystem.Instance.Add(_currentPrice);
            Debug.Log($"[Counter] +{_currentPrice}원 결제");
            _currentPrice = 0;
        }
    }
}
