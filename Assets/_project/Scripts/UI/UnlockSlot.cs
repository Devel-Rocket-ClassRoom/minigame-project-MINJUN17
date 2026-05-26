using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 해금 패널의 슬롯 한 칸. IUnlockEntry 만 받으면 표시 + 구매 가능.
/// 마케팅 entry 면 설명에 "(N개월)" 자동 부착.
/// </summary>
public class UnlockSlot : MonoBehaviour
{
    [Header("표시 요소")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image currencyIconImage;

    [Header("구매 버튼")]
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyButtonText;
    [SerializeField] private string buyLabel = "구매";
    [SerializeField] private string purchasedThisMonthLabel = "구매완료";

    [Header("잔액 부족 색")]
    [SerializeField] private Color affordColor   = Color.white;
    [SerializeField] private Color unaffordColor = Color.red;

    private IUnlockEntry _entry;

    public void Setup(IUnlockEntry entry, Sprite satIcon, Sprite moneyIcon)
    {
        UnsubscribeDisplayName();
        UnsubscribeDescription();

        _entry = entry;

        // 아이콘
        if (iconImage != null)
        {
            iconImage.sprite = entry.Icon;
            iconImage.enabled = entry.Icon != null;
        }

        // 이름 — LocalizedString 변경 이벤트 구독
        if (nameText != null)
        {
            if (entry.DisplayName != null && !entry.DisplayName.IsEmpty)
            {
                entry.DisplayName.StringChanged += OnDisplayNameChanged;
                entry.DisplayName.RefreshString();
            }
            else nameText.text = "";
        }

        // 설명 — 마케팅이면 (N개월) 부착
        if (descText != null)
        {
            if (entry.Description != null && !entry.Description.IsEmpty)
            {
                entry.Description.StringChanged += OnDescriptionChanged;
                entry.Description.RefreshString();
            }
            else descText.text = "";
        }

        // 통화 아이콘
        if (currencyIconImage != null)
        {
            currencyIconImage.sprite = entry.Currency == CurrencyType.Satisfaction ? satIcon : moneyIcon;
            currencyIconImage.enabled = currencyIconImage.sprite != null;
        }

        // 비용 + 잔액 색
        if (costText != null) costText.text = entry.Cost.ToString("N0");
        RefreshAffordability();

        // 구매 버튼
        bool blockedByMonthly = entry is RecruitmentUnlockEntry r && !r.IsPurchasableThisMonth;
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
            buyButton.interactable = !blockedByMonthly;
        }
        if (buyButtonText != null)
            buyButtonText.text = blockedByMonthly ? purchasedThisMonthLabel : buyLabel;
    }

    /// <summary>잔액이 바뀌었을 때 비용 색만 갱신.</summary>
    public void RefreshAffordability()
    {
        if (_entry == null || costText == null) return;
        costText.color = _entry.CanAfford ? affordColor : unaffordColor;
    }

    private void OnBuyClicked()
    {
        if (_entry == null) return;
        _entry.Purchase();
        // 결과는 매니저 이벤트로 패널이 Refresh — 슬롯이 사라지거나 잔액만 갱신됨
    }

    // --- LocalizedString 이벤트 핸들러 ---

    private void OnDisplayNameChanged(string s)
    {
        if (nameText != null) nameText.text = s;
    }

    private void OnDescriptionChanged(string s)
    {
        if (descText == null) return;
        // 마케팅 entry 면 (N개월) 자동 부착
        if (_entry is MarketingUnlockEntry m)
            descText.text = $"{s}({m.DurationMonths}개월)";
        else
            descText.text = s;
    }

    private void UnsubscribeDisplayName()
    {
        if (_entry?.DisplayName != null)
            _entry.DisplayName.StringChanged -= OnDisplayNameChanged;
    }

    private void UnsubscribeDescription()
    {
        if (_entry?.Description != null)
            _entry.Description.StringChanged -= OnDescriptionChanged;
    }

    private void OnDestroy()
    {
        UnsubscribeDisplayName();
        UnsubscribeDescription();
    }
}
