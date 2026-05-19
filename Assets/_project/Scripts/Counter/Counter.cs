using UnityEngine;

public class Counter : MonoBehaviour
{
    [SerializeField] private ServerStaff assignedStaff;   // null = 빈 카운터
    [SerializeField] private Transform servicePos;        // 인스펙터 연결
    [SerializeField] private Transform staffPos;          // 인스펙터 연결

    public Transform ServicePos => servicePos;
    public Transform StaffPos => staffPos;
    public ServerStaff AssignedStaff => assignedStaff;
    public bool IsEmpty => assignedStaff == null;

    public void AssignStaff(ServerStaff staff) => assignedStaff = staff;
    public void UnassignStaff() => assignedStaff = null;

    private bool _isOccupied;
    public bool IsOccupied => _isOccupied;

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
        // Server가 IDLE에서 폴링해서 가져감 (push 알림 없음)
    }
    public void OnCustomerPaid(int price)
    {
        _isOccupied = false;
        _waitingCustomer = null;
        if (price > 0)
        {
            MoneySystem.Instance.Earn(price);
        }
    }
}
