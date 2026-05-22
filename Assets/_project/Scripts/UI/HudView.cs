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
    private void Start()
    {
        MoneySystem.Instance.OnMoneyChanged += UpdateMoney;
        SatisfactionSystem.Instance.OnSatisfactionChanged += UpdateSatisfaction;
        ReputationSystem.Instance.OnReputationChanged += UpdateReputation;
        timeSystem.OnHourChanged += UpdateTime;

        UpdateMoney(MoneySystem.Instance.Money);
        UpdateSatisfaction(SatisfactionSystem.Instance.Satisfaction);
        UpdateReputation(ReputationSystem.Instance.AnnualReputation);
        UpdateTime();
    }

    // 슬롯이 비어있어도 NullReference 안 나게 보호 처리
    private void UpdateMoney(long money)
    {
        if (moneyText != null) moneyText.text = $"{money:N0}";
    }
    private void UpdateSatisfaction(int satisfaction)
    {
        if (satisfactionText != null) satisfactionText.text = $"Satis : {satisfaction}";
    }
    private void UpdateReputation(long reputation)
    {
        if (reputationText != null) reputationText.text = $"Rep : {reputation:N0}";
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
    }

}
