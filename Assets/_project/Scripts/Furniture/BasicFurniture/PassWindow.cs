using System.Collections.Generic;
using UnityEngine;

public class PassWindow : MonoBehaviour
{
    public Queue<Order> pendingOrders = new Queue<Order>();
    public List<Food> readyFoods = new List<Food>();

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

    public bool HasReadyHallFood()
    {
        foreach (var f in readyFoods)
            if (f.order != null && !f.order.isDelivery) return true;
        return false;
    }

    public bool HasReadyDeliveryFood()
    {
        foreach (var f in readyFoods)
            if (f.order != null && f.order.isDelivery) return true;
        return false;
    }

    public void SubmitOrder(Order order) => pendingOrders.Enqueue(order);
    public Order DequeueOrder() => pendingOrders.Dequeue();

    public void PlaceFood(Food food) => readyFoods.Add(food);

    // FIFO를 유지하면서 isDelivery 매칭되는 가장 앞 음식 픽업
    public Food PickupHallFood()
    {
        for (int i = 0; i < readyFoods.Count; i++)
        {
            if (readyFoods[i].order != null && !readyFoods[i].order.isDelivery)
            {
                Food f = readyFoods[i];
                readyFoods.RemoveAt(i);
                return f;
            }
        }
        return null;
    }

    public Food PickupDeliveryFood()
    {
        for (int i = 0; i < readyFoods.Count; i++)
        {
            if (readyFoods[i].order != null && readyFoods[i].order.isDelivery)
            {
                Food f = readyFoods[i];
                readyFoods.RemoveAt(i);
                return f;
            }
        }
        return null;
    }
}
