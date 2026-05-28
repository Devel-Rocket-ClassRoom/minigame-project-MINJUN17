using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 번 떠올랐다 페이드되는 텍스트 + 아이콘. FloatingTextSystem이 풀에서 꺼내 Play()로 동작시킴.
/// 프리팹 구성:
///   루트 (RectTransform + 이 컴포넌트)
///     ├─ Icon  (UI Image)            — 코인/하트 등
///     └─ Text  (TextMeshProUGUI)     — 수치
/// 정렬은 HorizontalLayoutGroup 으로 자연 배치 권장. Icon만/Text만 써도 됨 (icon이 null이면 자동 비활성).
/// </summary>
public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private Image icon;

    private RectTransform _rt;
    private CanvasGroup _canvasGroup;
    private Sequence _seq;

    public System.Action<FloatingText> OnFinished;

    [Tooltip("위로 떠오를 픽셀 거리")]
    [SerializeField] private float riseDistance = 60f;

    [Tooltip("페이드아웃 시작 지점 (0~1, duration 비율)")]
    [SerializeField, Range(0f, 1f)] private float fadeStart = 0.4f;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Play(Vector2 screenPos, string text, Color color, Sprite iconSprite, float duration)
    {
        _seq?.Kill();

        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
        }
        if (icon != null)
        {
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.color = Color.white;
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.gameObject.SetActive(false);
            }
        }

        _canvasGroup.alpha = 1f;
        _rt.position = screenPos;

        Vector2 startPos = screenPos;
        Vector2 endPos = startPos + new Vector2(0f, riseDistance);

        _seq = DOTween.Sequence()
            .SetLink(gameObject)
            .Append(_rt.DOMove(endPos, duration).SetEase(Ease.OutQuad))
            .Insert(duration * fadeStart, _canvasGroup.DOFade(0f, duration * (1f - fadeStart)))
            .OnComplete(() => OnFinished?.Invoke(this));
    }

    private void OnDisable()
    {
        _seq?.Kill();
    }
}
