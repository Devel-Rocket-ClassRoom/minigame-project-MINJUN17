using UnityEngine;

/// <summary>
/// 조리 진행도를 8단계(또는 N단계) 스프라이트로 표시. 요리사 머리 위 자식 SpriteRenderer에 부착.
/// CookStaff가 매 프레임 SetProgress(0~1) 호출. 조리 시작/종료 시 Show/Hide.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CookingProgressDisplay : MonoBehaviour
{
    [Tooltip("진행도 순서대로 정렬된 스프라이트 (0=시작/빈 상태, 마지막=꽉 참)")]
    [SerializeField] private Sprite[] frames;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        Hide();
    }

    public void Show()
    {
        if (_sr != null) _sr.enabled = true;
    }

    public void Hide()
    {
        if (_sr != null) _sr.enabled = false;
    }

    /// <summary>0~1 진행도를 인덱스에 매핑해 sprite 교체. 균등 분할.</summary>
    public void SetProgress(float t)
    {
        if (frames == null || frames.Length == 0 || _sr == null) return;
        t = Mathf.Clamp01(t);
        int idx = Mathf.Clamp(Mathf.FloorToInt(t * frames.Length), 0, frames.Length - 1);
        _sr.sprite = frames[idx];
    }
}
