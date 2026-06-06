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

    /// <summary>해당 마케팅이 활성(_active) 또는 당일 구매 대기(_pending) 중인지. (중복 구매 차단/UI 비활성화용)
    /// 단, 활성 중이라도 남은 기간이 1개월 이하이면 재구매 허용 (끊김 없이 연장 가능하도록).</summary>
    public bool IsActiveOrPending(MarketingData data)
    {
        if (data == null) return false;
        foreach (var c in _active)
            if (c != null && c.Data == data && c.RemainingMonths > 1) return true;
        foreach (var p in _pending)
            if (p == data) return true;
        return false;
    }

    /// <summary>활성/대기 중인 마케팅의 남은 개월 수. 대기 중이면 전체 기간, 없으면 0.</summary>
    public int GetRemainingMonths(MarketingData data)
    {
        if (data == null) return 0;
        foreach (var c in _active)
            if (c != null && c.Data == data) return c.RemainingMonths;
        foreach (var p in _pending)
            if (p == data) return data.durationMonths;
        return 0;
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
        if (IsActiveOrPending(data)) return false;   // 중복 구매 방지 — 효과 끝날 때까지 재구매 불가
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
