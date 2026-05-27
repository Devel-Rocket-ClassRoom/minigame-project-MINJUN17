using UnityEngine;

public class CookingToolInstance : MonoBehaviour
{
    public CookingToolData data;

    [Tooltip("요리사가 서서 조리할 위치. 비워두면 자동으로 인접 walkable 셀 계산")]
    [SerializeField] private Transform usePoint;
    public Transform UsePoint => usePoint;

    private void Awake() => CookingToolManager.Instance.Register(this);
    private void OnDestroy() => CookingToolManager.Instance?.Unregister(this);
}