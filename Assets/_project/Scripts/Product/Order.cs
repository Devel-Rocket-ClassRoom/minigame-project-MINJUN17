using System.Collections.Generic;

public class Order
{
    public Customer customer;       // 홀 손님 (Hall)
    public DTCustomer dtCustomer;   // DT 차량 (DT)
    public List<MenuData> menus;
    public OrderType type = OrderType.Hall;

    // 마이그레이션 편의용 (기존 코드 호환). 점진적으로 제거.
    public bool isDelivery
    {
        get => type == OrderType.Delivery;
        set => type = value ? OrderType.Delivery : OrderType.Hall;
    }
}