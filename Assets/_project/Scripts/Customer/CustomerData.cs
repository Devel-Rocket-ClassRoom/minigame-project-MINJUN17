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

    [Tooltip("체크 시 게임 시작부터 등장. 해제 시 preferredMenu가 해금돼야 등장")]
    public bool unlockedFromStart = true;

    [Tooltip("이 손님이 선호하는 음식. 잠긴 손님은 이 음식이 해금되면 자동 해금됨. 시작 손님은 비워두면 선호 없음(아무거나 먹음)")]
    public MenuData preferredMenu;

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

    [Header("화장실")]
    [Tooltip("식사 후 화장실 가고 싶어할 확률 (0~1)")]
    [Range(0f, 1f)] public float toiletUrgeProbability = 0.3f;
    [Tooltip("변기 사용 시간 (초)")]
    public float toiletUseDuration = 5f;
    [Tooltip("세면대 사용 시간 (초)")]
    public float sinkUseDuration = 2f;
    [Tooltip("변기 사용 완료 시 만족도 보너스")]
    public int toiletStallBonus = 10;
    [Tooltip("세면대 사용 완료 시 만족도 보너스 (독립 — 변기와 합산)")]
    public int sinkBonus = 5;
}
