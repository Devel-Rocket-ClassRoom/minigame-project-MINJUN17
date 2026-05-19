using System.Collections.Generic;
using UnityEngine;

public class PassWindow : MonoBehaviour
{
    public Queue<Order> pendingOrders = new Queue<Order>(); // 홀 → 요리사: 대기 중인 주문
    public Queue<Food> readyFoods = new Queue<Food>();      // 요리사 → 홀: 완성된 음식

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

    public void SubmitOrder(Order order) => pendingOrders.Enqueue(order);
    public Order DequeueOrder() => pendingOrders.Dequeue();

    public void PlaceFood(Food food) => readyFoods.Enqueue(food);
    public Food PickupFood() => readyFoods.Dequeue();
}
