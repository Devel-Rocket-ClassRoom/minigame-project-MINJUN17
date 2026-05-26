using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 연말 랭킹 패널. RankingSystem.OnYearRanked 구독 → 자동 표시.
/// 행마다 SetActive(true) + DOPunchScale + 숫자 카운트업으로 순차 등장.
/// 등수는 RankTierStyle 리스트로 등수별 색/크기/라벨 커스터마이즈.
/// 확인 버튼 → 패널 닫기 (게임은 계속).
/// </summary>
public class RankingPanel : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private RankingSystem rankingSystem;
    [SerializeField] private TimeSystem timeSystem;
    [SerializeField] private CanvasGroup panelGroup;  // 패널 자체에 붙인 CanvasGroup. 시작 alpha=0.

    [Header("행 GameObject (순차 등장)")]
    [SerializeField] private GameObject titleRow;
    [SerializeField] private GameObject revenueRow;
    [SerializeField] private GameObject reputationRow;
    [SerializeField] private GameObject rankRow;
    [SerializeField] private GameObject confirmRow;

    [Header("값 텍스트")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI reputationText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Button confirmButton;

    [Header("타이틀 포맷")]
    [SerializeField] private string titleFormat = "{0}년차 결산";

    [Header("등수 티어 스타일 (작은 maxRank부터 순서대로 검사)")]
    [SerializeField] private List<RankTierStyle> rankTiers = new()
    {
        new RankTierStyle { maxRank = 3,   color = new Color(1f, 0.84f, 0f),    fontScale = 1.5f }, // 금색
        new RankTierStyle { maxRank = 10,  color = new Color(1f, 0.92f, 0.3f),  fontScale = 1.2f }, // 노랑
        new RankTierStyle { maxRank = 50,  color = Color.white,                  fontScale = 1.0f }, // 흰색
        new RankTierStyle { maxRank = 100, color = new Color(0.7f, 0.7f, 0.7f),  fontScale = 1.0f }, // 회색
    };

    [Header("랭킹 미달 시 (Qualified == false 또는 Rank > 100)")]
    [SerializeField] private RankTierStyle notQualifiedStyle = new()
    {
        color = new Color(0.4f, 0.4f, 0.4f),
        fontScale = 0.8f,
        labelOverride = "참가상",
    };

    [Header("연출 타이밍")]
    [SerializeField] private float countUpSec = 0.6f;
    [SerializeField] private float stepDelay  = 0.5f;

    [Header("Punch 효과")]
    [SerializeField] private float punchStrength = 0.15f;
    [SerializeField] private float punchDuration = 0.3f;

    [Header("순위 강조")]
    [SerializeField] private bool  emphasizeRank = true;
    [SerializeField] private float rankPunchStrength = 0.4f;

    private Sequence _seq;

    [System.Serializable]
    public class RankTierStyle
    {
        [Tooltip("이 등수 이하면 이 스타일 적용. 작은 값부터 우선 검사.")]
        public int    maxRank = 100;
        public Color  color   = Color.white;
        public float  fontScale = 1f;
        [Tooltip("비어있으면 'N등' 그대로, 채우면 그 문자열 사용 (예: '참가상')")]
        public string labelOverride;
    }

    private void OnEnable()
    {
        if (rankingSystem != null) rankingSystem.OnYearRanked += Show;
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
    }

    private void OnDisable()
    {
        if (rankingSystem != null) rankingSystem.OnYearRanked -= Show;
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
        _seq?.Kill();
    }

    private void Show(RankingSystem.YearResult result)
    {
        _seq?.Kill();
        SetPanelVisible(true);

        // 타이틀 (TimeSystem.Year — OnYearEnded 시점에 이미 +1 되어 있음 → 방금 끝난 연차)
        if (titleText != null && timeSystem != null)
            titleText.text = string.Format(titleFormat, timeSystem.Year);

        HideAll();
        if (confirmButton != null) confirmButton.interactable = false;

        // 순위 스타일 결정
        var rankStyle = PickRankStyle(result);
        string rankLabel = !string.IsNullOrEmpty(rankStyle.labelOverride)
            ? rankStyle.labelOverride
            : $"{result.Rank}등";

        // 순위 텍스트 미리 색/크기 설정 (카운트업 안 함 — 단발 표시)
        if (rankText != null)
        {
            rankText.color = rankStyle.color;
            rankText.transform.localScale = Vector3.one * rankStyle.fontScale;
        }

        _seq = DOTween.Sequence()
            // 1) 타이틀
            .AppendCallback(() => Appear(titleRow))
            .AppendInterval(stepDelay)

            // 2) 연매출
            .AppendCallback(() => Appear(revenueRow))
            .Append(CountUp(revenueText, result.Revenue, "원"))
            .AppendInterval(stepDelay)

            // 3) 손님평판
            .AppendCallback(() => Appear(reputationRow))
            .Append(CountUp(reputationText, result.Reputation, "점"))
            .AppendInterval(stepDelay)

            // 4) 순위 (강조)
            .AppendCallback(() => {
                Appear(rankRow, emphasizeRank ? rankPunchStrength : punchStrength);
                if (rankText != null) rankText.text = rankLabel;
            })
            .AppendInterval(stepDelay)

            // 5) 확인 버튼
            .AppendCallback(() => {
                Appear(confirmRow);
                if (confirmButton != null) confirmButton.interactable = true;
            })
            .OnComplete(() => _seq = null)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    /// <summary>등수에 맞는 티어 스타일 선택.</summary>
    private RankTierStyle PickRankStyle(RankingSystem.YearResult result)
    {
        if (!result.Qualified) return notQualifiedStyle;

        // 작은 maxRank부터 검사 (1~3등 먼저, 그 다음 4~10등 …)
        if (rankTiers != null)
            foreach (var tier in rankTiers)
                if (tier != null && result.Rank <= tier.maxRank) return tier;

        return notQualifiedStyle;
    }

    /// <summary>행을 켜고 살짝 튀어오르게.</summary>
    private void Appear(GameObject row) => Appear(row, punchStrength);

    private void Appear(GameObject row, float strength)
    {
        if (row == null) return;
        row.SetActive(true);
        row.transform.localScale = Vector3.one;
        row.transform.DOPunchScale(Vector3.one * strength, punchDuration, 5, 0.5f)
            .SetUpdate(true);
    }

    /// <summary>숫자 0→target 카운트업. 단위(suffix)는 "원"/"점" 등.</summary>
    private Tween CountUp(TextMeshProUGUI text, long target, string suffix)
    {
        if (text == null) return null;
        text.text = $"0{suffix}";
        long current = 0;
        return DOTween.To(() => current, x => {
            current = x;
            text.text = $"{x:N0}{suffix}";
        }, target, countUpSec).SetEase(Ease.OutCubic);
    }

    private void HideAll()
    {
        SetRowActive(titleRow,      false);
        SetRowActive(revenueRow,    false);
        SetRowActive(reputationRow, false);
        SetRowActive(rankRow,       false);
        SetRowActive(confirmRow,    false);
    }

    private static void SetRowActive(GameObject row, bool active)
    {
        if (row != null) row.SetActive(active);
    }

    private void OnConfirm()
    {
        _seq?.Kill();
        SetPanelVisible(false);
        SaveLoadManager.Instance?.Save();   // 연말 정산 직후 자동 저장 (LastResult 포함)
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelGroup == null) return;
        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }
}
