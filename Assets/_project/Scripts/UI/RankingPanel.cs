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
    [Tooltip("드럼롤 재생 후 순위 공개까지 긴장감 텀(초)")]
    [SerializeField] private float drumrollLead = 1.5f;

    [Header("Punch 효과")]
    [SerializeField] private float punchStrength = 0.15f;
    [SerializeField] private float punchDuration = 0.3f;

    [Header("순위 강조")]
    [SerializeField] private bool  emphasizeRank = true;
    [SerializeField] private float rankPunchStrength = 0.4f;

    [Header("시상식 연동")]
    [Tooltip("끄면 OnYearRanked 자동 구독 안 함 — CeremonyDirector가 Show()를 직접 호출할 때 사용")]
    [SerializeField] private bool autoSubscribe = true;

    /// <summary>순위 발표 직전 시점(연매출·평판 다 뜬 뒤). 시상식 연출이 편지 꺼내기를 여기에 맞춤.</summary>
    public event System.Action OnDrumrollStart;
    /// <summary>(Director용) 연매출·평판 다 뜨고 멈춘 직후 — 편지 애니 시작 신호.</summary>
    public event System.Action OnResultsReady;
    /// <summary>순위가 실제로 공개되는 순간. 발표 효과음을 여기에 맞춤.</summary>
    public event System.Action OnRankReveal;
    /// <summary>확인 버튼으로 패널이 닫힘. 시상식 무대 정리용.</summary>
    public event System.Action OnClosed;

    private Sequence _seq;
    private RankingSystem.YearResult _pendingResult;   // ShowResults → RevealRank 사이 보관

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
        if (autoSubscribe && rankingSystem != null) rankingSystem.OnYearRanked += Show;
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
    }

    private void OnDisable()
    {
        if (rankingSystem != null) rankingSystem.OnYearRanked -= Show;
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
        _seq?.Kill();
    }

    /// <summary>OnYearRanked 자동 구독용 — 드럼롤/발표 연출 포함(단독 동작).</summary>
    public void Show(RankingSystem.YearResult result) => Show(result, true);

    /// <summary>
    /// 패널만 먼저 띄우기 — 타이틀만 보이고 값(연매출·평판·순위) 행은 숨김.
    /// 사회자가 걷는 동안 패널 프레임을 먼저 노출할 때 사용. 도착 후 Show()로 값 공개.
    /// </summary>
    public void ShowEmpty(RankingSystem.YearResult result)
    {
        _seq?.Kill();
        SetPanelVisible(true);

        if (titleText != null && timeSystem != null)
            titleText.text = string.Format(titleFormat, timeSystem.Year);

        HideAll();
        Appear(titleRow);   // 타이틀만 등장
        if (confirmButton != null) confirmButton.interactable = false;
    }

    /// <summary>
    /// dramatic=false면 드럼롤/발표 효과음/긴장감 텀을 생략한다.
    /// (CeremonyDirector가 봉투·핀조명·드럼롤·발표 소리를 이미 연출했을 때)
    /// </summary>
    public void Show(RankingSystem.YearResult result, bool dramatic)
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

            // 3.5) 순위 발표 직전 (연매출·평판 다 뜬 뒤). 이벤트는 항상, 드럼롤 소리는 dramatic일 때만
            //      Director 연출 시 여기서 편지 꺼내기를 트리거 → drumrollLead 동안 편지 애니가 순위보다 먼저
            .AppendCallback(() => {
                OnDrumrollStart?.Invoke();
                if (dramatic) SoundManager.Get()?.PlaySfx(SfxId.Drumroll);
            })
            .AppendInterval(drumrollLead)

            // 4) 순위 (강조) — 이벤트는 항상, 발표 효과음은 dramatic일 때만
            .AppendCallback(() => {
                OnRankReveal?.Invoke();
                if (dramatic) SoundManager.Get()?.PlaySfx(SfxId.RankingReveal);
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

    // ─── Director 연동: 값 표시 / 순위 공개 분리 ───

    /// <summary>
    /// (Director용) 타이틀·연매출·평판만 순서대로 띄우고 멈춘다. 다 뜨면 OnResultsReady 발생.
    /// 순위는 편지 애니가 끝난 뒤 RevealRank()로 공개. (드럼롤/발표 효과음은 Director가 재생)
    /// </summary>
    public void ShowResults(RankingSystem.YearResult result)
    {
        _seq?.Kill();
        _pendingResult = result;
        SetPanelVisible(true);

        if (titleText != null && timeSystem != null)
            titleText.text = string.Format(titleFormat, timeSystem.Year);

        HideAll();
        if (confirmButton != null) confirmButton.interactable = false;
        PrepRankText(result);   // 순위 텍스트 색/크기 미리 세팅

        _seq = DOTween.Sequence()
            .AppendCallback(() => Appear(titleRow))
            .AppendInterval(stepDelay)
            .AppendCallback(() => Appear(revenueRow))
            .Append(CountUp(revenueText, result.Revenue, "원"))
            .AppendInterval(stepDelay)
            .AppendCallback(() => Appear(reputationRow))
            .Append(CountUp(reputationText, result.Reputation, "점"))
            .AppendInterval(stepDelay)
            .AppendCallback(() => OnResultsReady?.Invoke())   // 편지 애니 시작 신호 (여기서 멈춤)
            .OnComplete(() => _seq = null)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    /// <summary>(Director용) 순위 공개 + 확인 버튼. 편지 애니가 다 끝난 뒤 호출.</summary>
    public void RevealRank()
    {
        _seq?.Kill();
        string rankLabel = BuildRankLabel(_pendingResult);

        _seq = DOTween.Sequence()
            .AppendCallback(() => {
                OnRankReveal?.Invoke();
                Appear(rankRow, emphasizeRank ? rankPunchStrength : punchStrength);
                if (rankText != null) rankText.text = rankLabel;
            })
            .AppendInterval(stepDelay)
            .AppendCallback(() => {
                Appear(confirmRow);
                if (confirmButton != null) confirmButton.interactable = true;
            })
            .OnComplete(() => _seq = null)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    /// <summary>순위 텍스트 색/크기를 등수 티어에 맞게 미리 세팅.</summary>
    private void PrepRankText(RankingSystem.YearResult result)
    {
        var style = PickRankStyle(result);
        if (rankText != null)
        {
            rankText.color = style.color;
            rankText.transform.localScale = Vector3.one * style.fontScale;
        }
    }

    /// <summary>등수 라벨 문자열 (labelOverride 있으면 그것, 없으면 "N등").</summary>
    private string BuildRankLabel(RankingSystem.YearResult result)
    {
        var style = PickRankStyle(result);
        return !string.IsNullOrEmpty(style.labelOverride) ? style.labelOverride : $"{result.Rank}등";
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
        OnClosed?.Invoke();                 // 시상식 무대 정리
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelGroup == null) return;
        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }
}
