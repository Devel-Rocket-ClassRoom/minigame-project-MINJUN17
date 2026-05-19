using System;
using System.Collections.Generic;
using UnityEngine;

public class RankingSystem : MonoBehaviour
{
    public static RankingSystem Instance;

    [SerializeField] private TimeSystem timeSystem;
    [SerializeField] private long minQualifyingScore = 100;
    [SerializeField] private float revenueDivisor = 100f;

    // 정렬된(내림차순) 더미 점수 100개. 인스펙터에서 채움.
    [SerializeField] private List<long> dummyTop100 = new();

    public struct YearResult
    {
        public long Score;
        public bool Qualified;
        public int Rank;        // 1~101 (101 = 더미 전부보다 낮음). Qualified가 false면 0.
        public long Revenue;
        public long Reputation;
    }

    public event Action<YearResult> OnYearRanked;
    public YearResult LastResult { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        timeSystem.OnYearEnded += HandleYearEnded;
    }

    private void OnDestroy()
    {
        if (timeSystem != null) timeSystem.OnYearEnded -= HandleYearEnded;
    }

    private void HandleYearEnded()
    {
        long revenue = SalesTracker.Instance.AnnualRevenue;
        long reputation = ReputationSystem.Instance.AnnualReputation;
        long score = (long)(revenue / revenueDivisor) + reputation;

        var result = new YearResult
        {
            Score = score,
            Revenue = revenue,
            Reputation = reputation,
            Qualified = score >= minQualifyingScore,
            Rank = 0,
        };

        if (result.Qualified)
            result.Rank = ComputeRank(score);

        LastResult = result;
        OnYearRanked?.Invoke(result);

        SalesTracker.Instance.ResetAnnual();
        ReputationSystem.Instance.ResetAnnual();
    }

    private int ComputeRank(long score)
    {
        for (int i = 0; i < dummyTop100.Count; i++)
            if (score >= dummyTop100[i]) return i + 1;
        return dummyTop100.Count + 1;
    }
}
