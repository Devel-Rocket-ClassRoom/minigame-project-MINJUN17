using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "ExpansionStageData", menuName = "Expansion/ExpansionStageData")]
public class ExpansionStageData : ScriptableObject, ISaveIdentifiable
{
    [Header("Save ID (자동 채움 — 수정 X)")]
    [SerializeField] private string saveId;
    public string SaveId => saveId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(saveId))
        {
            saveId = System.Guid.NewGuid().ToString("N").Substring(0, 12);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    public LocalizedString stageName;     // 다국어 표시명
    public LocalizedString description;   // 다국어 설명 (해금 슬롯 표시용)
    public Sprite icon;                   // 해금 슬롯 아이콘
    public Vector2Int origin;     // 좌하단 셀
    public int width;
    public int height;
    public CellZone newZone;
    public long unlockCost;
    public int order;
}
