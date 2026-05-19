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

    private int nextId = 1;
    private readonly List<CookStaff> cookStaffs = new();
    private readonly List<ServerStaff> serverStaffs = new();

    public IReadOnlyList<CookStaff> CookStaffs => cookStaffs;
    public IReadOnlyList<ServerStaff> ServerStaffs => serverStaffs;

    // 홀 직원은 카운터 수만큼만 가능
    private int MaxServerCount => CounterManager.Instance.CounterCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Init()
    {
        // 1주차 튜토리얼 완료 상태: 요리사 1 + 홀 2 (카운터 수만큼)
        HireCookStaff(starterCookData);

        foreach (var counter in CounterManager.Instance.Counters)
        {
            if (!counter.IsEmpty) continue;
            var server = HireServerStaff(starterServerData);
            if (server == null) break;
            AssignCounter(server, counter);
        }
    }

    // === 채용: Cook ===
    public CookStaff HireCookStaff(StaffData data)
    {
        if (data == null || cookStaffPrefab == null) return null;
        if (!CanAfford(data.hireCost)) return null;

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(cookStaffPrefab);
        staff.gameObject.name = $"Cook_{nextId}";
        staff.Init(data, nextId, kitchenIdlePos);
        nextId++;
        cookStaffs.Add(staff);
        return staff;
    }

    // === 채용: Server ===
    public ServerStaff HireServerStaff(StaffData data)
    {
        if (data == null || serverStaffPrefab == null) return null;
        if (serverStaffs.Count >= MaxServerCount) return null;
        if (!CanAfford(data.hireCost)) return null;

        MoneySystem.Instance.Spend(data.hireCost);
        var staff = Instantiate(serverStaffPrefab);
        staff.gameObject.name = $"Server_{nextId}";
        staff.Init(data, nextId);
        nextId++;
        serverStaffs.Add(staff);
        return staff;
    }

    // === 해고 ===
    public bool FireCookStaff(CookStaff staff)
    {
        if (staff == null || !cookStaffs.Contains(staff)) return false;
        if (!CanAfford(staff.Data.salary)) return false;

        MoneySystem.Instance.Spend(staff.Data.salary);
        cookStaffs.Remove(staff);
        Destroy(staff.gameObject);
        return true;
    }

    public bool FireServerStaff(ServerStaff staff)
    {
        if (staff == null || !serverStaffs.Contains(staff)) return false;
        if (!CanAfford(staff.Data.salary)) return false;

        MoneySystem.Instance.Spend(staff.Data.salary);
        UnassignCounter(staff);
        serverStaffs.Remove(staff);
        Destroy(staff.gameObject);
        return true;
    }

    // === 카운터 배정 (Server 전용) ===
    public bool AssignCounter(ServerStaff staff, Counter counter)
    {
        if (staff == null || counter == null) return false;
        if (!counter.IsEmpty) return false;

        if (staff.AssignedCounter != null)
            staff.AssignedCounter.UnassignStaff();

        staff.AssignTo(counter);
        counter.AssignStaff(staff);
        return true;
    }
    public long CalculateTotalSalaryCost()
    {
        long salary = 0;

        foreach (var cook in StaffManager.Instance.CookStaffs)
            salary += cook.Data.salary;
        foreach (var server in StaffManager.Instance.ServerStaffs)
            salary += server.Data.salary;
        //foreach (var rider in StaffManager.Instance.RiderStaffs)
        //    salary += rider.Data.salary;

        return salary;
    }
    public void UnassignCounter(ServerStaff staff)
    {
        if (staff == null || staff.AssignedCounter == null) return;
        staff.AssignedCounter.UnassignStaff();
        staff.Unassign();
    }

    private bool CanAfford(long amount) =>
        MoneySystem.Instance != null && MoneySystem.Instance.CanAfford(amount);
}
