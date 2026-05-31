using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class CustomerManager : MonoBehaviour // 매니저 겸 스포너
{
    public static CustomerManager Instance;

    [SerializeField] private CounterManager counterManager;
    [SerializeField] private CustomerData[] pool;
    [SerializeField] private QueueManager queueManager;
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private Transform entryPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float _minSpawnInterval = 10f;
    [SerializeField] private float _maxSpawnInterval = 30f;
    [SerializeField] private Transform[] waitingSlots;

    private readonly HashSet<Customer> _active = new();
    private readonly List<Customer> _waitingForSeat = new();

    /// <summary>현재 방문 가능한(해금된) 손님들. 잠긴 손님은 preferredMenu가 해금되면 자동 해금된다.</summary>
    private readonly HashSet<CustomerData> _unlocked = new();

    /// <summary>"새 손님 등장!" 팝업을 이미 띄운 손님 (첫 방문 1회만 노출).</summary>
    private readonly HashSet<CustomerData> _introduced = new();

    /// <summary>새로 해금된 손님이 처음 스폰될 때 발화 (인자: 해당 CustomerData). 팝업 UI가 구독.</summary>
    public event Action<CustomerData> OnNewCustomerIntroduced;

    /// <summary>SaveIdRegistry용 — 전체 손님 풀 노출.</summary>
    public IReadOnlyList<CustomerData> Pool => pool;

    /// <summary>특정 손님이 해금됐는지.</summary>
    public bool IsUnlocked(CustomerData d) => d != null && _unlocked.Contains(d);

    /// <summary>해금된 손님 목록 (pool 순서 유지). 손님 도감 UI용.</summary>
    public List<CustomerData> GetUnlockedCustomers()
    {
        var list = new List<CustomerData>();
        if (pool != null)
            foreach (var d in pool)
                if (d != null && _unlocked.Contains(d)) list.Add(d);
        return list;
    }
    public int ActiveCount => _active.Count; // 남은 손님 없어야 영업종료
    public IReadOnlyCollection<Customer> ActiveCustomers => _active;
    public void Register(Customer c) => _active.Add(c);
    public void Unregister(Customer c) { _active.Remove(c); _waitingForSeat.Remove(c); }

    public void RegisterWaitingForSeat(Customer c)
    {
        if (!_waitingForSeat.Contains(c)) _waitingForSeat.Add(c);
    }

    public void UnregisterWaitingForSeat(Customer c) => _waitingForSeat.Remove(c);

    public Vector3 GetWaitingSlotPosition(Customer c)
    {
        // 자리 대기 손님은 전부 스폰점에 겹쳐 대기 — 자리 비면 사이드워크 큐로 합류해 일자로 늘어섬
        return entryPoint.position;
    }

    private float _spawnInterval;
    private float _spawnTimer;
    private bool _spawning;
    private float _marketingMultiplier = 1f;
    public Vector3 EntryPosition => entryPoint.position;
    public Vector3 ExitPosition => exitPoint.position;

    public event Action OnEmpty;

    public void SetMarketingMultiplier(float m) => _marketingMultiplier = Mathf.Max(0.01f, m);

    private float RollSpawnInterval()
    {
        float baseInterval = Random.Range(_minSpawnInterval, _maxSpawnInterval);
        return baseInterval / _marketingMultiplier;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _spawnInterval = RollSpawnInterval();

        EnsureStartingUnlocked();
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnMenuUnlocked += HandleMenuUnlocked;
            // 로드/시작 직후 catch-up: 이미 해금된 메뉴들에 매칭되는 손님 해금
            foreach (var m in CatalogManager.Instance.UnlockedMenus)
                HandleMenuUnlocked(m);
        }
    }

    private void OnDestroy()
    {
        if (CatalogManager.Instance != null)
            CatalogManager.Instance.OnMenuUnlocked -= HandleMenuUnlocked;
    }

    // ─── 손님 해금 ───

    /// <summary>unlockedFromStart 손님을 항상 해금 상태로 보장 (중복 추가 안전).</summary>
    private void EnsureStartingUnlocked()
    {
        if (pool == null) return;
        foreach (var d in pool)
            if (d != null && d.unlockedFromStart) _unlocked.Add(d);
    }

    /// <summary>음식이 해금되면 그 음식을 preferredMenu로 가진 손님을 해금.</summary>
    private void HandleMenuUnlocked(MenuData menu)
    {
        if (menu == null || pool == null) return;
        foreach (var d in pool)
        {
            if (d == null || _unlocked.Contains(d)) continue;
            if (d.preferredMenu == menu)
            {
                _unlocked.Add(d);
                Debug.Log($"[CustomerManager] 신규 손님 해금 (선호 음식 {menu.name} 해금): {d.name}");
            }
        }
    }

    public void StartSpawning()
    {
        _spawning = true;
        _spawnInterval = RollSpawnInterval();
        _spawnTimer = 0f;
    }

    public void StopSpawning() => _spawning = false;

    // 영업 종료 시 호출 - 자리 대기 + 카운터 큐 손님 전원 강제 퇴장
    // (주문/식사 중인 손님은 정상 흐름 유지)
    public void ForceLeaveWaitingCustomers()
    {
        // _waitingForSeat 복사 후 순회 (ForceLeave에서 리스트 변경됨)
        var snapshot = new List<Customer>(_waitingForSeat);
        foreach (var c in snapshot)
            if (c != null) c.ForceLeave();

        // 사이드워크 큐 손님도 전원 퇴장
        if (queueManager != null) queueManager.ForceLeaveAll();
    }

    private void Update()
    {
        if (!_spawning) return;
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer > _spawnInterval)
        {
            _spawnTimer = 0f;
            _spawnInterval = RollSpawnInterval();
            Spawn();
        }
    }

    private void Spawn()
    {
        var data = PickByWeight();
        if (data == null || data.customerPrefab == null) return;
        var go = Instantiate(data.customerPrefab, entryPoint.position, Quaternion.identity);
        var c = go.GetComponent<Customer>();
        c.OnDespawned += HandleDespawn;
        Register(c);
        SalesTracker.Instance?.RecordCustomerVisit();   // 누적 방문 손님 수
        c.Init(data, counterManager, seatManager, queueManager, exitPoint.position);
    }

    /// <summary>
    /// 손님이 처음 "가게에 들어왔을 때" 1회만 소개 이벤트 발화 (시작 손님 포함 — 완전 첫 손님도 소개).
    /// 스폰은 화면 밖이라, 손님이 문 통과(가게 진입) 시점에 Customer가 직접 호출한다.
    /// </summary>
    public void TryIntroduceNewCustomer(CustomerData data)
    {
        if (data == null) return;
        if (!_introduced.Add(data)) return;   // 손님별 최초 1회만
        OnNewCustomerIntroduced?.Invoke(data);
    }

    private void HandleDespawn(Customer c)
    {
        Unregister(c);
        if (_active.Count == 0) OnEmpty?.Invoke();
    }

    public CustomerData PickByWeight()
    {
        // 해금된 손님만 후보로
        float total = 0f;
        foreach (var d in pool)
            if (d != null && _unlocked.Contains(d)) total += Mathf.Max(0f, d.spawnWeight);

        if (total <= 0f)
        {
            // 폴백: 가중치 합이 0이면 해금된 첫 손님이라도 반환
            foreach (var d in pool)
                if (d != null && _unlocked.Contains(d)) return d;
            return null;
        }

        float r = Random.Range(0f, total);
        float acc = 0f;
        foreach (var d in pool)
        {
            if (d == null || !_unlocked.Contains(d)) continue;
            acc += Mathf.Max(0f, d.spawnWeight);
            if (r <= acc) return d;
        }

        // 부동소수 오차 대비 — 해금된 마지막 손님
        for (int i = pool.Length - 1; i >= 0; i--)
            if (pool[i] != null && _unlocked.Contains(pool[i])) return pool[i];
        return null;
    }

    // ─── Save / Load ───

    public CustomerUnlockData ToData()
    {
        var ids = new List<string>();
        var introduced = new List<string>();
        foreach (var d in pool)
        {
            if (d is not ISaveIdentifiable s || string.IsNullOrEmpty(s.SaveId)) continue;
            if (_unlocked.Contains(d))   ids.Add(s.SaveId);
            if (_introduced.Contains(d)) introduced.Add(s.SaveId);
        }
        return new CustomerUnlockData { unlockedIds = ids, introducedIds = introduced };
    }

    public void FromData(CustomerUnlockData data)
    {
        _unlocked.Clear();
        _introduced.Clear();
        EnsureStartingUnlocked();                       // 시작 손님은 항상 포함
        if (data?.unlockedIds != null)
        {
            foreach (var id in data.unlockedIds)
            {
                var cd = SaveIdRegistry.GetById<CustomerData>(id);
                if (cd != null) _unlocked.Add(cd);
            }
        }
        if (data?.introducedIds != null)
        {
            foreach (var id in data.introducedIds)
            {
                var cd = SaveIdRegistry.GetById<CustomerData>(id);
                if (cd != null) _introduced.Add(cd);
            }
        }
    }
}
