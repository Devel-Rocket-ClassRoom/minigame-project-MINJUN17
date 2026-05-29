using UnityEngine;

public class CookingToolInstance : MonoBehaviour
{
    public CookingToolData data;

    [Tooltip("요리사가 서서 조리할 위치. 비워두면 자동으로 인접 walkable 셀 계산")]
    [SerializeField] private Transform usePoint;
    public Transform UsePoint => usePoint;

    [SerializeField] private Animator animator;
    private bool _canAnimate;
    private static readonly int CookingId = Animator.StringToHash("IsCooking");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // Animator 없거나, 컨트롤러 없거나, IsCooking 파라미터 없으면 애니 비활성 (도구마다 자유롭게 섞어 써도 안전)
        _canAnimate = animator != null
                   && animator.runtimeAnimatorController != null
                   && HasParam(animator, "IsCooking");
    }

    private void Start()
    {
        CookingToolManager.Instance.Register(this);
    }

    private void OnDestroy() => CookingToolManager.Instance?.Unregister(this);

    public void SetCooking(bool on)
    {
        if (_canAnimate) animator.SetBool(CookingId, on);   // 애니 없는 도구는 무시
    }

    private static bool HasParam(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.name == name) return true;
        return false;
    }
}