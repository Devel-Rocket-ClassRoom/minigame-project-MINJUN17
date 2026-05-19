using UnityEngine;
using TMPro;

public class HudView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI satisfactionText;

    [SerializeField] private TimeSystem timeSystem;
    [SerializeField] private TextMeshProUGUI timeText;
    private void Start()
    {
        MoneySystem.Instance.OnMoneyChanged += UpdateMoney;
        SatisfactionSystem.Instance.OnSatisfactionChanged += UpdateSatisfaction;
        timeSystem.OnHourChanged += UpdateTime;

        UpdateMoney(MoneySystem.Instance.Money);
        UpdateSatisfaction(SatisfactionSystem.Instance.Satisfaction);
        UpdateTime();
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
        if (timeSystem != null)
        {
            timeSystem.OnHourChanged -= UpdateTime;
        }
    }

    private void UpdateMoney(long money) => moneyText.text = $"₩ {money:N0}";
    private void UpdateSatisfaction(int satisfaction) => satisfactionText.text = $"Satis : {satisfaction}";
    private void UpdateTime()
    {
        timeText.text = $"{timeSystem.Year}year {timeSystem.Month}month  {timeSystem.Hour:D2}:00";
    }
}
