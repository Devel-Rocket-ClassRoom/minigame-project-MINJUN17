using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class StaffCandidatePool : MonoBehaviour
{
    public static StaffCandidatePool Instance;

    [SerializeField] private TimeSystem timeSystem;
    [SerializeField] private List<RecruitmentTierConfig> tierConfigs;
    [SerializeField] private int poolCap = 5;
    [SerializeField] private int applicantsPerTicket = 2;
    [SerializeField] private int ticketDelayMonths = 2;
    [SerializeField, Range(0f, 0.5f)] private float statVariance = 0.1f;
    [SerializeField] private bool isDeliveryUnlocked = false;
    [SerializeField] private string[] candidateNames;

    private readonly List<StaffCandidate> _applicants = new();
    private readonly List<RecruitmentTicket> _pendingTickets = new();
    private bool _purchasedThisMonth = false;

    public IReadOnlyList<StaffCandidate> Applicants => _applicants;
    public IReadOnlyList<RecruitmentTicket> PendingTickets => _pendingTickets;
    public bool CanPurchaseThisMonth => !_purchasedThisMonth;

    public event Action OnApplicantsChanged;
    public event Action OnPendingTicketsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (timeSystem != null) timeSystem.OnDayStarted += HandleDayStarted;
    }

    private void OnDestroy()
    {
        if (timeSystem != null) timeSystem.OnDayStarted -= HandleDayStarted;
    }

    public bool PurchaseRecruitment(RecruitmentTier tier)
    {
        if (_purchasedThisMonth) return false;
        var cfg = GetConfig(tier);
        if (cfg == null) return false;
        if (!SatisfactionSystem.Instance.Spend(cfg.satisfactionCost)) return false;

        _pendingTickets.Add(new RecruitmentTicket { tier = tier, monthsRemaining = ticketDelayMonths });
        _purchasedThisMonth = true;
        OnPendingTicketsChanged?.Invoke();
        return true;
    }

    private void HandleDayStarted()
    {
        _purchasedThisMonth = false;

        bool applicantsChanged = false;
        bool ticketsChanged = false;

        for (int i = _pendingTickets.Count - 1; i >= 0; i--)
        {
            _pendingTickets[i].monthsRemaining--;
            if (_pendingTickets[i].monthsRemaining <= 0)
            {
                var tier = _pendingTickets[i].tier;
                for (int n = 0; n < applicantsPerTicket; n++)
                {
                    var c = MakeApplicant(tier);
                    if (c != null) { AddApplicant(c); applicantsChanged = true; }
                }
                _pendingTickets.RemoveAt(i);
                ticketsChanged = true;
            }
        }

        if (ticketsChanged) OnPendingTicketsChanged?.Invoke();
        if (applicantsChanged) OnApplicantsChanged?.Invoke();
    }

    private void AddApplicant(StaffCandidate c)
    {
        _applicants.Add(c);
        while (_applicants.Count > poolCap) _applicants.RemoveAt(0);
    }

    private StaffCandidate MakeApplicant(RecruitmentTier tier)
    {
        var cfg = GetConfig(tier);
        if (cfg == null) return null;

        var role = PickRole();
        var grade = PickGrade(cfg);
        var baseData = StaffManager.Instance.GetGrade(role, grade);
        if (baseData == null) return null;

        return new StaffCandidate
        {
            candidateName = PickName(),
            baseData = baseData,
            hireVariance = Random.Range(-statVariance, statVariance),
        };
    }

    private StaffRole PickRole()
    {
        if (!isDeliveryUnlocked)
            return Random.value < 0.5f ? StaffRole.Cook : StaffRole.Server;

        int r = Random.Range(0, 3);
        return r switch
        {
            0 => StaffRole.Cook,
            1 => StaffRole.Server,
            _ => StaffRole.Rider,
        };
    }

    private StaffType PickGrade(RecruitmentTierConfig cfg)
    {
        int total = cfg.juniorWeight + cfg.seniorWeight + cfg.managerWeight;
        if (total <= 0) return StaffType.Junior;

        int r = Random.Range(0, total);
        if (r < cfg.juniorWeight) return StaffType.Junior;
        if (r < cfg.juniorWeight + cfg.seniorWeight) return StaffType.Senior;
        return StaffType.Manager;
    }

    private string PickName()
    {
        if (candidateNames == null || candidateNames.Length == 0)
            return $"후보_{Random.Range(1000, 9999)}";
        return candidateNames[Random.Range(0, candidateNames.Length)];
    }

    public bool Hire(StaffCandidate candidate)
    {
        if (candidate == null || !_applicants.Contains(candidate)) return false;

        bool ok = candidate.baseData.role switch
        {
            StaffRole.Cook   => StaffManager.Instance.HireCookStaff(candidate.baseData, candidate.hireVariance) != null,
            StaffRole.Server => StaffManager.Instance.HireServerStaff(candidate.baseData, candidate.hireVariance) != null,
            // Rider 채용은 RiderManager 구현 후 연결
            _ => false,
        };

        if (ok)
        {
            _applicants.Remove(candidate);
            OnApplicantsChanged?.Invoke();
        }
        return ok;
    }

    private RecruitmentTierConfig GetConfig(RecruitmentTier tier)
    {
        if (tierConfigs == null) return null;
        foreach (var c in tierConfigs) if (c != null && c.tier == tier) return c;
        return null;
    }
}
