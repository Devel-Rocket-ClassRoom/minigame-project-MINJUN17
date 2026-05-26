using UnityEngine;

[CreateAssetMenu(fileName = "StaffData", menuName = "Staff/StaffData")]
public class StaffData : ScriptableObject, ISaveIdentifiable
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

    public StaffRole role;           // Cook / Server / Rider
    public StaffType grade;          // Junior / Senior / Manager 

    [Header("외형")]
    public Sprite sprite;

    [Header("능력치")]
    public float moveSpeed;
    public float kindness;
    public float deliveryTime; // 라이더만 의미
    public float speedMultiplier;
    public long hireCost;
    public long salary;
}
