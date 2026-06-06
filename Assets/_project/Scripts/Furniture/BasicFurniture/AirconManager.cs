using UnityEngine;

public class AirconManager : MonoBehaviour
{
    public static AirconManager Instance;

    [Range(0f, 1f)]
    [SerializeField] private float operationCostRate = 0.1f;       // 운영비 증가 비율 (기본 10%)
    [Range(1f, 2f)]
    [SerializeField] private float satisfactionMultiplier = 1.05f; // 손님 만족도 배율 (기본 5%)

    private bool _installed;

    public bool IsInstalled => _installed;
    public float SatisfactionMultiplier => _installed ? satisfactionMultiplier : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register()
    {
        _installed = true;
    }

    public void Unregister()
    {
        _installed = false;
    }

    /// <summary>기존 운영비에 더할 에어컨 추가 비용을 반환.</summary>
    public long CalculateExtraCost(long baseOperationCost)
    {
        if (!_installed) return 0;
        return Mathf.RoundToInt(baseOperationCost * operationCostRate);
    }
}
