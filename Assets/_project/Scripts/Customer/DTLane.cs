using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드라이브 쓰루 차로. waypoint 시퀀스 + 활성 차 큐 관리.
/// 인스펙터에서 자식 Transform들을 waypoints 배열에 순서대로 드래그하고,
/// orderStopIndex/pickupStopIndex로 어느 waypoint가 정거장인지 지정.
/// </summary>
public class DTLane : MonoBehaviour
{
    public static DTLane Instance;

    [Header("Waypoints (Entry → ... → OrderStop → ... → PickupStop → ... → Exit)")]
    [SerializeField] private Transform[] waypoints;

    [Tooltip("주문창구 슬롯이 되는 waypoint 인덱스")]
    [SerializeField] private int orderStopIndex = -1;
    [Tooltip("픽업창구 슬롯이 되는 waypoint 인덱스")]
    [SerializeField] private int pickupStopIndex = -1;

    [Tooltip("차로에 동시에 존재 가능한 차 최대 수")]
    [SerializeField] private int maxCars = 5;

    private readonly List<DTCustomer> _activeCars = new();
    public IReadOnlyList<DTCustomer> ActiveCars => _activeCars;
    public int ActiveCarCount => _activeCars.Count;
    public bool IsFull => _activeCars.Count >= maxCars;

    public int WaypointCount => waypoints != null ? waypoints.Length : 0;
    public int OrderStopIndex => orderStopIndex;
    public int PickupStopIndex => pickupStopIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // GridManager.Awake보다 DTLane이 늦게 Awake되면 초기 카메라에 lane이 누락되므로 한 번 재정렬
        GridManager.Instance?.CenterCameraOnActiveGrid();
    }

    public Transform GetWaypoint(int index)
    {
        if (waypoints == null || index < 0 || index >= waypoints.Length) return null;
        return waypoints[index];
    }

    public bool IsOrderStop(int index) => index == orderStopIndex;
    public bool IsPickupStop(int index) => index == pickupStopIndex;
    public bool IsExit(int index) => index == WaypointCount - 1;  // 마지막이 Exit
    public bool IsEntry(int index) => index == 0;

    public void RegisterCar(DTCustomer car)
    {
        if (!_activeCars.Contains(car)) _activeCars.Add(car);
    }

    public void UnregisterCar(DTCustomer car) => _activeCars.Remove(car);

    /// <summary>해당 waypoint 인덱스를 점유한 다른 차가 있는지.</summary>
    public bool IsWaypointOccupiedByOther(int index, DTCustomer asker)
    {
        foreach (var c in _activeCars)
        {
            if (c == asker) continue;
            if (c.CurrentWaypointIndex == index) return true;
        }
        return false;
    }

    /// <summary>차로 bbox 반환 (6단계 카메라 합집합용).</summary>
    public Bounds GetVisibleBounds()
    {
        if (waypoints == null || waypoints.Length == 0)
            return new Bounds(transform.position, Vector3.zero);

        Bounds b = new Bounds(waypoints[0].position, Vector3.zero);
        foreach (var w in waypoints)
            if (w != null) b.Encapsulate(w.position);
        b.Expand(1f);   // 차 sprite 여유분
        return b;
    }
}
