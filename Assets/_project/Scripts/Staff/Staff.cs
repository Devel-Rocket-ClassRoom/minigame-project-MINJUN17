using System.Collections.Generic;
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

    // 통근 경유 웨이포인트(문/계단) + 최종 목적지. from에서 가까운 문부터 순서대로.
    private readonly List<Vector3> _commuteWaypoints = new();
    private int _commuteWpIndex;
    // 계단 웨이포인트 도착 시 텔레포트할 착지 위치 (index → landing)
    private readonly Dictionary<int, Vector3> _commuteTeleports = new();

    // 사이드워크 ↔ 가게 안 횡단 시 정문 (0,1) 경유
    private static readonly Vector3 DoorWorld = new Vector3(0.5f, 1.5f, 0f);

    // 출퇴근(통근) 시 이동 속도 배수 — 근무 중보다 빠르게 출퇴근
    private const float kCommuteSpeedMult = 2f;

    public bool IsCommuting => _commute != CommuteState.NONE;
    /// <summary>퇴근(출구로 이동) 중인지. 새 영업일 오픈 시 되돌려 재출근시키는 판정에 사용.</summary>
    public bool IsLeaving => _commute == CommuteState.LEAVING;

    // 서브클래스가 자기 근무지 좌표 / 도착 시 IDLE 진입을 구현
    protected abstract Vector3 GetWorkPosition();
    protected abstract void OnArrivedAtWork();

    // 가게 안↔밖 횡단인지 (x 기준). 같은 쪽이면 정문 경유 불필요.
    private static bool DoorCrossingNeeded(Vector3 from, Vector3 to)
        => (from.x >= 0f) != (to.x >= 0f);

    // 주방↔홀 경계를 넘는지 (1층 주방 y범위 기준). 출퇴근만 주방문으로 다니게 하기 위함.
    private static bool KitchenCrossingNeeded(Vector3 from, Vector3 to)
    {
        var gm = GridManager.Instance;
        if (gm == null) return false;
        int b = gm.KitchenBoundaryY, top = gm.Floor1Height;
        bool fromK = from.y >= b && from.y < top;
        bool toK   = to.y   >= b && to.y   < top;
        return fromK != toK;
    }

    private static Vector3 KitchenDoorWorld()
    {
        var gm = GridManager.Instance;
        return gm != null ? gm.CellToWorld(gm.KitchenDoorCell) : new Vector3(1.5f, 8.5f, 0f);
    }

    // from→to 경로에 필요한 웨이포인트를 구성 + 최종 목적지.
    // 2층 출발 시 계단 경유 후 텔레포트 → 이후 정문/주방문 경유 처리.
    private void BuildCommuteWaypoints(Vector3 from, Vector3 to)
    {
        _commuteWaypoints.Clear();
        _commuteWpIndex = 0;
        _commuteTeleports.Clear();

        // 2층에서 출발하는 경우 계단 먼저 경유
        var gm = GridManager.Instance;
        if (gm != null && gm.GetFloorAt(from) == FloorIndex.Floor2)
        {
            var stair = StairManager.Instance?.FindNearestStairOnFloor(FloorIndex.Floor2, from);
            if (stair != null && stair.HasPair)
            {
                Vector3 approachPos = stair.GetApproachPos(GetPathRole(), from);
                Vector3 landingPos  = stair.GetTeleportLandingPos(GetPathRole(), from);
                _commuteWaypoints.Add(approachPos);
                _commuteTeleports[0] = landingPos;
                from = landingPos; // 이후 경유지는 착지점 기준으로 계산
            }
        }

        // 정문/주방문 경유지 추가 후 from(착지점) 기준 거리 정렬
        var doors = new List<Vector3>();
        if (DoorCrossingNeeded(from, to))    doors.Add(DoorWorld);
        if (KitchenCrossingNeeded(from, to)) doors.Add(KitchenDoorWorld());
        doors.Sort((a, c) => Vector3.Distance(from, a).CompareTo(Vector3.Distance(from, c)));
        _commuteWaypoints.AddRange(doors);

        _commuteWaypoints.Add(to);
    }

    /// <summary>영업 시작: 입구에서 등장 → (정문/주방문 경유) → 근무지로 이동.</summary>
    public void BeginArriving(Vector3 entryPos)
    {
        gameObject.SetActive(true);
        transform.position = entryPos;
        _mover.Role = PathRole.Commute;
        _mover.Clear();
        BuildCommuteWaypoints(entryPos, GetWorkPosition());
        _commute = CommuteState.ARRIVING;
    }

    /// <summary>영업 종료: (주방문/정문 경유) → 입구로 이동 → 도착하면 숨김(다음날 재입장).</summary>
    public void BeginLeaving(Vector3 exitPos)
    {
        if (!gameObject.activeSelf) return;
        _mover.Role = PathRole.Commute;
        _mover.Clear();
        BuildCommuteWaypoints(transform.position, exitPos);
        _commute = CommuteState.LEAVING;
    }

    /// <summary>로드 직후: 출퇴근 연출 없이 근무지에 바로 배치 + IDLE 진입.
    /// (세이브 로드 시 직원들이 입구에서 우르르 등장→복귀하는 현상 방지)</summary>
    public void SnapToWorkPosition()
    {
        gameObject.SetActive(true);
        _commute = CommuteState.NONE;
        _mover.Role = GetPathRole();
        _mover.Clear();
        Vector3 wp = GetWorkPosition();
        if (wp != Vector3.zero) transform.position = wp;
        OnArrivedAtWork();   // 서브클래스 IDLE 진입
    }

    /// <summary>각 서브클래스 Update 맨 위에서 호출. true면 통근 중이라 기존 FSM 정지.</summary>
    protected bool TickCommute()
    {
        if (_commute == CommuteState.NONE) return false;
        if (_commuteWaypoints.Count == 0) { FinishCommute(); return true; }

        Vector3 dest = _commuteWaypoints[Mathf.Min(_commuteWpIndex, _commuteWaypoints.Count - 1)];
        // 출퇴근은 근무 이동보다 빠르게 (kCommuteSpeedMult 배)
        _mover.SetDestination(dest);
        _mover.Step(EffectiveMoveSpeed * kCommuteSpeedMult);

        if (HasArrived())
        {
            // 계단 웨이포인트 도착 시 텔레포트
            if (_commuteTeleports.TryGetValue(_commuteWpIndex, out Vector3 landing))
            {
                transform.position = landing;
                _mover.Clear();
            }
            _commuteWpIndex++;
            if (_commuteWpIndex >= _commuteWaypoints.Count) FinishCommute();
        }
        return true;
    }

    private void FinishCommute()
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

    public bool CanUpgrade
    {
        get
        {
            if (_data.grade == StaffType.Manager) return false;
            var next = StaffManager.Instance.GetNextGrade(_data);
            if (next == null) return false;
            if (next.grade == StaffType.Senior) return _tenureMonths >= 12;
            if (next.grade == StaffType.Manager) return _tenureMonths >= 24;
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
