using UnityEngine;

[CreateAssetMenu(fileName = "DTCustomerData", menuName = "Customer/DTCustomerData")]
public class DTCustomerData : ScriptableObject
{
    [Header("기본")]
    public string customerName;
    public Sprite carSprite;
    public float moveSpeed = 2f;        // 차 이동 속도 (waypoint 간 MoveTowards)
    public float patience = 30f;        // 응대/음식 대기 한계 (초)
    public float spawnWeight = 1f;

    [Header("주문")]
    public int minOrderCount = 1;
    public int maxOrderCount = 2;

    [Header("만족도")]
    public int baseSatisfaction = 50;
    public int waitPenaltyRate = 3;     // patience 초과 시 1초당 감소 (사용은 추후)
}
