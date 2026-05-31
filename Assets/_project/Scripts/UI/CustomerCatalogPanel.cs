using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// 손님 도감. 해금된 손님만 한 명씩(1/N) 좌우 화살표로 넘겨보며 아이콘/이름/지갑을 표시.
///
/// 열기: 사이드 "손님" 버튼 onClick → Open()
/// 닫기: 닫기(X) 버튼은 PopupPanel이 자동 연결, 또는 Close().
/// </summary>
public class CustomerCatalogPanel : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PopupPanel popup;            // 열기/닫기 애니메이션 (옵션)
    [SerializeField] private Image iconImage;             // 손님 아이콘
    [SerializeField] private TextMeshProUGUI nameText;    // 손님 이름
    [SerializeField] private TextMeshProUGUI walletText;  // "지갑: 20,000원"
    [SerializeField] private TextMeshProUGUI indexText;   // "1/5"
    [SerializeField] private Image favoriteFoodIcon;      // 최애 음식 아이콘 (preferredMenu)
    [SerializeField] private Button prevButton;           // ◀
    [SerializeField] private Button nextButton;           // ▶

    [Header("표시 형식")]
    [SerializeField] private string walletFormat = "지갑: {0:N0}원";
    [Tooltip("{0}=현재 번호, {1}=전체 수")]
    [SerializeField] private string indexFormat = "손님 {0}/{1}";

    private readonly List<CustomerData> _list = new();
    private int _index;
    private LocalizedString _boundName;

    private void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(Prev);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
    }

    private void OnDestroy() => UnbindName();

    // 패널이 켜지는 순간 무조건 실제 해금 손님 데이터로 갱신.
    // → 버튼이 Open()을 부르든 / PopupPanel로 켜든 / SetActive로 켜든
    //   항상 인스펙터의 더미 텍스트를 실제 데이터로 덮어씀.
    private void OnEnable()
    {
        _index = 0;
        Rebuild();
        Refresh();
    }

    /// <summary>"손님" 버튼 onClick에 연결.</summary>
    public void Open()
    {
        popup?.Open();   // 활성화되면 OnEnable에서 데이터 갱신됨
        // popup이 없으면(그냥 토글 방식) 이 오브젝트를 직접 켬
        if (popup == null) gameObject.SetActive(true);
        else { Rebuild(); _index = 0; Refresh(); }   // 이미 활성 상태에서 다시 열 때 대비
    }

    public void Close() => popup?.Close();

    private void Rebuild()
    {
        _list.Clear();
        if (CustomerManager.Instance != null)
            _list.AddRange(CustomerManager.Instance.GetUnlockedCustomers());   // 해금된 손님만
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

        // 화살표는 항상 그대로 표시. 손님 0~1명이면 눌러도 Next/Prev가 아무 일도 안 함.

        if (!hasAny)
        {
            if (iconImage != null)        iconImage.enabled = false;
            if (nameText != null)         nameText.text = "";
            if (walletText != null)       walletText.text = "";
            if (favoriteFoodIcon != null) favoriteFoodIcon.enabled = false;
            UnbindName();
            return;
        }

        var data = _list[Mathf.Clamp(_index, 0, count - 1)];

        if (iconImage != null)
        {
            iconImage.sprite  = data.icon;
            iconImage.enabled = data.icon != null;
        }
        if (walletText != null)
            walletText.text = string.Format(walletFormat, data.wallet);

        // 최애 음식 아이콘 (선호 메뉴의 음식 스프라이트)
        if (favoriteFoodIcon != null)
        {
            Sprite fav = data.preferredMenu != null ? data.preferredMenu.foodSprite : null;
            favoriteFoodIcon.sprite  = fav;
            favoriteFoodIcon.enabled = fav != null;
        }

        BindName(data);
    }

    // ─── LocalizedString 이름 바인딩 (로케일 변경 시 자동 갱신) ───
    private void BindName(CustomerData data)
    {
        UnbindName();
        if (nameText == null) return;

        if (data.customerName != null && !data.customerName.IsEmpty)
        {
            _boundName = data.customerName;
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
