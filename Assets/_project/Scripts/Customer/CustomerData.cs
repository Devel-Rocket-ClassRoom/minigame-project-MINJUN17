using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "CustomerData", menuName = "Customer/CustomerData")]
public class CustomerData : ScriptableObject, ISaveIdentifiable
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

    [Tooltip("체크 시 게임 시작부터 등장. 해제 시 누적 만족도 임계점(1000마다)에 도달하면 랜덤 해금")]
    public bool unlockedFromStart = true;

    public GameObject customerPrefab;
    public LocalizedString customerName;   // 다국어 표시명
    public Sprite icon;

    public int minOrderCount = 1;      // 최소 주문 개수
    public int maxOrderCount = 1;      // 최대 주문 개수

    [Header("만족도")]
    public int baseSatisfaction = 50;   // 시작 만족도
    public int eatGainRate = 5;         // 식사 중 초당 증가
    public int waitPenaltyRate = 3;     // patience 초과 1초당 감소

    public float moveSpeed = 2f;
    public float eatSpeed = 5f;
    public float patience = 20f;

    public float spawnWeight = 1f; // 스폰 매니저에서 스폰 빈도
}
