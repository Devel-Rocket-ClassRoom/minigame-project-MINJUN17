using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "MarketingData", menuName = "Data/MarketingData")]
public class MarketingData : ScriptableObject
{
    public LocalizedString marketingName;       // 다국어 표시명
    public LocalizedString description;         // 다국어 설명 (옵션 — UI 툴팁/팝업용)
    public int satisfactionCost;
    public float spawnBoost;
    public int durationMonths;
}
