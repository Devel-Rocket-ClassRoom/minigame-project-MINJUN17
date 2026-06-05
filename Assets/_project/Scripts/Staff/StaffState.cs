public enum CookState
{
    IDLE_AT_KITCHEN,
    CHECK_PASS_WINDOW,
    WALK_TO_TOOL,
    USING_TOOL,
    WALK_TO_PASS_WINDOW,
    PLACE_FOOD,
}
public enum ServerState
{
    IDLE_AT_COUNTER,
    TAKING_ORDER,
    WAIT_FOR_NEXT_ORDER,   // 줄 남아있을 때 다음 주문 손님을 카운터에서 대기
    WALK_TO_PASS_WINDOW,
    WALK_TO_SEAT,
    WALK_TO_STAIR,
    DELIVER,
    WALK_TO_PHONE,
    TAKING_DELIVERY_ORDER,
    WALK_TO_DT_ORDER,
    TAKING_DT_ORDER,
    WALK_TO_DT_PICKUP,
}
public enum RiderState
{
    IDLE_OUTSIDE,
    WALK_TO_PASSWINDOW,
    WALK_TO_EXIT,
    DELIVER,
}
