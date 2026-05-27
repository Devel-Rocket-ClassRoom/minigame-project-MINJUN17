using UnityEngine;

/// <summary>
/// 이동 방향에 따라 Animator 파라미터를 갱신하는 공용 컴포넌트.
///
/// transform.position 의 프레임 간 변화량으로 "어느 방향으로 움직이는지 / 움직이는 중인지"를
/// 스스로 판단하므로, PathMover · FSM 종류와 무관하게 움직이는 캐릭터(직원/손님 등)에
/// 붙이기만 하면 동작한다. (DTCustomer와 동일한 Direction 규약 사용)
///
/// 필요한 Animator 파라미터:
///   - "Direction" (Int)            : 0=오른쪽, 1=위, 2=왼쪽, 3=아래
///   - "Moving"    (Bool)           : 이동 중이면 true (idle ↔ walk 전환용)
///   - "Turn"      (Trigger, 선택)  : 방향이 바뀌는 순간 1회 발동 (필요 없으면 안 만들어도 됨)
///
/// Animator Controller 쪽에서 이 파라미터들로 idle/walk × 4방향 상태를 구성하면 된다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class DirectionalCharacterAnimator : MonoBehaviour
{
    // Animator "Direction" Int 값 (DTCustomer와 동일)
    public const int DIR_RIGHT = 0;
    public const int DIR_UP    = 1;
    public const int DIR_LEFT  = 2;
    public const int DIR_DOWN  = 3;

    private static readonly int kHashDirection = Animator.StringToHash("Direction");
    private static readonly int kHashMoving    = Animator.StringToHash("Moving");
    private static readonly int kHashTurn      = Animator.StringToHash("Turn");

    [Tooltip("이 속도(유닛/초) 이상으로 움직이면 '이동 중'으로 판단. 프레임레이트와 무관하게 동작. " +
             "미세 떨림만 거르면 되므로 작은 값이면 충분")]
    [SerializeField] private float moveSpeedThreshold = 0.05f;

    [Tooltip("시작 시 바라볼 기본 방향 (0=오른쪽,1=위,2=왼쪽,3=아래)")]
    [SerializeField] private int defaultDirection = DIR_DOWN;

    private Animator _animator;
    private Vector3 _prevPos;
    private int _currentDir = -1;     // -1 = 미지정

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _prevPos = transform.position;
        ApplyDirection(defaultDirection, fireTurn: false);
    }

    // 풀링/재활성화·텔레포트 직후 한 프레임의 큰 delta로 오판하지 않도록 기준 위치 리셋
    private void OnEnable() => _prevPos = transform.position;

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || _animator == null) return;   // 일시정지(timeScale 0) 등

        Vector3 delta = transform.position - _prevPos;
        _prevPos = transform.position;

        float speed = delta.magnitude / dt;          // 유닛/초 — 프레임레이트 무관
        bool moving = speed > moveSpeedThreshold;

        if (moving)
        {
            int dir = ComputeDirection(delta);
            if (dir != _currentDir)
            {
                bool wasUnset = _currentDir < 0;
                _currentDir = dir;
                if (!wasUnset) _animator.SetTrigger(kHashTurn);
            }
        }

        // 매 프레임 푸시 — 런타임에 Override Controller가 교체돼 파라미터가 리셋돼도 즉시 복구됨
        _animator.SetInteger(kHashDirection, _currentDir < 0 ? defaultDirection : _currentDir);
        _animator.SetBool(kHashMoving, moving);
    }

    private static int ComputeDirection(Vector3 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0 ? DIR_RIGHT : DIR_LEFT;
        return v.y >= 0 ? DIR_UP : DIR_DOWN;
    }

    private void ApplyDirection(int newDir, bool fireTurn)
    {
        if (newDir == _currentDir) return;
        bool wasUnset = _currentDir < 0;
        _currentDir = newDir;

        if (_animator == null) return;
        _animator.SetInteger(kHashDirection, newDir);
        if (fireTurn && !wasUnset)
            _animator.SetTrigger(kHashTurn);
    }

    /// <summary>현재 바라보는 방향 (0=R,1=U,2=L,3=D). 외부에서 필요 시 참조.</summary>
    public int CurrentDirection => _currentDir;

    /// <summary>외부에서 강제로 방향을 지정 (예: 대기 중 카운터 쪽 바라보게). Turn 트리거도 발동.</summary>
    public void FaceDirection(int dir) => ApplyDirection(dir, fireTurn: true);

    /// <summary>방향 벡터로 강제 지정.</summary>
    public void FaceTowards(Vector3 worldDir)
    {
        if (worldDir.sqrMagnitude < 1e-6f) return;
        ApplyDirection(ComputeDirection(worldDir), fireTurn: true);
    }
}
