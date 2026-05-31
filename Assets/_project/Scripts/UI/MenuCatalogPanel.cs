using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// 메뉴 도감. 해금된 메뉴만 하나씩(1/N) 좌우 화살표로 넘겨보며 아이콘/이름/판매가/재료비를 표시.
///
/// 열기: 사이드 "메뉴" 버튼 onClick → Open()
/// 닫기: 닫기(X) 버튼은 PopupPanel이 자동 연결, 또는 Close().
/// (CustomerCatalogPanel과 동일한 구조)
/// </summary>
public class MenuCatalogPanel : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PopupPanel popup;            // 열기/닫기 애니메이션 (옵션)
    [SerializeField] private Image iconImage;             // 음식 아이콘
    [SerializeField] private TextMeshProUGUI nameText;    // 메뉴 이름
    [SerializeField] private TextMeshProUGUI priceText;   // "판매가: 6,000원"
    [SerializeField] private TextMeshProUGUI costText;    // "재료비: 2,000원"
    [SerializeField] private TextMeshProUGUI indexText;   // "메뉴 1/5"
    [SerializeField] private Button prevButton;           // ◀
    [SerializeField] private Button nextButton;           // ▶

    [Header("표시 형식")]
    [SerializeField] private string priceFormat = "판매가: {0:N0}원";
    [SerializeField] private string costFormat  = "재료비: {0:N0}원";
    [Tooltip("{0}=현재 번호, {1}=전체 수")]
    [SerializeField] private string indexFormat = "메뉴 {0}/{1}";

    private readonly List<MenuData> _list = new();
    private int _index;
    private LocalizedString _boundName;

    private void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(Prev);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
    }

    private void OnDestroy() => UnbindName();

    // 패널이 켜지는 순간 무조건 실제 해금 메뉴 데이터로 갱신.
    private void OnEnable()
    {
        _index = 0;
        Rebuild();
        Refresh();
    }

    /// <summary>"메뉴" 버튼 onClick에 연결.</summary>
    public void Open()
    {
        popup?.Open();   // 활성화되면 OnEnable에서 데이터 갱신됨
        if (popup == null) gameObject.SetActive(true);
        else { Rebuild(); _index = 0; Refresh(); }   // 이미 활성 상태에서 다시 열 때 대비
    }

    public void Close() => popup?.Close();

    private void Rebuild()
    {
        _list.Clear();
        if (MenuManager.Instance != null)
            _list.AddRange(MenuManager.Instance.UnlockedMenus);   // 해금된 메뉴만
    }

    public void Next()
    {
        if (_list.Count == 0) return;
        _index = (_index + 1) % _list.Count;     // 순환
        Refresh();
    }

    public void Prev()
    {
        if (_list.Count == 0) return;
        _index = (_index - 1 + _list.Count) % _list.Count;
        Refresh();
    }

    private void Refresh()
    {
        int count = _list.Count;
        bool hasAny = count > 0;

        if (indexText != null)
            indexText.text = string.Format(indexFormat, hasAny ? _index + 1 : 0, count);

        if (!hasAny)
        {
            if (iconImage != null) iconImage.enabled = false;
            if (nameText != null)  nameText.text = "";
            if (priceText != null) priceText.text = "";
            if (costText != null)  costText.text = "";
            UnbindName();
            return;
        }

        var data = _list[Mathf.Clamp(_index, 0, count - 1)];

        if (iconImage != null)
        {
            iconImage.sprite  = data.foodSprite;
            iconImage.enabled = data.foodSprite != null;
        }
        if (priceText != null) priceText.text = string.Format(priceFormat, data.price);
        if (costText != null)  costText.text  = string.Format(costFormat,  data.cost);

        BindName(data);
    }

    // ─── LocalizedString 이름 바인딩 (로케일 변경 시 자동 갱신) ───
    private void BindName(MenuData data)
    {
        UnbindName();
        if (nameText == null) return;

        if (data.menuName != null && !data.menuName.IsEmpty)
        {
            _boundName = data.menuName;
            _boundName.StringChanged += OnNameChanged;
            _boundName.RefreshString();
        }
        else
        {
            nameText.text = data.name;   // 폴백: 에셋 이름
        }
    }

    private void OnNameChanged(string s)
    {
        if (nameText != null) nameText.text = s;
    }

    private void UnbindName()
    {
        if (_boundName != null) _boundName.StringChanged -= OnNameChanged;
        _boundName = null;
    }
}
