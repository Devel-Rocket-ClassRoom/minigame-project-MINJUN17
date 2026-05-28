using UnityEngine;

/// <summary>
/// 머리 위에 단순 표시 아이콘 (주문 말풍선, EAT !, etc).
/// 자식 SpriteRenderer에 부착하고 부모에서 Show/Hide만 호출.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class IndicatorIcon : MonoBehaviour
{
    private SpriteRenderer _sr;

    [Tooltip("시작 시 보이는 상태로 둘지 (기본: 숨김)")]
    [SerializeField] private bool startVisible = false;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _sr.enabled = startVisible;
    }

    public void Show()
    {
        if (_sr != null) _sr.enabled = true;
    }

    public void Hide()
    {
        if (_sr != null) _sr.enabled = false;
    }

    public bool IsVisible => _sr != null && _sr.enabled;
}
