using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// 채용 후보 1명을 표시하는 슬롯. StaffCandidate 직접 받음.
/// 이름(동적), 역할+등급(로컬라이즈), 채용 비용(돈), 고용 버튼.
/// </summary>
public class StaffCandidateSlot : MonoBehaviour
{
    private const string TableName = "StringTable";

    [Header("표시 요소")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleGradeText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image currencyIconImage;

    [Header("고용 버튼")]
    [SerializeField] private Button hireButton;
    [SerializeField] private TextMeshProUGUI hireButtonText;
    [SerializeField] private string hireLabel = "고용";
    [SerializeField] private string capReachedLabel = "정원 초과";

    [Header("잔액 부족 색")]
    [SerializeField] private Color affordColor   = Color.white;
    [SerializeField] private Color unaffordColor = Color.red;

    private StaffCandidate _candidate;

    public void Setup(StaffCandidate candidate, Sprite moneyIcon)
    {
        _candidate = candidate;

        // 아이콘
        if (iconImage != null)
        {
            iconImage.sprite = candidate?.baseData != null ? candidate.baseData.sprite : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        // 이름 (동적, StaffNamePool 키 → 현재 로케일)
        if (nameText != null)
        {
            nameText.text = StaffManager.Instance != null
                ? StaffManager.Instance.ResolveName(candidate?.candidateName)
                : (candidate?.candidateName ?? "");
        }

        // 역할 + 등급
        if (roleGradeText != null && candidate?.baseData != null)
            roleGradeText.text = $"{Loc("staff.role." + candidate.baseData.role.ToString().ToLower())} " +
                                 $"{Loc("staff.grade." + candidate.baseData.grade.ToString().ToLower())}";

        // 비용
        long cost = candidate?.baseData != null ? candidate.baseData.hireCost : 0;
        if (costText != null) costText.text = cost.ToString("N0");

        // 통화 아이콘 (항상 돈)
        if (currencyIconImage != null)
        {
            currencyIconImage.sprite = moneyIcon;
            currencyIconImage.enabled = moneyIcon != null;
        }

        // 고용 버튼 — 정원 초과 / 잔액 부족 체크
        bool capReached = IsCapReached();
        if (hireButton != null)
        {
            hireButton.onClick.RemoveAllListeners();
            hireButton.onClick.AddListener(OnHireClicked);
            hireButton.interactable = !capReached;
        }
        if (hireButtonText != null)
            hireButtonText.text = capReached ? capReachedLabel : hireLabel;

        RefreshAffordability();
    }

    /// <summary>잔액 색만 갱신 (돈 변동 시).</summary>
    public void RefreshAffordability()
    {
        if (_candidate?.baseData == null || costText == null) return;
        bool canAfford = MoneySystem.Instance != null
                      && MoneySystem.Instance.CanAfford(_candidate.baseData.hireCost);
        costText.color = canAfford ? affordColor : unaffordColor;
    }

    private bool IsCapReached()
    {
        if (_candidate?.baseData == null || StaffManager.Instance == null) return false;
        return _candidate.baseData.role switch
        {
            StaffRole.Cook   => StaffManager.Instance.CookStaffs.Count   >= StaffManager.Instance.MaxCookCount,
            StaffRole.Server => StaffManager.Instance.ServerStaffs.Count >= StaffManager.Instance.MaxServerCount,
            StaffRole.Rider  => StaffManager.Instance.RiderStaffs.Count  >= StaffManager.Instance.MaxRiderCount,
            _ => false,
        };
    }

    private void OnHireClicked()
    {
        if (_candidate == null) return;
        StaffCandidatePool.Instance?.Hire(_candidate);
        // 패널이 OnApplicantsChanged 받아서 슬롯 통째로 다시 그림 → 사라짐
    }

    private static string Loc(string key)
    {
        try
        {
            string s = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
            return string.IsNullOrEmpty(s) ? key : s;
        }
        catch { return key; }
    }
}
