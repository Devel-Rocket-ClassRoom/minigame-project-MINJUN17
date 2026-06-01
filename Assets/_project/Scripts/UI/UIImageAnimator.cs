using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Image의 sprite를 프레임 배열로 순환 재생. (대사 초상화 말하는 애니 등)
/// EmoteAnimator의 UI(Image) 버전. 자동 재생, Time.unscaledDeltaTime 사용(일시정지 무관).
/// </summary>
[RequireComponent(typeof(Image))]
public class UIImageAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 4f;
    [SerializeField] private bool playOnEnable = true;

    private Image _img;
    private float _timer;
    private int _index;
    private bool _playing;

    private void Awake() => _img = GetComponent<Image>();

    private void OnEnable() { if (playOnEnable) Play(); }
    private void OnDisable() => _playing = false;

    public void Play()
    {
        if (frames == null || frames.Length == 0) return;
        _playing = true; _index = 0; _timer = 0f;
        _img.sprite = frames[0];
    }

    public void Stop() => _playing = false;

    private void Update()
    {
        if (!_playing || fps <= 0f || frames == null || frames.Length < 2) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < 1f / fps) return;
        _timer = 0f;

        _index = (_index + 1) % frames.Length;
        _img.sprite = frames[_index];
    }
}
