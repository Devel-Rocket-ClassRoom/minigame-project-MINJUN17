using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "ExpansionStageData", menuName = "Expansion/ExpansionStageData")]
public class ExpansionStageData : ScriptableObject
{
    public LocalizedString stageName;   // 다국어 표시명
    public Vector2Int origin;     // 좌하단 셀
    public int width;
    public int height;
    public CellZone newZone;
    public long unlockCost;
    public int order;
}
