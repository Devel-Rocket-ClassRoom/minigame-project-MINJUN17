using System.Collections.Generic;
using UnityEngine;

public class StaffManager : MonoBehaviour
{
    public static StaffManager Instance;
    [SerializeField] private CounterManager counterManager;
    [SerializeField] private Staff staffPrefab;
    [SerializeField] private StaffData starterStaffData;   // 시작 시 자동 고용할 신입 데이터

    [SerializeField] private Transform toolPos; // 더미

    private int nextId = 1;
    private readonly List<Staff> staffs = new();

    public IReadOnlyList<Staff> Staffs => staffs;
    private int MaxStaffCount => counterManager.CounterCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 시작 시 신입 1명 자동 고용 + 첫 빈 카운터에 자동 배정 (튜토리얼 초기 상태)
        if (starterStaffData == null) return;
        var starter = HireStaff(starterStaffData);
        if (starter == null) return;
        var firstCounter = counterManager.GetFirstEmptyCounter();
        if (firstCounter != null) AssignCounter(starter, firstCounter);
    }

    // === 채용 ===
    public Staff HireStaff(StaffData data)
    {
        if (data == null) return null;
        if (staffPrefab == null) return null;
        if (staffs.Count >= MaxStaffCount) return null;
        if (MoneySystem.Instance == null) return null;
        if (!MoneySystem.Instance.CanAfford(data.hireCost)) return null;

        MoneySystem.Instance.Spend(data.hireCost);

        var staff = Instantiate(staffPrefab);
        staff.gameObject.name = $"Staff_{nextId}";
        staff.Init(data, nextId, toolPos);
        nextId++;

        staffs.Add(staff);
        return staff;
    }

    // === 해고 ===
    public bool FireStaff(Staff staff)
    {
        if (staff == null || !staffs.Contains(staff)) return false;
        if (!MoneySystem.Instance.CanAfford(staff.Data.salary)) return false;

        MoneySystem.Instance.Spend(staff.Data.salary);
        UnassignStaff(staff);
        staffs.Remove(staff);
        Destroy(staff.gameObject);
        return true;
    }

    // === 카운터 배정 ===
    public bool AssignCounter(Staff staff, Counter counter)
    {
        if (staff == null || counter == null) return false;
        if (!counter.IsEmpty) return false;

        if (staff.AssignedCounter != null)
            staff.AssignedCounter.UnassignStaff();

        staff.AssignTo(counter);
        counter.AssignStaff(staff);
        return true;
    }

    // === 카운터 해제 ===
    public void UnassignStaff(Staff staff)
    {
        if (staff == null || staff.AssignedCounter == null) return;
        staff.AssignedCounter.UnassignStaff();
        staff.Unassign();
    }
}
