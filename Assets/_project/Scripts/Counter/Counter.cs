using UnityEngine;

public class Counter : MonoBehaviour
{
    [SerializeField] private Transform servicePos;
    [SerializeField] private Transform staffPos;

    public Transform ServicePos => servicePos;
    public Transform StaffPos => staffPos;

    private bool _isOccupied;
    public bool IsOccupied => _isOccupied;

    private Customer _waitingCustomer;
    public Customer WaitingCustomer => _waitingCustomer;

    private ServerStaff _servicingServer;
    public ServerStaff ServicingServer => _servicingServer;
    public bool HasServicingServer => _servicingServer != null;

    private void Awake()
    {
        CounterManager.Instance.RegisterCounter(this);
    }

    private void OnDestroy()
    {
        if (CounterManager.Instance != null)
            CounterManager.Instance.UnregisterCounter(this);
    }

    public void Reserve()
    {
        _isOccupied = true;
    }

    public void OnCustomerArrived(Customer c)
    {
        _isOccupied = true;
        _waitingCustomer = c;
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

    public bool TryClaim(ServerStaff server)
    {
        if (_servicingServer != null) return false;
        _servicingServer = server;
        return true;
    }

    public void ReleaseClaim(ServerStaff server)
    {
        if (_servicingServer == server) _servicingServer = null;
    }
}
