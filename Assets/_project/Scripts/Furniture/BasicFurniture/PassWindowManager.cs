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

    // === 직원이 호출하는 API ===
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

    public Order DequeueOrder()
    {
        foreach (var pw in passWindows)
            if (pw.HasPendingOrder()) return pw.DequeueOrder();
        return null;
    }

    public Food PickupFood()
    {
        foreach (var pw in passWindows)
            if (pw.HasReadyFood()) return pw.PickupFood();
        return null;
    }

    public void SubmitOrder(Order order)
    {
        // 1주차: 첫 번째 픽업대에 제출. 2주차에 분배 정책 도입 (가까운 곳, 빈 곳 등)
        if (passWindows.Count == 0) return;
        passWindows[0].SubmitOrder(order);
    }

    public void PlaceFood(Food food)
    {
        if (passWindows.Count == 0) return;
        passWindows[0].PlaceFood(food);
    }

    // 1주차 임시: 직원이 이동할 위치 (단일 픽업대 가정)
    public Transform GetFirstPassWindowTransform()
    {
        return passWindows.Count > 0 ? passWindows[0].transform : null;
    }

    // PassWindow 인접 셀 중 해당 역할이 걸을 수 있는 위치 반환 (풋프린트 전체 고려)
    public Vector3 GetApproachPosition(PathRole role)
    {
        if (passWindows.Count == 0) return Vector3.zero;
        return GridManager.Instance.GetFurnitureApproachPosition(passWindows[0].transform.position, role);
    }
}
