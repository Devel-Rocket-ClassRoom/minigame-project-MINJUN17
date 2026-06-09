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

    [Header("직원 상한 (카운터 수와 무관 — 각 4명 고정)")]
    [SerializeField] private int maxCookCount = 4;
    [SerializeField] private int maxServerCount = 4;
    [SerializeField] private int maxRiderCount = 4;

    [Header("시작 직원 수")]
    [SerializeField] private int starterCookCount = 1;
    [SerializeField] private int starterServerCount = 1;

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

    // SaveIdRegistry용 — 등급 SO 리스트 노출
    public IReadOnlyList<StaffData> CookGrades   => cookGrades;
    public IReadOnlyList<StaffData> ServerGrades => serverGrades;
    public IReadOnlyList<StaffData> RiderGrades  => riderGrades;

    public int RiderCount => riderStaffs.Count;
    public int TotalStaffCount => cookStaffs.Count + serverStaffs.Count + riderStaffs.Count;

    /// <summary>Cook → Server → Rider 순으로 전체 직원 순회.</summary>
    public IEnumerable<Staff> GetAllStaffs()
    {
        foreach (var s in cookStaffs)   yield return s;
        foreach (var s in serverStaffs) yield return s;
        foreach (var s in riderStaffs)  yield return s;
    }

    public string PickNameKey() => namePool != null ? namePool.PickRandomKey() : null;

    /// <summary>UI / Staff.Name 에서 호출 — 키를 현재 로케일 문자열로.</summary>
    public string ResolveName(string nameKey) =>
        namePool != null ? namePool.Resolve(nameKey) : nameKey;

    public int MaxServerCount => maxServerCount;
    public int MaxCookCount => maxCookCount;
    public int MaxRiderCount => maxRiderCount;

    /// <summary>해당 직군에 고용 여유가 있는지 (현재 인원 &lt; 상한).</summary>
    public bool HasRoomFor(StaffRole role) => role switch
    {
        StaffRole.Cook   => cookStaffs.Count   < maxCookCount,
        StaffRole.Server => serverStaffs.Count < maxServerCount,
        StaffRole.Rider  => riderStaffs.Count  < maxRiderCount,
        _ => false,
    };

    // 라이더 고용 가능 = 전화기가 카탈로그에서 해금되었을 때
    public bool IsRiderHiringUnlocked =>
        PhoneManager.Instance != null && PhoneManager.Instance.IsUnlocked;

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

    /// <summary>영업 시작 — 미출근(숨겨진) 직원만 입구에서 등장해 근무지로.</summary>
    public void OnBusinessOpen()
    {
        if (CustomerManager.Instance == null) return;
        Vector3 entry = CustomerManager.Instance.EntryPosition;
        foreach (var s in GetAllStaffs())
        {
            if (s == null) continue;
            // 이미 출근해 근무/입장 중인 직원은 자리 유지 — 입구에서 재입장 안 시킴.
            // (튜토리얼 중 고용해 이미 들어온 직원이 튜토리얼 종료 시 또 출근하던 문제 방지)
            // 단, 아직 퇴근(LEAVING) 중이면 되돌려 재출근시킴 — 정산 직후 바로 다음날을 열면
            // 출구로 걸어가던 직원이 그대로 숨겨져 그날 내내 미복귀하던 버그 방지.
            bool workingOrArriving = s.gameObject.activeSelf && !s.IsLeaving;
            if (!workingOrArriving) s.BeginArriving(entry);
        }
    }

    /// <summary>영업 종료 — 전 직원 입구로 퇴장(도착 시 숨김).</summary>
    public void OnBusinessClose()
    {
        if (CustomerManager.Instance == null) return;
        Vector3 exit = CustomerManager.Instance.ExitPosition;
        foreach (var s in GetAllStaffs()) s.BeginLeaving(exit);
    }

    // 채용 직후 등장 처리: 영업 중이면 즉시 입구에서 입장, 아니면 숨겨뒀다 다음 영업에 입장
    private void OnHired(Staff staff)
    {
        bool open = timeSystem != null && timeSystem.IsOpen;
        if (open && CustomerManager.Instance != null)
            staff.BeginArriving(CustomerManager.Instance.EntryPosition);
        else
            staff.gameObject.SetActive(false);
    }

    public void Init()
    {
        for (int i = 0; i < starterCookCount; i++)
            if (HireCookStaff(starterCookData) == null) break;

        for (int i = 0; i < starterServerCount; i++)
            if (HireServerStaff(starterServerData) == null) break;
    }

    public CookStaff HireCookStaff(StaffData data, float hireVariance = 0f, string nameKey = null)
    {
        if (data == null || cookStaffPrefab == null) return null;
        if (cookStaffs.Count >= MaxCookCount) return null;
        if (!CanAfford(data.hireCost)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return null; }

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(cookStaffPrefab);
        staff.gameObject.name = $"Cook_{nextId}";
        staff.Init(data, nextId, nameKey ?? PickNameKey(), hireVariance);
        nextId++;
        cookStaffs.Add(staff);
        OnHired(staff);
        OnRosterChanged?.Invoke();
        return staff;
    }

    public ServerStaff HireServerStaff(StaffData data, float hireVariance = 0f, string nameKey = null)
    {
        if (data == null || serverStaffPrefab == null) return null;
        if (serverStaffs.Count >= MaxServerCount) return null;
        if (!CanAfford(data.hireCost)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return null; }

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(serverStaffPrefab);
        staff.gameObject.name = $"Server_{nextId}";
        staff.Init(data, nextId, nameKey ?? PickNameKey(), hireVariance);
        nextId++;
        serverStaffs.Add(staff);
        OnHired(staff);
        OnRosterChanged?.Invoke();
        return staff;
    }

    public RiderStaff HireRiderStaff(StaffData data, float hireVariance = 0f, string nameKey = null)
    {
        if (data == null || riderStaffPrefab == null) return null;
        if (!IsRiderHiringUnlocked) return null;   // 라이더룸 가구 설치 필요
        if (riderStaffs.Count >= MaxRiderCount) return null;
        if (!CanAfford(data.hireCost)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return null; }

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(riderStaffPrefab);
        staff.gameObject.name = $"Rider_{nextId}";

        // 라이더는 밖(입구)에서 대기
        if (CustomerManager.Instance != null)
            staff.transform.position = CustomerManager.Instance.EntryPosition;

        staff.Init(data, nextId, nameKey ?? PickNameKey(), hireVariance);
        nextId++;
        riderStaffs.Add(staff);
        OnHired(staff);
        OnRosterChanged?.Invoke();
        return staff;
    }

    public bool FireCookStaff(CookStaff staff)
    {
        if (staff == null || !cookStaffs.Contains(staff)) return false;
        long severance = staff.EffectiveSalary;
        if (!CanAfford(severance)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return false; }

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
        if (!CanAfford(severance)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return false; }

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
        if (!CanAfford(severance)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return false; }

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
        if (!CanAfford(cost)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return false; }

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
        if (!CanAfford(cost)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return false; }

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
        if (!CanAfford(cost)) { MoneySystem.Instance?.NotifyInsufficientFunds(); return false; }

        MoneySystem.Instance.Spend(cost);
        staff.SetData(next);
        OnRosterChanged?.Invoke();
        return true;
    }

    private bool CanAfford(long amount) =>
        MoneySystem.Instance != null && MoneySystem.Instance.CanAfford(amount);

    // ─── Save / Load ───

    public StaffSaveData[] CollectSaveData()
    {
        var list = new List<StaffSaveData>();
        foreach (var c in cookStaffs)   if (c != null) list.Add(c.ToData("Cook"));
        foreach (var s in serverStaffs) if (s != null) list.Add(s.ToData("Server"));
        foreach (var r in riderStaffs)  if (r != null) list.Add(r.ToData("Rider"));
        return list.ToArray();
    }

    public void RestoreFromData(StaffSaveData[] data)
    {
        ClearAllStaff();
        if (data == null) return;

        int maxId = 0;
        foreach (var sd in data)
        {
            Staff staff = sd.role switch
            {
                "Cook"   => cookStaffPrefab   != null ? Instantiate(cookStaffPrefab)   as Staff : null,
                "Server" => serverStaffPrefab != null ? Instantiate(serverStaffPrefab) as Staff : null,
                "Rider"  => riderStaffPrefab  != null ? Instantiate(riderStaffPrefab)  as Staff : null,
                _ => null,
            };
            if (staff == null)
            {
                continue;
            }

            staff.gameObject.name = $"{sd.role}_{sd.id}";
            staff.FromData(sd);

            switch (staff)
            {
                case CookStaff c:   cookStaffs.Add(c);   break;
                case ServerStaff s: serverStaffs.Add(s); break;
                case RiderStaff r:  riderStaffs.Add(r);  break;
            }

            if (sd.id > maxId) maxId = sd.id;
        }
        nextId = maxId + 1;

        // 로드 시: 출퇴근 연출 없이 각자 근무지에 바로 배치 (입구에서 다같이 등장→복귀 방지)
        foreach (var s in GetAllStaffs()) s.SnapToWorkPosition();

        OnRosterChanged?.Invoke();
    }

    private void ClearAllStaff()
    {
        foreach (var c in cookStaffs)   if (c != null) Destroy(c.gameObject);
        foreach (var s in serverStaffs) if (s != null) Destroy(s.gameObject);
        foreach (var r in riderStaffs)  if (r != null) Destroy(r.gameObject);
        cookStaffs.Clear();
        serverStaffs.Clear();
        riderStaffs.Clear();
    }
}
