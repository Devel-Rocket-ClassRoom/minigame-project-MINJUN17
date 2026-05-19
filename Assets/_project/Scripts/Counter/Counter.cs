using Unity.VisualScripting;
using UnityEngine;

public class Counter : MonoBehaviour
{
    [SerializeField] private Staff assignedStaff;   // null = 빈 카운터
    [SerializeField] private Transform servicePos;  // 인스펙터 연결
    [SerializeField] private Transform staffPos;    // 인스펙터 연결

    public Transform ServicePos => servicePos;
    public Transform StaffPos => staffPos;
    public Staff AssignedStaff => assignedStaff;
    public bool IsEmpty => assignedStaff == null;

    public void AssignStaff(Staff staff) => assignedStaff = staff;
    public void UnassignStaff() => assignedStaff = null;
    private bool _isOccupied;// 현재 카운터에 손님이 있는지
    public bool IsOccupied => _isOccupied; // 직원 없으면 작동x

    private Customer _waitingCustomer;
    public Customer WaitingCustomer => _waitingCustomer;

    private void Awake()
    {
        CounterManager.Instance.RegisterCounter(this);
    }

    public void Reserve()
    {
        // 손님이 카운터로 출발하는 순간 호출 — 다른 손님이 못 가져가게 즉시 점유 표시
        _isOccupied = true;

    }

    public void OnCustomerArrived(Customer c)
    {
        if (IsEmpty) return;
        _isOccupied = true;
        _waitingCustomer = c;
        assignedStaff.OnOrderReceived();
    }

    public void OnFoodReady()
    {
        if (_waitingCustomer != null)
            _waitingCustomer.OnFoodReady();
    }

    private int _currentPrice; // 아직 상품 시스템 미구현 더미데이터

    public void ReceiveOrder(int price) => _currentPrice = price;

    public void OnCustomerPaid()
    {
        _isOccupied = false;
        _waitingCustomer = null;
        if (_currentPrice > 0)
        {
            MoneySystem.Instance.Earn(_currentPrice);
            _currentPrice = 0;
        }
    }
}
