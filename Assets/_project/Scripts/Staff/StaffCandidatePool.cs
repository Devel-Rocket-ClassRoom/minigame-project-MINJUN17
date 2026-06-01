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

    private readonly List<StaffCandidate> _applicants = new();
    private readonly List<RecruitmentTicket> _pendingTickets = new();
    private bool _purchasedThisMonth = false;

    public IReadOnlyList<StaffCandidate> Applicants => _applicants;
    public IReadOnlyList<RecruitmentTicket> PendingTickets => _pendingTickets;
    public IReadOnlyList<RecruitmentTierConfig> TierConfigs => tierConfigs;
    public bool CanPurchaseThisMonth => !_purchasedThisMonth;

    public event Action OnApplicantsChanged;
    public event Action OnPendingTicketsChanged;
    /// <summary>이번 달 구매 가능 상태가 바뀔 때 (구매 직후 / 새 달 시작) — UI 갱신용.</summary>
    public event Action OnPurchaseStateChanged;

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
        OnPurchaseStateChanged?.Invoke();
        return true;
    }

    private void HandleDayStarted()
    {
        bool wasBlocked = _purchasedThisMonth;
        _purchasedThisMonth = false;
        if (wasBlocked) OnPurchaseStateChanged?.Invoke();

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

    /// <summary>튜토리얼용 — 대기 중인 모집 티켓 전부 제거 (모집 즉시처리 후 잔여 티켓이 나중에 후보를 또 뱉지 않게).</summary>
    public void CancelAllPendingTickets()
    {
        if (_pendingTickets.Count == 0) return;
        _pendingTickets.Clear();
        OnPendingTicketsChanged?.Invoke();
    }

    /// <summary>튜토리얼용 — 모집 대기 없이 지정 역할/등급 후보를 즉시 풀에 추가.</summary>
    public void SeedApplicant(StaffRole role, StaffType grade)
    {
        var baseData = StaffManager.Instance != null ? StaffManager.Instance.GetGrade(role, grade) : null;
        if (baseData == null) return;
        AddApplicant(new StaffCandidate
        {
            candidateName = PickName(),
            baseData      = baseData,
            hireVariance  = 0f,
        });
        OnApplicantsChanged?.Invoke();
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
        bool riderUnlocked = isDeliveryUnlocked
            || (StaffManager.Instance != null && StaffManager.Instance.IsRiderHiringUnlocked);
        if (!riderUnlocked)
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
        // StaffManager의 StaffNamePool 공용 사용 (50개 등록된 이름)
        string key = StaffManager.Instance?.PickNameKey();
        if (!string.IsNullOrEmpty(key)) return key;

        // 폴백: 이름 풀 없거나 비어있으면 자동 생성
        return $"후보_{Random.Range(1000, 9999)}";
    }

    public bool Hire(StaffCandidate candidate)
    {
        if (candidate == null || !_applicants.Contains(candidate)) return false;

        bool ok = candidate.baseData.role switch
        {
            StaffRole.Cook   => StaffManager.Instance.HireCookStaff(candidate.baseData, candidate.hireVariance, candidate.candidateName) != null,
            StaffRole.Server => StaffManager.Instance.HireServerStaff(candidate.baseData, candidate.hireVariance, candidate.candidateName) != null,
            StaffRole.Rider  => StaffManager.Instance.HireRiderStaff(candidate.baseData, candidate.hireVariance, candidate.candidateName) != null,
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

    // ─── Save / Load ───
    public CandidatePoolData ToData()
    {
        var apps = new List<CandidateEntry>();
        foreach (var c in _applicants)
        {
            if (c == null || c.baseData == null) continue;
            apps.Add(new CandidateEntry
            {
                candidateName   = c.candidateName,
                staffDataSaveId = (c.baseData as ISaveIdentifiable)?.SaveId,
                hireVariance    = c.hireVariance,
            });
        }

        var tickets = new List<TicketEntry>();
        foreach (var t in _pendingTickets)
        {
            if (t == null) continue;
            tickets.Add(new TicketEntry { tier = (int)t.tier, monthsRemaining = t.monthsRemaining });
        }

        return new CandidatePoolData
        {
            applicants         = apps,
            pendingTickets     = tickets,
            purchasedThisMonth = _purchasedThisMonth,
        };
    }

    public void FromData(CandidatePoolData data)
    {
        _applicants.Clear();
        _pendingTickets.Clear();
        _purchasedThisMonth = false;

        if (data != null)
        {
            if (data.applicants != null)
            {
                foreach (var e in data.applicants)
                {
                    if (e == null) continue;
                    var sd = SaveIdRegistry.GetById<StaffData>(e.staffDataSaveId);
                    if (sd == null)
                    {
                        Debug.LogWarning($"[StaffCandidatePool] StaffData 못 찾음: {e.staffDataSaveId}");
                        continue;
                    }
                    _applicants.Add(new StaffCandidate
                    {
                        candidateName = e.candidateName,
                        baseData      = sd,
                        hireVariance  = e.hireVariance,
                    });
                }
            }
            if (data.pendingTickets != null)
            {
                foreach (var t in data.pendingTickets)
                {
                    if (t == null) continue;
                    _pendingTickets.Add(new RecruitmentTicket
                    {
                        tier            = (RecruitmentTier)t.tier,
                        monthsRemaining = t.monthsRemaining,
                    });
                }
            }
            _purchasedThisMonth = data.purchasedThisMonth;
        }

        OnApplicantsChanged?.Invoke();
        OnPendingTicketsChanged?.Invoke();
        OnPurchaseStateChanged?.Invoke();
    }
}
