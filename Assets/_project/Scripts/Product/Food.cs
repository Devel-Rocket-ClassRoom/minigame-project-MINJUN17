using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Food : MonoBehaviour
{
    public Order order;
    public Staff claimedBy;   // 픽업대에 있지만 누가 가져갈 거라고 클레임된 상태 (시각은 픽업대 그대로)
    private SpriteRenderer sr;

    // 강제 표시(pin) 복원용 원래 정렬값
    private int _baseOrder;
    private int _baseLayerId;
    private YSorter _ySorter;   // 있으면 pin 동안 비활성 (Y정렬이 덮어쓰지 않게)

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        _baseOrder   = sr.sortingOrder;
        _baseLayerId = sr.sortingLayerID;
        _ySorter     = GetComponent<YSorter>();
    }

    public void SetUp(Order order)
    {
        this.order = order;
        sr.sprite = order.menus[0].foodSprite;
    }

    /// <summary>
    /// 패스윈도우 등 음식을 가리는 비주얼 위로 강제 표시.
    /// 지정한 정렬 레이어 + order로 올린다. 놓여있는 동안만 호출.
    /// </summary>
    public void PinInFront(string sortingLayer, int order)
    {
        if (_ySorter != null) _ySorter.enabled = false;   // Y정렬이 매 프레임 덮어쓰지 않게
        if (sr == null) return;
        if (!string.IsNullOrEmpty(sortingLayer)) sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = order;
    }

    /// <summary>강제 표시 해제 — 원래 정렬로 복원 (픽업대에서 들어올릴 때 호출).</summary>
    public void RestoreSorting()
    {
        if (sr != null)
        {
            sr.sortingLayerID = _baseLayerId;
            sr.sortingOrder   = _baseOrder;
        }
        if (_ySorter != null) _ySorter.enabled = true;
    }
}
