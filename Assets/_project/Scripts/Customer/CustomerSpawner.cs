using System.Linq;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [SerializeField] private CounterManager counterManager;
    [SerializeField] private CustomerData[] pool;
    [SerializeField] private QueueManager queueManager;
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private Transform entryPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float _minSpawnInterval = 10f;
    [SerializeField] private float _maxSpawnInterval = 30f;
    private float _spawnInterval;
    private float _spawnTimer;
    public Vector3 EntryPosition => entryPoint.position;
    public Vector3 ExitPosition => exitPoint.position;
    private void Start()
    {
        _spawnInterval = Random.Range(_minSpawnInterval, _maxSpawnInterval);
    }

    private void Update()
    {
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
        go.GetComponent<Customer>().Init(data, counterManager, seatManager, queueManager, exitPoint.position);
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
