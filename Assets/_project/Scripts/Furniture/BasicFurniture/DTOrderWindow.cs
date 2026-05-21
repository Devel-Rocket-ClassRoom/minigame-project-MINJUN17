using UnityEngine;

/// <summary>
/// 드라이브 쓰루 주문 창구. 차가 와서 주문/결제 받는 곳.
/// Counter 변형 — 차 1대 슬롯 + 직원 응대 위치.
/// </summary>
public class DTOrderWindow : MonoBehaviour
{
    [Tooltip("차가 멈춰서 주문하는 위치")]
    [SerializeField] private Transform carSlot;
    [Tooltip("ServerStaff가 응대하러 오는 위치 (그리드 안쪽)")]
    [SerializeField] private Transform staffPos;

    public Transform CarSlot => carSlot;
    public Transform StaffPos => staffPos;

    private DTCustomer _waitingCar;
    public DTCustomer WaitingCar => _waitingCar;
    public bool HasWaitingCar => _waitingCar != null;

    private ServerStaff _servicingServer;
    public ServerStaff ServicingServer => _servicingServer;
    public bool HasServicingServer => _servicingServer != null;

    private void Awake()
    {
        if (DTWindowManager.Instance != null)
            DTWindowManager.Instance.RegisterOrder(this);
    }

    private void OnDestroy()
    {
        if (DTWindowManager.Instance != null)
            DTWindowManager.Instance.UnregisterOrder(this);
    }

    public void OnCarArrived(DTCustomer car)
    {
        _waitingCar = car;
    }

    public void OnCarLeft()
    {
        _waitingCar = null;
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
