using UnityEngine;

/// <summary>
/// 스프라이트 배열을 fps 간격으로 순환 재생. (2장이면 번갈아 깜빡)
/// CookingProgressDisplay와 같은 "배열" 방식이되, 외부 진행도 대신 내부 타이머로 자동 순환.
/// 부모에서 Show()/Hide()로 켜고 끔.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EmoteAnimator : MonoBehaviour
{
    [Tooltip("순환할 스프라이트들 (2장이면 번갈아 표시)")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("초당 프레임 교체 수 (낮을수록 천천히 깜빡)")]
    [SerializeField] private float fps = 3f;

    private SpriteRenderer _sr;
    private float _timer;
    private int _index;
    private bool _playing;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.enabled = false;
    }

    public void Show()
    {
        if (frames == null || frames.Length == 0) return;
        _playing = true;
        _index = 0;
        _timer = 0f;
        _sr.enabled = true;
        _sr.sprite = frames[0];
    }

    public void Hide()
    {
        _playing = false;
        if (_sr != null) _sr.enabled = false;
    }

    public bool IsVisible => _sr != null && _sr.enabled;

    private void Update()
    {
        if (!_playing || fps <= 0f || frames == null || frames.Length < 2) return;

        _timer += Time.deltaTime;
        if (_timer < 1f / fps) return;
        _timer = 0f;

        _index = (_index + 1) % frames.Length;   // 배열 순환
        _sr.sprite = frames[_index];
    }
}
