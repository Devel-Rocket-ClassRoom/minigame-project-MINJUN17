using System.Collections.Generic;
using UnityEngine;

public class StaffManager : MonoBehaviour
{
    public static StaffManager Instance;

    [SerializeField] private CookStaff cookStaffPrefab;
    [SerializeField] private ServerStaff serverStaffPrefab;
    [SerializeField] private RiderStaff riderStaffPrefab;
    [SerializeField] private StaffData starterCookData;
    [SerializeField] private StaffData starterServerData;
    [SerializeField] private TimeSystem timeSystem;

    [Header("등급별 SO (Junior, Senior, Manager 순)")]
    [SerializeField] private List<StaffData> cookGrades;
    [SerializeField] private List<StaffData> serverGrades;
    [SerializeField] private List<StaffData> riderGrades;

    [Header("라이더 상한")]
    [SerializeField] private int maxRiderCount = 3;

    [Header("이름 풀")]
    [SerializeField] private StaffNamePool namePool;

    /// <summary>채용/해고/승급 시 발화 — UI 갱신 신호.</summary>
    public event System.Action OnRosterChanged;

    private int nextId = 1;
    private readonly List<CookStaff> cookStaffs = new();
    private readonly List<ServerStaff> serverStaffs = new();
    private readonly List<RiderStaff> riderStaffs = new();

    public IReadOnlyList<CookStaff> CookStaffs => cookStaffs;
    public IReadOnlyList<ServerStaff> ServerStaffs => serverStaffs;
    public IReadOnlyList<RiderStaff> RiderStaffs => riderStaffs;
    public int RiderCount => riderStaffs.Count;
    public int TotalStaffCount => cookStaffs.Count + serverStaffs.Count + riderStaffs.Count;

    /// <summary>Cook → Server → Rider 순으로 전체 직원 순회.</summary>
    public IEnumerable<Staff> GetAllStaffs()
    {
        foreach (var s in cookStaffs)   yield return s;
        foreach (var s in serverStaffs) yield return s;
        foreach (var s in riderStaffs)  yield return s;
    }

    private string PickNameKey() => namePool != null ? namePool.PickRandomKey() : null;

    /// <summary>UI / Staff.Name 에서 호출 — 키를 현재 로케일 문자열로.</summary>
    public string ResolveName(string nameKey) =>
        namePool != null ? namePool.Resolve(nameKey) : nameKey;

    public int MaxServerCount => CounterManager.Instance.CounterCount;
    public int MaxCookCount => CounterManager.Instance.CounterCount;
    public int MaxRiderCount => maxRiderCount;

    public bool IsRiderHiringUnlocked =>
        PhoneManager.Instance != null && PhoneManager.Instance.IsRiderHiringUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (timeSystem != null) timeSystem.OnDayStarted += MonthTick;
    }

    private void OnDestroy()
    {
        if (timeSystem != null) timeSystem.OnDayStarted -= MonthTick;
    }

    private void MonthTick()
    {
        foreach (var c in cookStaffs)   c.TickMonth();
        foreach (var s in serverStaffs) s.TickMonth();
        foreach (var r in riderStaffs)  r.TickMonth();
        OnRosterChanged?.Invoke();
    }

    public void Init()
    {
        HireCookStaff(starterCookData);

        int targetServers = CounterManager.Instance.CounterCount;
        for (int i = 0; i < targetServers; i++)
            if (HireServerStaff(starterServerData) == null) break;
    }

    public CookStaff HireCookStaff(StaffData data, float hireVariance = 0f)
    {
        if (data == null || cookStaffPrefab == null) return null;
        if (cookStaffs.Count >= MaxCookCount) return null;
        if (!CanAfford(data.hireCost)) return null;

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(cookStaffPrefab);
        staff.gameObject.name = $"Cook_{nextId}";
        staff.Init(data, nextId, PickNameKey(), hireVariance);
        nextId++;
        cookStaffs.Add(staff);
        OnRosterChanged?.Invoke();
        return staff;
    }

    public ServerStaff HireServerStaff(StaffData data, float hireVariance = 0f)
    {
        if (data == null || serverStaffPrefab == null) return null;
        if (serverStaffs.Count >= MaxServerCount) return null;
        if (!CanAfford(data.hireCost)) return null;

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(serverStaffPrefab);
        staff.gameObject.name = $"Server_{nextId}";
        staff.Init(data, nextId, PickNameKey(), hireVariance);
        nextId++;
        serverStaffs.Add(staff);
        OnRosterChanged?.Invoke();
        return staff;
    }

    public RiderStaff HireRiderStaff(StaffData data, float hireVariance = 0f)
    {
        if (data == null || riderStaffPrefab == null) return null;
        if (!IsRiderHiringUnlocked) return null;
        if (RiderRoomManager.Instance != null && !RiderRoomManager.Instance.HasRiderRoom()) return null;
        if (riderStaffs.Count >= MaxRiderCount) return null;
        if (!CanAfford(data.hireCost)) return null;

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(riderStaffPrefab);
        staff.gameObject.name = $"Rider_{nextId}";

        // 라이더룸 빈 셀에 초기 배치
        var candidates = GridManager.Instance.GetWalkableCellsInZone(CellZone.RiderRoom);
        var occupiers = new List<Vector3>();
        foreach (var r in riderStaffs) occupiers.Add(r.transform.position);
        Vector3 spawnPos = RestSpotPicker.PickClosestFree(
            staff.transform.position, candidates, occupiers, 0.5f);
        if (spawnPos != Vector3.zero) staff.transform.position = spawnPos;

        staff.Init(data, nextId, PickNameKey(), hireVariance);
        nextId++;
        riderStaffs.Add(staff);
        OnRosterChanged?.Invoke();
        return staff;
    }

    public bool FireCookStaff(CookStaff staff)
    {
        if (staff == null || !cookStaffs.Contains(staff)) return false;
        long severance = staff.EffectiveSalary;
        if (!CanAfford(severance)) return false;

        MoneySystem.Instance.Spend(severance);
        cookStaffs.Remove(staff);
        Destroy(staff.gameObject);
        OnRosterChanged?.Invoke();
        return true;
    }

    public bool FireServerStaff(ServerStaff staff)
    {
        if (staff == null || !serverStaffs.Contains(staff)) return false;
        long severance = staff.EffectiveSalary;
        if (!CanAfford(severance)) return false;

        MoneySystem.Instance.Spend(severance);
        serverStaffs.Remove(staff);
        Destroy(staff.gameObject);
        OnRosterChanged?.Invoke();
        return true;
    }

    public bool FireRiderStaff(RiderStaff staff)
    {
        if (staff == null || !riderStaffs.Contains(staff)) return false;
        long severance = staff.EffectiveSalary;
        if (!CanAfford(severance)) return false;

        MoneySystem.Instance.Spend(severance);
        riderStaffs.Remove(staff);
        Destroy(staff.gameObject);
        OnRosterChanged?.Invoke();
        return true;
    }

    /// <summary>UI용 통합 해고 — Staff 타입을 모르고 호출 가능.</summary>
    public bool Fire(Staff staff) => staff switch
    {
        CookStaff c   => FireCookStaff(c),
        ServerStaff s => FireServerStaff(s),
        RiderStaff r  => FireRiderStaff(r),
        _ => false
    };

    /// <summary>UI용 통합 승급.</summary>
    public bool Upgrade(Staff staff) => staff switch
    {
        CookStaff c   => UpgradeCook(c),
        ServerStaff s => UpgradeServer(s),
        RiderStaff r  => UpgradeRider(r),
        _ => false
    };

    public long CalculateTotalSalaryCost()
    {
        long salary = 0;
        foreach (var cook in cookStaffs)     salary += cook.EffectiveSalary;
        foreach (var server in serverStaffs) salary += server.EffectiveSalary;
        foreach (var rider in riderStaffs)   salary += rider.EffectiveSalary;
        return salary;
    }

    public StaffData GetGrade(StaffRole role, StaffType grade)
    {
        var list = GetGradeList(role);
        if (list == null) return null;
        foreach (var d in list) if (d != null && d.grade == grade) return d;
        return null;
    }

    public StaffData GetNextGrade(StaffData current)
    {
        if (current == null || current.grade == StaffType.Manager) return null;
        var nextGrade = (StaffType)((int)current.grade + 1);
        return GetGrade(current.role, nextGrade);
    }

    private List<StaffData> GetGradeList(StaffRole role) => role switch
    {
        StaffRole.Cook => cookGrades,
        StaffRole.Server => serverGrades,
        StaffRole.Rider => riderGrades,
        _ => null,
    };

    public bool UpgradeCook(CookStaff staff)
    {
        if (staff == null || !staff.CanUpgrade) return false;
        var next = GetNextGrade(staff.Data);
        if (next == null) return false;

        long cost = next.hireCost / 2;
        if (!CanAfford(cost)) return false;

        MoneySystem.Instance.Spend(cost);
        staff.SetData(next);
        OnRosterChanged?.Invoke();
        return true;
    }

    public bool UpgradeServer(ServerStaff staff)
    {
        if (staff == null || !staff.CanUpgrade) return false;
        var next = GetNextGrade(staff.Data);
        if (next == null) return false;

        long cost = next.hireCost / 2;
        if (!CanAfford(cost)) return false;

        MoneySystem.Instance.Spend(cost);
        staff.SetData(next);
        OnRosterChanged?.Invoke();
        return true;
    }

    public bool UpgradeRider(RiderStaff staff)
    {
        if (staff == null || !staff.CanUpgrade) return false;
        var next = GetNextGrade(staff.Data);
        if (next == null) return false;

        long cost = next.hireCost / 2;
        if (!CanAfford(cost)) return false;

        MoneySystem.Instance.Spend(cost);
        staff.SetData(next);
        OnRosterChanged?.Invoke();
        return true;
    }

    private bool CanAfford(long amount) =>
        MoneySystem.Instance != null && MoneySystem.Instance.CanAfford(amount);
}
