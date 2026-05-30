using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 팝업 패널의 열기/닫기 (페이드 + 살짝 슬라이드).
/// 외부 버튼(드롭다운의 설치 버튼 등) → Open()
/// 패널 내 닫기 버튼 → Close()
/// </summary>
public class PopupPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panel;        // 애니메이션 대상 (보통 자기 자신 또는 자식 루트)
    [SerializeField] private CanvasGroup canvasGroup;    // 없으면 자동 추가
    [SerializeField] private Button closeButton;         // 옵션 (있으면 자동 연결)
    [Tooltip("풀스크린 투명 버튼(모달 막). 패널 뒤·다른 버튼 앞에 배치. 누르면 닫히고, 뒤 버튼 클릭을 차단.")]
    [SerializeField] private Button dimBackdrop;         // 옵션 (있으면 패널과 함께 켜지고 클릭 시 Close)

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.2f;
    [SerializeField] private float slideOffset = 30f;
    [SerializeField] private Ease ease = Ease.OutCubic;

    [Header("Start State")]
    [SerializeField] private bool startOpen = false;

    private bool _isOpen;
    private Vector2 _openPos;
    private Vector2 _closedPos;
    private bool _inited;

    private void Awake() => EnsureInit();

    /// <summary>
    /// 초기화. Awake에서 1회 실행되지만, 비활성 상태에서 Open()이 먼저 호출되는 경우
    /// (Awake 미실행)에도 대비해 Apply에서 호출 → null 크래시 방지.
    /// </summary>
    private void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        if (panel == null) panel = transform as RectTransform;
        if (panel == null)
        {
            Debug.LogError($"[PopupPanel] panel 미지정 + 이 오브젝트가 RectTransform이 아님 — " +
                           $"인스펙터에서 panel을 지정하세요: {name}", this);
            return;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        _openPos = panel.anchoredPosition;
        _closedPos = _openPos + new Vector2(0f, -slideOffset);

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (dimBackdrop != null) dimBackdrop.onClick.AddListener(Close);

        Apply(startOpen, instant: true);   // _inited=true 이후라 재귀 안 됨
    }

    public void Open()   => Apply(true,  instant: false);
    public void Close()  => Apply(false, instant: false);
    public void Toggle() => Apply(!_isOpen, instant: false);

    private void Apply(bool open, bool instant)
    {
        EnsureInit();
        if (panel == null) return;   // 초기화 실패(RectTransform 아님) 시 크래시 방지

        _isOpen = open;
        panel.DOKill();
        canvasGroup.DOKill();

        if (dimBackdrop != null) dimBackdrop.gameObject.SetActive(open);   // 막은 패널과 함께 즉시 켜고 끔

        if (open)
        {
            panel.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            if (instant)
            {
                panel.anchoredPosition = _openPos;
                canvasGroup.alpha = 1f;
            }
            else
            {
                panel.anchoredPosition = _closedPos;
                canvasGroup.alpha = 0f;
                panel.DOAnchorPos(_openPos, animDuration).SetEase(ease);
                canvasGroup.DOFade(1f, animDuration);
            }
        }
        else
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (instant)
            {
                panel.anchoredPosition = _closedPos;
                canvasGroup.alpha = 0f;
                panel.gameObject.SetActive(false);
            }
            else
            {
                panel.DOAnchorPos(_closedPos, animDuration).SetEase(ease);
                canvasGroup.DOFade(0f, animDuration)
                    .OnComplete(() => panel.gameObject.SetActive(false));
            }
        }
    }
}
