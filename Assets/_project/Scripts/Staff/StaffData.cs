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
    [Tooltip("애니메이터가 없을 때(정적 스프라이트) 또는 폴백용으로 쓰이는 단일 스프라이트")]
    public Sprite sprite;

    [Tooltip("등급별 걷기/대기 클립 세트. 비워두면 위 sprite 한 장을 그대로 사용. " +
             "프리팹 Animator의 '기본 컨트롤러'를 베이스로 만든 Animator Override Controller를 연결")]
    public AnimatorOverrideController animController;

    [Header("능력치")]
    public float moveSpeed;
    public float kindness;
    public float deliveryTime; // 라이더만 의미
    public float speedMultiplier;
    public long hireCost;
    public long salary;
}
