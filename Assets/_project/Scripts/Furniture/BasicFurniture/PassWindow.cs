using System.Collections.Generic;
using UnityEngine;

public class PassWindow : MonoBehaviour
{
    public Queue<Order> pendingOrders = new Queue<Order>();
    public List<Food> readyFoods = new List<Food>();

    [Header("음식 표시 슬롯")]
    [SerializeField] private Vector3 firstSlotOffset = new Vector3(-0.3f, 0.2f, 0f);
    [SerializeField] private float slotSpacing = 0.3f;

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

    // claim 안 된 음식만 카운트 (다른 직원이 가져가기로 한 음식은 제외)
    public bool HasReadyHallFood()
    {
        foreach (var f in readyFoods)
            if (f != null && f.order != null && !f.order.isDelivery && f.claimedBy == null) return true;
        return false;
    }

    public bool HasReadyDeliveryFood()
    {
        foreach (var f in readyFoods)
            if (f != null && f.order != null && f.order.isDelivery && f.claimedBy == null) return true;
        return false;
    }

    // 클레임만: 큐에 그대로 두고 claimedBy만 표시 (시각은 픽업대 위 유지)
    public Food ClaimHallFood(Staff claimer)
    {
        foreach (var f in readyFoods)
        {
            if (f != null && f.order != null && !f.order.isDelivery && f.claimedBy == null)
            {
                f.claimedBy = claimer;
                return f;
            }
        }
        return null;
    }

    public Food ClaimDeliveryFood(Staff claimer)
    {
        foreach (var f in readyFoods)
        {
            if (f != null && f.order != null && f.order.isDelivery && f.claimedBy == null)
            {
                f.claimedBy = claimer;
                return f;
            }
        }
        return null;
    }

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
