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
        // 비어있거나, 다른 FurnitureData와 중복(에셋 복제 시)이면 새 ID 발급.
        if (string.IsNullOrEmpty(saveId) || IsSaveIdDuplicated())
        {
            saveId = System.Guid.NewGuid().ToString("N").Substring(0, 12);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    private bool IsSaveIdDuplicated()
    {
        // CookingToolData 등 서브클래스 포함 전체 FurnitureData 에셋 검사.
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FurnitureData"))
        {
            var path  = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var other = UnityEditor.AssetDatabase.LoadAssetAtPath<FurnitureData>(path);
            if (other != null && other != this && other.saveId == saveId)
                return true;
        }
        return false;
    }
#endif

    public GameObject prefab;
    public PlacementZone zone;
    public int width = 1;
    public int height = 1;
    public int anchorX;
    public int anchorY;

    [Tooltip("이동만 가능 — 추가 설치/삭제 불가. 시작 배치로 1개만 존재하는 고정 가구 (예: 카운터).")]
    public bool fixedSingle;

    [Header("고정 위치 즉시 설치 (드래그 X)")]
    [Tooltip("체크 시 카탈로그 클릭하면 fixedCell 위치에 즉시 spawn. 이동/삭제/회전 불가, 1회만 설치 가능.")]
    public bool fixedPlacement;
    [Tooltip("fixedPlacement=true일 때 설치될 셀 좌표 (좌하단 origin)")]
    public Vector2Int fixedCell;

    [Header("위치 보정")]
    [Tooltip("셀 중앙에서 추가로 줄 월드 오프셋 (스프라이트 미세조정용). 드래그 배치/이동/로드 전부 적용됨.")]
    public Vector2 fixedWorldOffset;

    [Header("카탈로그 / 해금")]
    public LocalizedString displayName;           // 상점 슬롯 표시명 (다국어)
    public LocalizedString description;           // 다국어 설명 (해금 슬롯 표시용)
    public Sprite icon;                           // 상점 슬롯 아이콘
    public int satisfactionUnlock;                // 0이면 시작 해금 가능 (좌석 등 기본 가구) — 카탈로그 1회 해금 비용
    public long purchaseCost;                     // 설치 1회당 돈 비용 (0이면 무료)
    public ExpansionStageData unlockOnExpansion;  // 이 확장 단계 활성화 시 자동 해금
}
