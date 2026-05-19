using UnityEngine;

[CreateAssetMenu(fileName = "MarketingData", menuName = "Data/MarketingData")]
public class MarketingData : ScriptableObject
{
    public string marketingName;
    public int satisfactionCost;
    public float spawnBoost;
    public int durationMonths;
}
