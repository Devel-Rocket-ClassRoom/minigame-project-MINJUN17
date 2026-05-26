using System;
using UnityEngine;

public class ReputationSystem : MonoBehaviour
{
    public static ReputationSystem Instance;

    public event Action<long> OnReputationChanged;

    private long _annualReputation;
    public long AnnualReputation => _annualReputation;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Report(int customerSatisfaction)
    {
        _annualReputation += customerSatisfaction;
        OnReputationChanged?.Invoke(_annualReputation);
    }

    public void ResetAnnual()
    {
        _annualReputation = 0;
        OnReputationChanged?.Invoke(_annualReputation);
    }

    // ─── Save / Load ───
    public ReputationData ToData() => new ReputationData { annualReputation = _annualReputation };

    public void FromData(ReputationData data)
    {
        if (data == null) return;
        _annualReputation = data.annualReputation;
        OnReputationChanged?.Invoke(_annualReputation);
    }
}
