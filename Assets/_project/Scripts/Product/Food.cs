using UnityEngine;

public class Food : MonoBehaviour
{
    public Order order;
    public Staff claimedBy;   // 픽업대에 있지만 누가 가져갈 거라고 클레임된 상태 (시각은 픽업대 그대로)
    private SpriteRenderer sr;

    private void Awake()
    {
        // 정렬은 루트 위치 기준, 그림은 자식 Visual(위로 올림)에서 — 루트/자식 어디 있든 OK
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void SetUp(Order order)
    {
        this.order = order;
        sr.sprite = order.menus[0].foodSprite;
    }

    /// <summary>정렬 순서 설정 (패스윈도우 위=올림, 들고가면=0 복귀).</summary>
    public void SetSortingOrder(int order)
    {
        if (sr != null) sr.sortingOrder = order;
    }
}
