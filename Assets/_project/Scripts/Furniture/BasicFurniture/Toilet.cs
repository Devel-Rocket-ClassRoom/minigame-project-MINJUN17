using UnityEngine;

public class Toilet : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private static readonly int kHashInUse = Animator.StringToHash("InUse");

    private bool _isOccupied;
    public bool IsOccupied => _isOccupied;
    public void Occupy() => _isOccupied = true;   // 예약만 (애니메이션 X)
    public void Release()
    {
        _isOccupied = false;
        if (animator != null) animator.SetBool(kHashInUse, false);
    }
    /// <summary>손님이 실제 도착해서 사용 시작 (애니메이션 트리거).</summary>
    public void StartUse() { if (animator != null) animator.SetBool(kHashInUse, true); }

    [Tooltip("손님이 화장실 이용 중 위치할 셀. 비워두면 transform.position 사용.")]
    [SerializeField] private Transform usePoint;
    public Vector3 UsePosition => usePoint != null ? usePoint.position : transform.position;

    private void OnEnable()
    {
        ToiletManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        if (ToiletManager.Instance != null)
            ToiletManager.Instance.Unregister(this);
        _isOccupied = false;   // 비활성화 시 점유 강제 해제
        if (animator != null) animator.SetBool(kHashInUse, false);
    }
}
