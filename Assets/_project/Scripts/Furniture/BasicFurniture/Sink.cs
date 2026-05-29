using UnityEngine;

public class Sink : MonoBehaviour
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

    [Tooltip("손님이 손 씻을 때 설 위치. 비워두면 transform.position 사용.")]
    [SerializeField] private Transform usePoint;
    public Vector3 UsePosition => usePoint != null ? usePoint.position : transform.position;

    private void OnEnable()
    {
        SinkManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        if (SinkManager.Instance != null)
            SinkManager.Instance.Unregister(this);
        _isOccupied = false;
        if (animator != null) animator.SetBool(kHashInUse, false);
    }
}
