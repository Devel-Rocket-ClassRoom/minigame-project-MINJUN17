using System;
using System.Collections.Generic;
using UnityEngine;

public class MarketingManager : MonoBehaviour
{
    public static MarketingManager Instance;

    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private TimeSystem timeSystem;

    [Header("전체 마케팅 카탈로그 (UnlockShopPanel 에서 순회)")]
    [SerializeField] private List<MarketingData> allMarketing;
    public IReadOnlyList<MarketingData> AllMarketing => allMarketing;

    public event Action<MarketingData> OnMarketingPurchased;
    public event Action OnActiveChanged;

    private class ActiveCampaign
    {
        public MarketingData Data;
        public int RemainingMonths;
    }

    private readonly List<ActiveCampaign> _active = new();
    private readonly List<MarketingData> _pending = new();

    public IReadOnlyList<MarketingData> PendingCampaigns => _pending;
    public int ActiveCount => _active.Count;

    /// <summary>해당 마케팅이 현재 활성(_active) 중인지.</summary>
    public bool IsActive(MarketingData data)
    {
        if (data == null) return false;
        foreach (var c in _active)
            if (c != null && c.Data == data) return true;
        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        timeSystem.OnDayStarted += HandleDayStarted;
    }

    private void OnDestroy()
    {
        if (timeSystem != null) timeSystem.OnDayStarted -= HandleDayStarted;
    }

    public bool Apply(MarketingData data)
    {
        if (data == null) return false;
        if (!SatisfactionSystem.Instance.Spend(data.satisfactionCost)) return false;

        _pending.Add(data);
        OnMarketingPurchased?.Invoke(data);
        return true;
    }

    private void HandleDayStarted()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].RemainingMonths--;
            if (_active[i].RemainingMonths <= 0) _active.RemoveAt(i);
        }

        foreach (var p in _pending)
            _active.Add(new ActiveCampaign { Data = p, RemainingMonths = p.durationMonths });
        _pending.Clear();

        RecomputeMultiplier();
        OnActiveChanged?.Invoke();
    }

    private void RecomputeMultiplier()
    {
        float sumBoost = 0f;
        foreach (var c in _active) sumBoost += c.Data.spawnBoost;

        float multiplier = 1f + Mathf.Log(1f + Mathf.Max(0f, sumBoost));
        customerManager.SetMarketingMultiplier(multiplier);
    }

    // ─── Save / Load ───
    public MarketingSaveData ToData()
    {
        var active = new List<ActiveCampaignEntry>();
        foreach (var a in _active)
        {
            if (a == null || a.Data == null) continue;
            active.Add(new ActiveCampaignEntry
            {
                marketingSaveId = a.Data.SaveId,
                remainingMonths = a.RemainingMonths,
            });
        }

        var pending = new List<string>();
        foreach (var p in _pending)
        {
            if (p == null) continue;
            pending.Add(p.SaveId);
        }

        return new MarketingSaveData { active = active, pending = pending };
    }

    public void FromData(MarketingSaveData data)
    {
        _active.Clear();
        _pending.Clear();

        if (data != null)
        {
            if (data.active != null)
            {
                foreach (var e in data.active)
                {
                    if (e == null) continue;
                    var md = SaveIdRegistry.GetById<MarketingData>(e.marketingSaveId);
                    if (md == null)
                    {
                        Debug.LogWarning($"[MarketingManager] MarketingData 못 찾음: {e.marketingSaveId}");
                        continue;
                    }
                    _active.Add(new ActiveCampaign { Data = md, RemainingMonths = e.remainingMonths });
                }
            }
            if (data.pending != null)
            {
                foreach (var id in data.pending)
                {
                    var md = SaveIdRegistry.GetById<MarketingData>(id);
                    if (md == null)
                    {
                        Debug.LogWarning($"[MarketingManager] MarketingData 못 찾음: {id}");
                        continue;
                    }
                    _pending.Add(md);
                }
            }
        }

        RecomputeMultiplier();
        OnActiveChanged?.Invoke();
    }
}
