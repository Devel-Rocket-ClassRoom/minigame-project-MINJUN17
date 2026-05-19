using System;
using System.Collections.Generic;
using System.Linq;
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
    public int ActiveCount => _active.Count; // 남은 손님 없어야 영업종료
    public void Register(Customer c) => _active.Add(c);
    public void Unregister(Customer c) { _active.Remove(c); _waitingForSeat.Remove(c); }

    public void RegisterWaitingForSeat(Customer c)
    {
        if (!_waitingForSeat.Contains(c)) _waitingForSeat.Add(c);
    }

    public void UnregisterWaitingForSeat(Customer c) => _waitingForSeat.Remove(c);

    public Vector3 GetWaitingSlotPosition(Customer c)
    {
        int idx = _waitingForSeat.IndexOf(c);
        if (waitingSlots == null || waitingSlots.Length == 0) return entryPoint.position;
        int slot = Mathf.Clamp(idx, 0, waitingSlots.Length - 1);
        return waitingSlots[slot].position;
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
    }

    public void StartSpawning()
    {
        _spawning = true;
        _spawnInterval = RollSpawnInterval();
        _spawnTimer = 0f;
    }

    public void StopSpawning() => _spawning = false;

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
        var go = Instantiate(data.customerPrefab, entryPoint.position, Quaternion.identity);
        var c = go.GetComponent<Customer>();
        c.OnDespawned += HandleDespawn;
        Register(c);
        c.Init(data, counterManager, seatManager, queueManager, exitPoint.position);
    }

    private void HandleDespawn(Customer c)
    {
        Unregister(c);
        if (_active.Count == 0) OnEmpty?.Invoke();
    }

    public CustomerData PickByWeight()
    {
        float total = pool.Sum(d => d.spawnWeight);
        float r = Random.Range(0, total);
        float acc = 0f;
        foreach (var d in pool)
        {
            acc += d.spawnWeight;
            if (r <= acc) return d;
        }
        return pool[pool.Length - 1];
    }
}
