using System.Collections.Generic;
using UnityEngine;

public class PassWindow : MonoBehaviour
{
    public Queue<Order> pendingOrders = new Queue<Order>();
    public List<Food> readyFoods = new List<Food>();

    [Header("음식 표시 슬롯")]
    [SerializeField] private Vector3 firstSlotOffset = new Vector3(-0.3f, 0.2f, 0f);
    [SerializeField] private float slotSpacing = 0.3f;

    [Header("사용 위치 (비우면 자동 인접셀 계산)")]
    [Tooltip("요리사가 음식을 놓는 위치 (주방측)")]
    [SerializeField] private Transform cookUsePoint;
    [Tooltip("서버/라이더가 음식을 가져가는 위치 (홀측)")]
    [SerializeField] private Transform serverUsePoint;

    /// <summary>역할별 명시 사용 위치. null이면 호출측이 자동 계산으로 폴백.</summary>
    public Transform GetUsePoint(PathRole role) =>
        role == PathRole.Cook ? cookUsePoint : serverUsePoint;

    private void Awake()
    {
        PassWindowManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (PassWindowManager.Instance != null)
            PassWindowManager.Instance.Unregister(this);
    }

    public bool HasPendingOrder() => pendingOrders.Count > 0;
    public bool HasReadyFood() => readyFoods.Count > 0;

    // ===== type별 generic 헬퍼 (claim 안 된 것만) =====
    public bool HasReadyFood(OrderType type)
    {
        foreach (var f in readyFoods)
            if (f != null && f.order != null && f.order.type == type && f.claimedBy == null) return true;
        return false;
    }

    public Food ClaimFood(OrderType type, Staff claimer)
    {
        foreach (var f in readyFoods)
        {
            if (f != null && f.order != null && f.order.type == type && f.claimedBy == null)
            {
                f.claimedBy = claimer;
                return f;
            }
        }
        return null;
    }

    // ===== 기존 호환 메서드 (점진 제거) =====
    public bool HasReadyHallFood() => HasReadyFood(OrderType.Hall);
    public bool HasReadyDeliveryFood() => HasReadyFood(OrderType.Delivery);
    public Food ClaimHallFood(Staff claimer) => ClaimFood(OrderType.Hall, claimer);
    public Food ClaimDeliveryFood(Staff claimer) => ClaimFood(OrderType.Delivery, claimer);

    // 클레임된 음식을 실제로 들기: 큐에서 dequeue + 슬롯 재정렬
    public Food TakeFood(Food food)
    {
        if (food == null) return null;
        if (readyFoods.Remove(food))
        {
            ReflowSlots();
            food.claimedBy = null;   // 정리
            return food;
        }
        return null;
    }

    public void SubmitOrder(Order order) => pendingOrders.Enqueue(order);
    public Order DequeueOrder() => pendingOrders.Dequeue();

    /// <summary>영업 정리: 미처리 주문 큐 비우고 픽업대에 남은 음식 프리팹 제거.</summary>
    public void ClearAll()
    {
        pendingOrders.Clear();
        foreach (var f in readyFoods)
            if (f != null) Destroy(f.gameObject);
        readyFoods.Clear();
    }

    public void PlaceFood(Food food)
    {
        readyFoods.Add(food);
        food.transform.SetParent(transform, false);
        food.transform.localPosition = SlotPos(readyFoods.Count - 1);
    }

    // FIFO를 유지하면서 isDelivery 매칭되는 가장 앞 음식 픽업
    public Food PickupHallFood()
    {
        for (int i = 0; i < readyFoods.Count; i++)
        {
            if (readyFoods[i] != null && readyFoods[i].order != null && !readyFoods[i].order.isDelivery)
            {
                Food f = readyFoods[i];
                readyFoods.RemoveAt(i);
                ReflowSlots();
                return f;
            }
        }
        return null;
    }

    public Food PickupDeliveryFood()
    {
        for (int i = 0; i < readyFoods.Count; i++)
        {
            if (readyFoods[i] != null && readyFoods[i].order != null && readyFoods[i].order.isDelivery)
            {
                Food f = readyFoods[i];
                readyFoods.RemoveAt(i);
                ReflowSlots();
                return f;
            }
        }
        return null;
    }

    private Vector3 SlotPos(int index)
        => firstSlotOffset + new Vector3(slotSpacing * index, 0f, 0f);

    private void ReflowSlots()
    {
        for (int i = 0; i < readyFoods.Count; i++)
            if (readyFoods[i] != null)
                readyFoods[i].transform.localPosition = SlotPos(i);
    }
}
