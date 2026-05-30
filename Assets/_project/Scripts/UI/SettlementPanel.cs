using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 월말 정산 패널. DayCycleController.OnSettlementReady 구독 → 자동 표시.
/// 행마다 SetActive(true) + DOPunchScale + 숫자 카운트업으로 순차 등장.
/// 확인 버튼 → DayCycleController.ConfirmSettlement().
/// </summary>
public class SettlementPanel : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private DayCycleController dayCycle;
    [SerializeField] private CanvasGroup panelGroup;   // 패널 자체에 붙인 CanvasGroup. 시작 alpha=0 권장.

    [Header("행 GameObject (순차 등장)")]
    [SerializeField] private GameObject titleRow;
    [SerializeField] private GameObject materialRow;
    [SerializeField] private GameObject salaryRow;       // 선택 — 없으면 비워둬도 됨
    [SerializeField] private GameObject operationRow;
    [SerializeField] private GameObject totalRow;
    [SerializeField] private GameObject confirmRow;

    [Header("값 텍스트")]
    [SerializeField] private TextMeshProUGUI materialText;
    [SerializeField] private TextMeshProUGUI salaryText;
    [SerializeField] private TextMeshProUGUI operationText;
    [SerializeField] private TextMeshProUGUI totalText;
    [SerializeField] private Button confirmButton;

    [Header("연출 타이밍")]
    [SerializeField] private float countUpSec = 0.6f;    // 숫자 카운트업 시간
    [SerializeField] private float stepDelay  = 0.4f;    // 다음 행까지 대기

    [Header("Punch 효과")]
    [SerializeField] private float punchStrength = 0.15f; // 살짝 튀는 강도
    [SerializeField] private float punchDuration = 0.3f;

    [Header("총합 강조 (선택)")]
    [SerializeField] private bool  emphasizeTotal = true;
    [SerializeField] private float totalPunchStrength = 0.4f;

    private Sequence _seq;

    private void OnEnable()
    {
        if (dayCycle != null) dayCycle.OnSettlementReady += Show;
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
    }

    private void OnDisable()
    {
        if (dayCycle != null) dayCycle.OnSettlementReady -= Show;
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
        _seq?.Kill();
    }

    private void Show()
    {
        _seq?.Kill();
        SetPanelVisible(true);

        var r = MoneySystem.Instance != null
                ? MoneySystem.Instance.CalculateSettlement()
                : default;

        HideAll();
        if (confirmButton != null) confirmButton.interactable = false;

        _seq = DOTween.Sequence()
            .AppendCallback(() => Appear(titleRow))
            .AppendInterval(stepDelay);

        // 1) 직원월급 — 인스펙터에 연결돼 있을 때만
        if (salaryRow != null)
        {
            _seq.AppendCallback(() => AppearItem(salaryRow))
                .Append(CountUp(salaryText, r.SalaryCost))
                .AppendInterval(stepDelay);
        }

        // 2) 재료비
        _seq.AppendCallback(() => AppearItem(materialRow))
            .Append(CountUp(materialText, r.MaterialCost))
            .AppendInterval(stepDelay)

            // 3) 임대료 (운영비)
            .AppendCallback(() => AppearItem(operationRow))
            .Append(CountUp(operationText, r.OperationCost))
            .AppendInterval(stepDelay)

            // 4) 총합 (항상 음수 — 빨강 텍스트로 인스펙터 설정)
            .AppendCallback(() => AppearItem(totalRow, emphasizeTotal ? totalPunchStrength : punchStrength))
            .Append(CountUp(totalText, r.NetProfit))
            .AppendInterval(stepDelay)

            // 5) 확인 버튼
            .AppendCallback(() => {
                Appear(confirmRow);
                if (confirmButton != null) confirmButton.interactable = true;
            })
            .OnComplete(() => _seq = null)
            .SetUpdate(true)            // 일시정지 영향 X
            .SetLink(gameObject);       // 파괴 시 자동 Kill
    }

    /// <summary>정산 내역 행: 효과음과 함께 등장 (한 줄씩 뜰 때마다).</summary>
    private void AppearItem(GameObject row) => AppearItem(row, punchStrength);

    private void AppearItem(GameObject row, float strength)
    {
        if (row == null) return;   // 연결 안 된 행은 소리도 스킵
        SoundManager.Get()?.PlaySfx(SfxId.SettlementItem);
        Appear(row, strength);
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

    /// <summary>숫자 0→target 카운트업.</summary>
    private Tween CountUp(TextMeshProUGUI text, long target)
    {
        if (text == null) return null;
        text.text = "0원";
        long current = 0;
        return DOTween.To(() => current, x => {
            current = x;
            text.text = $"{x:N0}원";
        }, target, countUpSec).SetEase(Ease.OutCubic);
    }

    private void HideAll()
    {
        SetRowActive(titleRow,     false);
        SetRowActive(materialRow,  false);
        SetRowActive(salaryRow,    false);
        SetRowActive(operationRow, false);
        SetRowActive(totalRow,     false);
        SetRowActive(confirmRow,   false);
    }

    private static void SetRowActive(GameObject row, bool active)
    {
        if (row != null) row.SetActive(active);
    }

    private void OnConfirm()
    {
        _seq?.Kill();
        SetPanelVisible(false);
        dayCycle?.ConfirmSettlement();
        SaveLoadManager.Instance?.Save();   // 월 정산 직후 자동 저장
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelGroup == null) return;
        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }
}
