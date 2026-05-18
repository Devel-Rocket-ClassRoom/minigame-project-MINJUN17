using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class CustomerManager : MonoBehaviour // 매니저 겸 스포너
{
    [SerializeField] private CounterManager counterManager;
    [SerializeField] private CustomerData[] pool;
    [SerializeField] private QueueManager queueManager;
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private Transform entryPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float _minSpawnInterval = 10f;
    [SerializeField] private float _maxSpawnInterval = 30f;

    private readonly HashSet<Customer> _active = new();
    public int ActiveCount => _active.Count; // 남은 손님 없어야 영업종료
    public void Register(Customer c) => _active.Add(c);
    public void Unregister(Customer c) => _active.Remove(c);

    private float _spawnInterval;
    private float _spawnTimer;
    private bool _spawning;
    public Vector3 EntryPosition => entryPoint.position;
    public Vector3 ExitPosition => exitPoint.position;

    public event Action OnEmpty;

    private void Start()
    {
        _spawnInterval = Random.Range(_minSpawnInterval, _maxSpawnInterval);
    }

    public void StartSpawning()
    {
        _spawning = true;
        _spawnInterval = UnityEngine.Random.Range(_minSpawnInterval, _maxSpawnInterval);
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
            _spawnInterval = Random.Range(_minSpawnInterval, _maxSpawnInterval);
            Spawn();
        }
    }

    private void Spawn()
    {
        if (!queueManager.HasSpace) return;
        if (pool == null || pool.Length == 0) return;
        var data = PickByWeight();
        if (data == null || data.customerPrefab == null) return;
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
