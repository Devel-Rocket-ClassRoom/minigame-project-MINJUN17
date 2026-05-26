using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "FurnitureData", menuName = "Furniture/Furniture Data")]
public class FurnitureData : ScriptableObject, ISaveIdentifiable
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

    public GameObject prefab;
    public PlacementZone zone;
    public int width = 1;
    public int height = 1;
    public int anchorX;
    public int anchorY;
    public float deliveryBonus; // 라이더룸 가구만 의미 있음

    [Header("카탈로그 / 해금")]
    public LocalizedString displayName;           // 상점 슬롯 표시명 (다국어)
    public LocalizedString description;           // 다국어 설명 (해금 슬롯 표시용)
    public Sprite icon;                           // 상점 슬롯 아이콘
    public int satisfactionUnlock;                // 0이면 시작 해금 가능 (좌석 등 기본 가구) — 카탈로그 1회 해금 비용
    public long purchaseCost;                     // 설치 1회당 돈 비용 (0이면 무료)
    public ExpansionStageData unlockOnExpansion;  // 이 확장 단계 활성화 시 자동 해금
}
