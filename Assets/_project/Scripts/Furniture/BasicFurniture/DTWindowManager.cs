using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DT Order/Pickup 창구 등록 매니저. Counter/Seat 매니저 패턴 동일.
/// </summary>
public class DTWindowManager : MonoBehaviour
{
    private static DTWindowManager _instance;
    public static DTWindowManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<DTWindowManager>();
            return _instance;
        }
    }

    private readonly List<DTOrderWindow> orderWindows = new();
    private readonly List<DTPickupWindow> pickupWindows = new();

    public IReadOnlyList<DTOrderWindow> OrderWindows => orderWindows;
    public IReadOnlyList<DTPickupWindow> PickupWindows => pickupWindows;

    public DTOrderWindow FirstOrderWindow => orderWindows.Count > 0 ? orderWindows[0] : null;
    public DTPickupWindow FirstPickupWindow => pickupWindows.Count > 0 ? pickupWindows[0] : null;

    public int OrderWindowCount => orderWindows.Count;
    public int PickupWindowCount => pickupWindows.Count;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    public void RegisterOrder(DTOrderWindow w)
    {
        if (!orderWindows.Contains(w)) orderWindows.Add(w);
    }

    public void UnregisterOrder(DTOrderWindow w) => orderWindows.Remove(w);

    public void RegisterPickup(DTPickupWindow w)
    {
        if (!pickupWindows.Contains(w)) pickupWindows.Add(w);
    }

    public void UnregisterPickup(DTPickupWindow w) => pickupWindows.Remove(w);

    /// <summary>
    /// 응대 안 받은 차가 있는 OrderWindow 찾기.
    /// ServerStaff가 IDLE 우선순위에서 호출.
    /// </summary>
    public DTOrderWindow GetOrderWindowWithUnservedCar()
    {
        foreach (var w in orderWindows)
        {
            if (w.HasWaitingCar && !w.HasServicingServer)
                return w;
        }
        return null;
    }

    /// <summary>음식이 비치된 PickupWindow가 있나 (차가 가져갈 수 있는 상태).</summary>
    public bool HasAnyPickupWindowWithReadyFood()
    {
        foreach (var w in pickupWindows)
            if (w.PlacedFoodCount > 0) return true;
        return false;
    }
}
