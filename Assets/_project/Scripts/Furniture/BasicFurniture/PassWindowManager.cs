using System.Collections.Generic;
using UnityEngine;

public class PassWindowManager : MonoBehaviour
{
    public static PassWindowManager Instance;

    private readonly List<PassWindow> passWindows = new();

    public IReadOnlyList<PassWindow> PassWindows => passWindows;
    public int Count => passWindows.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(PassWindow pw)
    {
        if (!passWindows.Contains(pw)) passWindows.Add(pw);
    }

    public void Unregister(PassWindow pw) => passWindows.Remove(pw);

    public bool HasPendingOrder()
    {
        foreach (var pw in passWindows)
            if (pw.HasPendingOrder()) return true;
        return false;
    }

    public bool HasReadyFood()
    {
        foreach (var pw in passWindows)
            if (pw.HasReadyFood()) return true;
        return false;
    }

    public bool HasReadyHallFood()
    {
        foreach (var pw in passWindows)
            if (pw.HasReadyHallFood()) return true;
        return false;
    }

    public bool HasReadyDeliveryFood()
    {
        foreach (var pw in passWindows)
            if (pw.HasReadyDeliveryFood()) return true;
        return false;
    }

    public Order DequeueOrder()
    {
        foreach (var pw in passWindows)
            if (pw.HasPendingOrder()) return pw.DequeueOrder();
        return null;
    }

    public Food PickupHallFood()
    {
        foreach (var pw in passWindows)
        {
            var f = pw.PickupHallFood();
            if (f != null) return f;
        }
        return null;
    }

    public Food PickupDeliveryFood()
    {
        foreach (var pw in passWindows)
        {
            var f = pw.PickupDeliveryFood();
            if (f != null) return f;
        }
        return null;
    }

    public void SubmitOrder(Order order)
    {
        if (passWindows.Count == 0) return;
        passWindows[0].SubmitOrder(order);
    }

    public void PlaceFood(Food food)
    {
        if (passWindows.Count == 0) return;
        passWindows[0].PlaceFood(food);
    }

    public Transform GetFirstPassWindowTransform()
    {
        return passWindows.Count > 0 ? passWindows[0].transform : null;
    }

    public Vector3 GetApproachPosition(PathRole role)
    {
        if (passWindows.Count == 0) return Vector3.zero;
        return GridManager.Instance.GetFurnitureApproachPosition(passWindows[0].transform.position, role);
    }
}
