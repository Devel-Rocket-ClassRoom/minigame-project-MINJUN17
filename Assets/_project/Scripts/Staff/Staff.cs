using UnityEngine;

[RequireComponent(typeof(PathMover))]
public abstract class Staff : MonoBehaviour
{
    protected StaffData _data;
    [SerializeField] protected int id;
    protected string _nameKey;
    protected float _stateTimer;

    protected int _tenureMonths;
    protected int _growthBumps;
    protected float _growthMultiplier = 1f;
    protected float _hireVariance;

    protected PathMover _mover;

    [Header("운반 위치 (자식 Transform — 인스펙터에서 위치 조정)")]
    [SerializeField] protected Transform carryPoint;

    public StaffData Data => _data;
    public int Id => id;
    public string NameKey => _nameKey;
    public string Name
    {
        get
        {
            if (string.IsNullOrEmpty(_nameKey)) return "";
            return StaffManager.Instance != null
                ? StaffManager.Instance.ResolveName(_nameKey)
                : _nameKey;
        }
    }
    public int TenureMonths => _tenureMonths;
    public int GrowthBumps => _growthBumps;
    public float GrowthMultiplier => _growthMultiplier;
    public float HireVariance => _hireVariance;

    public float EffectiveMoveSpeed => _data.moveSpeed * (1f + _hireVariance);
    public long EffectiveSalary => (long)(_data.salary * (1f + _hireVariance));

    protected virtual void Awake()
    {
        _mover = GetComponent<PathMover>();
        if (_mover != null) _mover.Role = GetPathRole();
    }

    // 자식 클래스가 자기 역할 지정 (Cook/Server)
    protected abstract PathRole GetPathRole();

    public bool CanUpgrade
    {
        get
        {
            if (_data.grade == StaffType.Manager) return false;
            var next = StaffManager.Instance.GetNextGrade(_data);
            if (next == null) return false;
            if (next.grade == StaffType.Senior) return _tenureMonths >= 6;
            if (next.grade == StaffType.Manager) return _tenureMonths >= 12;
            return false;
        }
    }

    public virtual void SetData(StaffData data)
    {
        _data = data;
        GetComponent<SpriteRenderer>().sprite = data.sprite;
    }

    public virtual void TickMonth()
    {
        _tenureMonths++;
        if (_growthBumps < 12)
        {
            _growthBumps++;
            _growthMultiplier = 1f + 0.03f * _growthBumps;
        }
    }

    protected void InitBase(StaffData data, int id, string nameKey, float hireVariance = 0f)
    {
        _data = data;
        this.id = id;
        _nameKey = nameKey;
        _hireVariance = hireVariance;
        GetComponent<SpriteRenderer>().sprite = data.sprite;
        _tenureMonths = 0;
        _growthBumps = 0;
        _growthMultiplier = 1f;
    }

    // FSM이 호출하는 이동 API. 매 프레임 호출 OK (재계산은 안 함).
    protected void MoveTo(Vector3 destination)
    {
        _mover.SetDestination(destination);
        _mover.Step(EffectiveMoveSpeed);
    }

    protected bool HasArrived()
    {
        return _mover.HasArrived();
    }

    /// <summary>
    /// 음식을 직원의 carryPoint 자식으로 부착. carryPoint 없으면 직원 transform 자체로 폴백.
    /// </summary>
    public void AttachFood(Food food)
    {
        if (food == null) return;
        Transform parent = carryPoint != null ? carryPoint : transform;
        food.transform.SetParent(parent, false);
        food.transform.localPosition = Vector3.zero;
    }
}
