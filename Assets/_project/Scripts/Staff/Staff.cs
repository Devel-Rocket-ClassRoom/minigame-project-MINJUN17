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
    protected Animator _animator;
    protected SpriteRenderer _spriteRenderer;
    protected DirectionalCharacterAnimator _dirAnim;

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
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _dirAnim = GetComponent<DirectionalCharacterAnimator>();
    }

    /// <summary>특정 월드 지점을 바라보게 한다 (대기/작업 중 방향 고정용). 애니메이터 없으면 무시.</summary>
    protected void FaceToward(Vector3 worldPoint)
    {
        if (_dirAnim != null) _dirAnim.FaceTowards(worldPoint - transform.position);
    }

    /// <summary>
    /// 등급(StaffData)별 외형 적용.
    /// - animController가 있으면 Animator의 클립 세트를 그걸로 교체 (걷기/대기 4방향).
    /// - 없으면 단일 sprite로 폴백 (애니메이터 미설정 캐릭터 대비).
    /// 채용/로드(InitBase) · 승급(SetData) 양쪽에서 호출된다.
    /// </summary>
    protected void ApplyVisuals(StaffData data)
    {
        if (data == null) return;

        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator != null && data.animController != null)
            _animator.runtimeAnimatorController = data.animController;

        // 폴백: 애니메이터 override가 없으면 정적 스프라이트라도 세팅
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null && data.sprite != null)
            _spriteRenderer.sprite = data.sprite;
    }

    // 자식 클래스가 자기 역할 지정 (Cook/Server)
    protected abstract PathRole GetPathRole();

    // ─── 통근(출퇴근) ───
    protected enum CommuteState { NONE, ARRIVING, LEAVING }
    private CommuteState _commute = CommuteState.NONE;
    private Vector3 _commuteTarget;

    public bool IsCommuting => _commute != CommuteState.NONE;

    // 서브클래스가 자기 근무지 좌표 / 도착 시 IDLE 진입을 구현
    protected abstract Vector3 GetWorkPosition();
    protected abstract void OnArrivedAtWork();

    /// <summary>영업 시작: 입구에서 등장 → 근무지로 이동.</summary>
    public void BeginArriving(Vector3 entryPos)
    {
        gameObject.SetActive(true);
        transform.position = entryPos;
        _commuteTarget = GetWorkPosition();
        _mover.Role = PathRole.Commute;
        _mover.Clear();
        _commute = CommuteState.ARRIVING;
    }

    /// <summary>영업 종료: 입구로 이동 → 도착하면 숨김(다음날 재입장).</summary>
    public void BeginLeaving(Vector3 exitPos)
    {
        if (!gameObject.activeSelf) return;
        _commuteTarget = exitPos;
        _mover.Role = PathRole.Commute;
        _mover.Clear();
        _commute = CommuteState.LEAVING;
    }

    /// <summary>각 서브클래스 Update 맨 위에서 호출. true면 통근 중이라 기존 FSM 정지.</summary>
    protected bool TickCommute()
    {
        if (_commute == CommuteState.NONE) return false;

        MoveTo(_commuteTarget);
        if (HasArrived())
        {
            if (_commute == CommuteState.ARRIVING)
            {
                _commute = CommuteState.NONE;
                _mover.Role = GetPathRole();   // 근무 역할 복원
                _mover.Clear();
                OnArrivedAtWork();             // 서브클래스 IDLE 진입
            }
            else // LEAVING
            {
                _commute = CommuteState.NONE;
                gameObject.SetActive(false);   // 퇴장 → 숨김
            }
        }
        return true;
    }

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
        ApplyVisuals(data);
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
        ApplyVisuals(data);
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

    // ─── Save / Load ───
    public StaffSaveData ToData(string roleName)
    {
        var pos = transform.position;
        return new StaffSaveData
        {
            role            = roleName,
            staffDataSaveId = _data is ISaveIdentifiable ident ? ident.SaveId : null,
            nameKey         = _nameKey,
            id              = id,
            tenureMonths    = _tenureMonths,
            growthBumps     = _growthBumps,
            hireVariance    = _hireVariance,
            posX = pos.x, posY = pos.y, posZ = pos.z,
        };
    }

    public void FromData(StaffSaveData data)
    {
        var staffData = SaveIdRegistry.GetById<StaffData>(data.staffDataSaveId);
        if (staffData == null)
        {
            Debug.LogWarning($"[Staff] StaffData 못 찾음: {data.staffDataSaveId}");
            return;
        }

        InitBase(staffData, data.id, data.nameKey, data.hireVariance);
        _tenureMonths     = data.tenureMonths;
        _growthBumps      = data.growthBumps;
        _growthMultiplier = 1f + 0.03f * _growthBumps;
        transform.position = new Vector3(data.posX, data.posY, data.posZ);
    }
}
