using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "MenuData", menuName = "Menu/MenuData")]
public class MenuData : ScriptableObject, ISaveIdentifiable
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

    public LocalizedString menuName;      // 다국어 표시명
    public LocalizedString description;   // 다국어 설명 (해금 슬롯 표시용)
    public Sprite foodSprite;
    public int price;
    public int cost;
    public CookingToolData tool;          // 1:1
    public int satisfactionUnlock;        // 만족도 해금 비용 (선택)
    public float spawnWeight;
}
