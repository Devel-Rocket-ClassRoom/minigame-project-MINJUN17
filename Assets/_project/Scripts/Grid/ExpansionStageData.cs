using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "ExpansionStageData", menuName = "Expansion/ExpansionStageData")]
public class ExpansionStageData : ScriptableObject, ISaveIdentifiable
{
    public enum StageType
    {
        CellExpansion,   // 셀 활성화 + 해금 트리거 (예: 2단 2층, 3단 화장실)
        UnlockOnly       // 셀 확장 없이 카탈로그 해금만 (예: 1단 DT)
    }

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

    [Header("Stage Type")]
    [Tooltip("CellExpansion: 셀 활성화 + 해금 트리거 (origin/width/height/newZone 사용)\n" +
             "UnlockOnly: 셀 확장 없이 해금만 (예: 1단 DT 카탈로그)")]
    public StageType stageType = StageType.CellExpansion;

    [Header("Cell Expansion (StageType=CellExpansion 일 때만 사용)")]
    public Vector2Int origin;     // 좌하단 셀
    public int width;
    public int height;
    public CellZone newZone;

    [Header("Cost")]
    public long unlockCost;
    public int order;
}
