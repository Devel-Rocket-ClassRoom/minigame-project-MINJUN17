using System.Collections.Generic;
using UnityEngine;

public class StaffManager : MonoBehaviour
{
    public static StaffManager Instance;

    [SerializeField] private CookStaff cookStaffPrefab;
    [SerializeField] private ServerStaff serverStaffPrefab;
    [SerializeField] private StaffData starterCookData;
    [SerializeField] private StaffData starterServerData;
    [SerializeField] private Transform kitchenIdlePos;
    [SerializeField] private TimeSystem timeSystem;

    [Header("등급별 SO (Junior, Senior, Manager 순)")]
    [SerializeField] private List<StaffData> cookGrades;
    [SerializeField] private List<StaffData> serverGrades;
    [SerializeField] private List<StaffData> riderGrades;

    private int nextId = 1;
    private readonly List<CookStaff> cookStaffs = new();
    private readonly List<ServerStaff> serverStaffs = new();

    public IReadOnlyList<CookStaff> CookStaffs => cookStaffs;
    public IReadOnlyList<ServerStaff> ServerStaffs => serverStaffs;

    public int MaxServerCount => CounterManager.Instance.CounterCount;
    public int MaxCookCount => CounterManager.Instance.CounterCount;

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
        staff.Init(data, nextId, kitchenIdlePos, hireVariance);
        nextId++;
        cookStaffs.Add(staff);
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
        staff.Init(data, nextId, hireVariance);
        nextId++;
        serverStaffs.Add(staff);
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
        return true;
    }

    public long CalculateTotalSalaryCost()
    {
        long salary = 0;
        foreach (var cook in cookStaffs)   salary += cook.EffectiveSalary;
        foreach (var server in serverStaffs) salary += server.EffectiveSalary;
        return salary;
    }

    // === 등급 조회 ===
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

    // === 업그레이드(승급) ===
    public bool UpgradeCook(CookStaff staff)
    {
        if (staff == null || !staff.CanUpgrade) return false;
        var next = GetNextGrade(staff.Data);
        if (next == null) return false;

        long cost = next.hireCost / 2;
        if (!CanAfford(cost)) return false;

        MoneySystem.Instance.Spend(cost);
        staff.SetData(next);
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
        return true;
    }

    private bool CanAfford(long amount) =>
        MoneySystem.Instance != null && MoneySystem.Instance.CanAfford(amount);
}
