using UnityEngine;

public abstract class Staff : MonoBehaviour
{
    protected StaffData _data;
    [SerializeField] protected int id;
    protected float _stateTimer;

    protected int _tenureMonths;
    protected int _growthBumps;
    protected float _growthMultiplier = 1f;
    protected float _hireVariance;

    public StaffData Data => _data;
    public int Id => id;
    public int TenureMonths => _tenureMonths;
    public int GrowthBumps => _growthBumps;
    public float GrowthMultiplier => _growthMultiplier;
    public float HireVariance => _hireVariance;

    public float EffectiveMoveSpeed => _data.moveSpeed * (1f + _hireVariance);
    public long EffectiveSalary => (long)(_data.salary * (1f + _hireVariance));

    public bool CanUpgrade
    {
        get
        {
            if (_data.grade == StaffType.Manager) return false;
            var next = StaffManager.Instance.GetNextGrade(_data);
            if (next == null) return false;
            if (next.grade == StaffType.Senior)  return _tenureMonths >= 6;
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

    protected void InitBase(StaffData data, int id, float hireVariance = 0f)
    {
        _data = data;
        this.id = id;
        _hireVariance = hireVariance;
        GetComponent<SpriteRenderer>().sprite = data.sprite;
        _tenureMonths = 0;
        _growthBumps = 0;
        _growthMultiplier = 1f;
    }

    protected bool MoveTowards(Vector3 target) =>
        MoveUtil.MoveTowards(transform, target, EffectiveMoveSpeed);
}
