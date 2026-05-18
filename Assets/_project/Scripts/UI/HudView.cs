using UnityEngine;
using TMPro;

public class HudView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI satisfactionText;
    private void Start()
    {
        MoneySystem.Instance.OnMoneyChanged += UpdateMoney;
        SatisfactionSystem.Instance.OnSatisfactionChanged += UpdateSatisfaction;
        UpdateMoney(MoneySystem.Instance.Money);
        UpdateSatisfaction(SatisfactionSystem.Instance.Satisfaction);
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
    }

    private void UpdateMoney(long money) => moneyText.text = $"₩ {money:N0}";
    private void UpdateSatisfaction(int satisfaction) => satisfactionText.text = $"Satis : {satisfaction}";
}
