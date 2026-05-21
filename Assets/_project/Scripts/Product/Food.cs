using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Food : MonoBehaviour
{
    public Order order;
    public Staff claimedBy;   // 픽업대에 있지만 누가 가져갈 거라고 클레임된 상태 (시각은 픽업대 그대로)
    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void SetUp(Order order)
    {
        this.order = order;
        sr.sprite = order.menus[0].foodSprite;
    }
}