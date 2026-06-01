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
    [Tooltip("끄면 마지막 프레임에서 멈춤(한 번만 재생). 편지 꺼내기 같은 단발 동작용")]
    [SerializeField] private bool loop = true;

    [Header("랜덤 시작 (여러 개가 동시에 켜질 때 동기화 방지 — 관중 들썩 등)")]
    [Tooltip("켜면 시작 프레임을 랜덤하게. 여러 오브젝트가 로봇처럼 똑같이 움직이는 걸 방지")]
    [SerializeField] private bool randomizeStart = false;
    [Tooltip("재생 속도 랜덤 배수 (min~max). 1,1이면 고정. 1과 다르면 시간이 갈수록 더 흩어짐")]
    [SerializeField] private Vector2 fpsJitter = new(1f, 1f);

    private Image _img;
    private float _timer;
    private int _index;
    private bool _playing;
    private float _fps;   // 실제 적용 fps (랜덤 배수 반영)

    private void Awake() => _img = GetComponent<Image>();

    private void OnEnable() { if (playOnEnable) Play(); }
    private void OnDisable() => _playing = false;

    public void Play()
    {
        if (_img == null) _img = GetComponent<Image>();   // Awake 순서 무관하게 안전
        if (frames == null || frames.Length == 0) return;
        _playing = true; _timer = 0f;
        _index = randomizeStart ? Random.Range(0, frames.Length) : 0;
        _fps = fps * Random.Range(fpsJitter.x, fpsJitter.y);
        _img.sprite = frames[_index];
    }

    public void Stop() => _playing = false;

    /// <summary>한 번 재생에 걸리는 시간(초) = 프레임 수 / fps. 끝나는 타이밍 계산용.</summary>
    public float Duration => (frames != null && frames.Length > 0 && fps > 0f) ? frames.Length / fps : 0f;

    private void Update()
    {
        if (!_playing || _fps <= 0f || frames == null || frames.Length < 2) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < 1f / _fps) return;
        _timer = 0f;

        if (_index >= frames.Length - 1)
        {
            if (!loop) { _playing = false; return; }   // 단발: 마지막 프레임에서 멈춤
            _index = 0;
        }
        else _index++;

        _img.sprite = frames[_index];
    }
}
