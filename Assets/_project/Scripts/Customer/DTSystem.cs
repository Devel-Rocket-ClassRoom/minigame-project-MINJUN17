using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 드라이브 쓰루 매니저 + 스포너.
/// - 해금 상태는 CatalogManager가 보관 (dtCatalogData가 해금됐는지 조회)
/// - 해금 + 영업 중일 때 차 스폰. 영업 종료 시 스폰만 멈추고 차는 정상 흐름 유지.
/// - 차로 점유 한도(DTLane.IsFull) 체크.
/// </summary>
public class DTSystem : MonoBehaviour
{
    public static DTSystem Instance;

    [Header("카탈로그 식별")]
    [Tooltip("DT 시스템을 나타내는 카탈로그 항목 (보통 DTOrderWindow나 DT 마커 FurnitureData)")]
    [SerializeField] private FurnitureData dtCatalogData;

    [Header("스폰")]
    [SerializeField] private GameObject dtCustomerPrefab;
    [SerializeField] private DTCustomerData[] pool;
    [SerializeField] private float minSpawnInterval = 6f;
    [SerializeField] private float maxSpawnInterval = 14f;
    [SerializeField] private int newSpawnCutoffHour = 22;   // 이 시각 이후 신규 스폰 차단
    [SerializeField] private TimeSystem timeSystem;

    private float _spawnTimer;
    private float _spawnInterval;
    private bool _spawning;

    public event Action OnUnlocked;            // CatalogManager 프록시 (외부 호환)

    public FurnitureData DTCatalogData => dtCatalogData;
    public bool IsUnlocked => CatalogManager.Instance != null
                              && CatalogManager.Instance.IsUnlocked(dtCatalogData);
    public bool IsSpawning => _spawning;
    public int ActiveCarCount => DTLane.Instance != null ? DTLane.Instance.ActiveCarCount : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _spawnInterval = RollSpawnInterval();
        if (CatalogManager.Instance != null)
            CatalogManager.Instance.OnFurnitureUnlocked += HandleFurnitureUnlocked;
    }

    private void OnDestroy()
    {
        if (CatalogManager.Instance != null)
            CatalogManager.Instance.OnFurnitureUnlocked -= HandleFurnitureUnlocked;
    }

    private void HandleFurnitureUnlocked(FurnitureData f)
    {
        if (f == dtCatalogData) OnUnlocked?.Invoke();
    }

    public void StartSpawning()
    {
        _spawning = true;
        _spawnInterval = RollSpawnInterval();
        _spawnTimer = 0f;
    }

    public void StopSpawning() => _spawning = false;

    private float RollSpawnInterval()
    {
        return Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private void Update()
    {
        if (!_spawning) return;
        if (!IsUnlocked) return;
        if (!CanSpawn()) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0f;
            _spawnInterval = RollSpawnInterval();
            Spawn();
        }
    }

    private bool CanSpawn()
    {
        if (DTLane.Instance == null || DTLane.Instance.WaypointCount == 0) return false;
        if (DTLane.Instance.IsFull) return false;
        if (DTWindowManager.Instance == null) return false;
        if (DTWindowManager.Instance.FirstOrderWindow == null) return false;
        if (DTWindowManager.Instance.FirstPickupWindow == null) return false;
        if (dtCustomerPrefab == null || pool == null || pool.Length == 0) return false;

        if (timeSystem != null)
        {
            if (!timeSystem.IsOpen) return false;
            if (timeSystem.Hour >= newSpawnCutoffHour) return false;
        }
        return true;
    }

    private void Spawn()
    {
        var data = PickByWeight();
        if (data == null) return;

        var lane = DTLane.Instance;
        var orderWin = DTWindowManager.Instance.FirstOrderWindow;
        var pickupWin = DTWindowManager.Instance.FirstPickupWindow;

        var entry = lane.GetWaypoint(0);
        Vector3 spawnPos = entry != null ? entry.position : lane.transform.position;

        var go = Instantiate(dtCustomerPrefab, spawnPos, Quaternion.identity);
        var car = go.GetComponent<DTCustomer>();
        if (car == null)
        {
            Debug.LogWarning("[DTSystem] dtCustomerPrefab에 DTCustomer 컴포넌트 없음");
            Destroy(go);
            return;
        }
        car.Init(data, lane, orderWin, pickupWin);
    }

    /// <summary>디버그용: 만족도 무시하고 1대 강제 스폰.</summary>
    public void DebugForceSpawnOne()
    {
        if (!CanSpawn())
        {
            Debug.LogWarning($"[DTSystem] CanSpawn=false\n" +
                $"  DTLane.Instance={DTLane.Instance != null}\n" +
                $"  WaypointCount={DTLane.Instance?.WaypointCount ?? 0}\n" +
                $"  IsFull={DTLane.Instance?.IsFull}\n" +
                $"  DTWindowManager.Instance={DTWindowManager.Instance != null}\n" +
                $"  FirstOrderWindow={DTWindowManager.Instance?.FirstOrderWindow != null}\n" +
                $"  FirstPickupWindow={DTWindowManager.Instance?.FirstPickupWindow != null}\n" +
                $"  dtCustomerPrefab={dtCustomerPrefab != null}\n" +
                $"  pool.Length={pool?.Length ?? 0}\n" +
                $"  timeSystem.IsOpen={timeSystem?.IsOpen}\n" +
                $"  timeSystem.Hour={timeSystem?.Hour}  cutoff={newSpawnCutoffHour}");
            return;
        }
        Spawn();
    }

    public DTCustomerData PickByWeight()
    {
        if (pool == null || pool.Length == 0) return null;
        float total = pool.Sum(d => d != null ? Mathf.Max(0f, d.spawnWeight) : 0f);
        if (total <= 0f) return pool[0];
        float r = Random.Range(0f, total);
        float acc = 0f;
        foreach (var d in pool)
        {
            if (d == null) continue;
            acc += Mathf.Max(0f, d.spawnWeight);
            if (r <= acc) return d;
        }
        return pool[pool.Length - 1];
    }
}
