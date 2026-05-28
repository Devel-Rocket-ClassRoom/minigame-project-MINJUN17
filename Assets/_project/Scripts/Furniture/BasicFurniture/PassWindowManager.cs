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

    // ===== type별 generic 헬퍼 =====
    public bool HasReadyFood(OrderType type)
    {
        foreach (var pw in passWindows)
            if (pw.HasReadyFood(type)) return true;
        return false;
    }

    public Food ClaimFood(OrderType type, Staff claimer)
    {
        foreach (var pw in passWindows)
        {
            var f = pw.ClaimFood(type, claimer);
            if (f != null) return f;
        }
        return null;
    }

    // ===== 기존 호환 메서드 =====
    public bool HasReadyHallFood() => HasReadyFood(OrderType.Hall);
    public bool HasReadyDeliveryFood() => HasReadyFood(OrderType.Delivery);

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

    // 클레임/실제 픽업 분리: 시각은 픽업대에 머물고, 직원이 픽업대 도착 시 TakeFood로 들기
    public Food ClaimHallFood(Staff claimer)
    {
        foreach (var pw in passWindows)
        {
            var f = pw.ClaimHallFood(claimer);
            if (f != null) return f;
        }
        return null;
    }

    public Food ClaimDeliveryFood(Staff claimer)
    {
        foreach (var pw in passWindows)
        {
            var f = pw.ClaimDeliveryFood(claimer);
            if (f != null) return f;
        }
        return null;
    }

    public Food TakeFood(Food food)
    {
        foreach (var pw in passWindows)
        {
            var taken = pw.TakeFood(food);
            if (taken != null) return taken;
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

    public Vector3 GetApproachPosition(PathRole role, Vector3 from)
    {
        if (passWindows.Count == 0) return Vector3.zero;
        var pw = passWindows[0];
        // 명시 사용 위치가 있으면 우선 (없으면 자동 인접셀 계산)
        var up = pw.GetUsePoint(role);
        if (up != null) return up.position;
        return GridManager.Instance.GetFurnitureApproachPosition(pw.transform.position, role, from);
    }
}
