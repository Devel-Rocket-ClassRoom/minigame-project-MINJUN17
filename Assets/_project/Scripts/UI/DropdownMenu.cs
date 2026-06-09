using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 우하단(또는 어디든) 버튼을 누르면 위로 펼쳐지는 드롭다운 메뉴.
/// - menuPanel: 펼쳐질 메뉴 본체 (버튼 위쪽에 배치)
/// - toggleButton: 메뉴 켜고 끄는 버튼
/// 버튼을 다시 누르거나 메뉴 밖을 누르면 닫힘.
/// </summary>
public class DropdownMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private RectTransform menuPanel; // 펼쳐질 메뉴
    [SerializeField] private CanvasGroup menuCanvasGroup; // 페이드/클릭 차단용 (없으면 자동 추가)

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.2f;
    [SerializeField] private float slideOffset = 30f; // 펼쳐질 때 살짝 위로 슬라이드되는 거리
    [SerializeField] private Ease ease = Ease.OutCubic;

    [Header("Start State")]
    [SerializeField] private bool startOpen = false;

    private bool _isOpen;
    private Vector2 _panelOpenPos;   // 펼쳐졌을 때 위치 (원래 RectTransform 위치)
    private Vector2 _panelClosedPos; // 닫혔을 때 위치 (살짝 아래로 내려간 상태)

    private void Awake()
    {
        if (menuPanel == null)
        {
            return;
        }

        // CanvasGroup 없으면 자동 추가 (클릭 차단, 페이드 처리용)
        if (menuCanvasGroup == null)
        {
            menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
            if (menuCanvasGroup == null)
                menuCanvasGroup = menuPanel.gameObject.AddComponent<CanvasGroup>();
        }

        // 시작 위치 기록
        _panelOpenPos = menuPanel.anchoredPosition;
        _panelClosedPos = _panelOpenPos + new Vector2(0f, -slideOffset);

        // 버튼 이벤트 연결
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);

        // 초기 상태 적용 (애니메이션 없이)
        ApplyState(startOpen, instant: true);
    }

    public void Toggle()
    {
        ApplyState(!_isOpen, instant: false);
    }

    public void Open()  => ApplyState(true, instant: false);
    public void Close() => ApplyState(false, instant: false);

    private void ApplyState(bool open, bool instant)
    {
        _isOpen = open;

        // 진행 중인 트윈 정리
        menuPanel.DOKill();
        menuCanvasGroup.DOKill();

        if (open)
        {
            menuPanel.gameObject.SetActive(true);
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;

            if (instant)
            {
                menuPanel.anchoredPosition = _panelOpenPos;
                menuCanvasGroup.alpha = 1f;
            }
            else
            {
                menuPanel.anchoredPosition = _panelClosedPos;
                menuCanvasGroup.alpha = 0f;
                menuPanel.DOAnchorPos(_panelOpenPos, animDuration).SetEase(ease);
                menuCanvasGroup.DOFade(1f, animDuration);
            }
        }
        else
        {
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;

            if (instant)
            {
                menuPanel.anchoredPosition = _panelClosedPos;
                menuCanvasGroup.alpha = 0f;
                menuPanel.gameObject.SetActive(false);
            }
            else
            {
                menuPanel.DOAnchorPos(_panelClosedPos, animDuration).SetEase(ease);
                menuCanvasGroup.DOFade(0f, animDuration)
                    .OnComplete(() => menuPanel.gameObject.SetActive(false));
            }
        }
    }
}
