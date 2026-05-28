using DG.Tweening;
using UnityEngine;
using TMPro;

public class HudView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI satisfactionText;
    [SerializeField] private TextMeshProUGUI reputationText;

    [SerializeField] private TimeSystem timeSystem;
    [SerializeField] private TextMeshProUGUI dateText; // 년/월 (예: "1년 05월")
    [SerializeField] private TextMeshProUGUI timeText; // 시각 (예: "12:00")

    [Header("카운트업 보간 시간 (초)")]
    [SerializeField] private float moneyTweenDuration = 0.4f;
    [SerializeField] private float satisfactionTweenDuration = 0.3f;
    [SerializeField] private float reputationTweenDuration = 0.4f;

    // 현재 표시중인 값 (트윈 from 기준점). 초기엔 0이고 첫 갱신 때 즉시 셋팅.
    private long _displayedMoney;
    private int _displayedSatisfaction;
    private long _displayedReputation;
    private bool _initialized;

    private Tween _moneyTween;
    private Tween _satisfactionTween;
    private Tween _reputationTween;

    private void Start()
    {
        MoneySystem.Instance.OnMoneyChanged += UpdateMoney;
        SatisfactionSystem.Instance.OnSatisfactionChanged += UpdateSatisfaction;
        ReputationSystem.Instance.OnReputationChanged += UpdateReputation;
        timeSystem.OnHourChanged += UpdateTime;

        // 첫 갱신은 카운트업 없이 즉시 표시 (게임 시작 시 0에서 굴러올라가지 않게)
        _displayedMoney = MoneySystem.Instance.Money;
        _displayedSatisfaction = SatisfactionSystem.Instance.Satisfaction;
        _displayedReputation = ReputationSystem.Instance.AnnualReputation;
        _initialized = true;
        if (moneyText != null) moneyText.text = $"{_displayedMoney:N0}";
        if (satisfactionText != null) satisfactionText.text = $"{_displayedSatisfaction}";
        if (reputationText != null) reputationText.text = $"Rep : {_displayedReputation:N0}";
        UpdateTime();
    }

    // 슬롯이 비어있어도 NullReference 안 나게 보호 처리
    private void UpdateMoney(long money)
    {
        if (moneyText == null) return;
        if (!_initialized) { moneyText.text = $"{money:N0}"; _displayedMoney = money; return; }

        _moneyTween?.Kill();
        long from = _displayedMoney;
        _moneyTween = DOTween.To(() => (float)from, v =>
        {
            long shown = (long)v;
            moneyText.text = $"{shown:N0}";
        }, money, moneyTweenDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => { _displayedMoney = money; moneyText.text = $"{money:N0}"; });
    }

    private void UpdateSatisfaction(int satisfaction)
    {
        if (satisfactionText == null) return;
        if (!_initialized) { satisfactionText.text = $"{satisfaction}"; _displayedSatisfaction = satisfaction; return; }

        _satisfactionTween?.Kill();
        int from = _displayedSatisfaction;
        _satisfactionTween = DOTween.To(() => (float)from, v =>
        {
            satisfactionText.text = $"{(int)v}";
        }, satisfaction, satisfactionTweenDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => { _displayedSatisfaction = satisfaction; satisfactionText.text = $"{satisfaction}"; });
    }

    private void UpdateReputation(long reputation)
    {
        if (reputationText == null) return;
        if (!_initialized) { reputationText.text = $"Rep : {reputation:N0}"; _displayedReputation = reputation; return; }

        _reputationTween?.Kill();
        long from = _displayedReputation;
        _reputationTween = DOTween.To(() => (float)from, v =>
        {
            long shown = (long)v;
            reputationText.text = $"Rep : {shown:N0}";
        }, reputation, reputationTweenDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => { _displayedReputation = reputation; reputationText.text = $"Rep : {reputation:N0}"; });
    }
    private void UpdateTime()
    {
        if (dateText != null) dateText.text = $"{timeSystem.Year}년 {timeSystem.Month:D2}월";
        if (timeText != null) timeText.text = $"{timeSystem.Hour:D2}:00";
    }

    private void OnDisable()
    {
        if (MoneySystem.Instance != null)
        {
            MoneySystem.Instance.OnMoneyChanged -= UpdateMoney;
        }
        if (SatisfactionSystem.Instance != null)
        {
            SatisfactionSystem.Instance.OnSatisfactionChanged -= UpdateSatisfaction;
        }
        if (ReputationSystem.Instance != null)
        {
            ReputationSystem.Instance.OnReputationChanged -= UpdateReputation;
        }
        if (timeSystem != null)
        {
            timeSystem.OnHourChanged -= UpdateTime;
        }

        _moneyTween?.Kill();
        _satisfactionTween?.Kill();
        _reputationTween?.Kill();
    }
}
